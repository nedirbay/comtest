using System.Text;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Infrastructure.Identity.Passwords;
using ComTest.Infrastructure.Messaging;
using ComTest.Shared.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Infrastructure.Identity;

public sealed class AccountService(
    UserManager<ApplicationUser> users,
    IPasswordHasher<ApplicationUser> passwordHasher,
    SessionManager sessions,
    SecurityEventLogger securityEvents,
    AuthEmailComposer emails,
    IEmailDispatcher emailDispatcher,
    TimeProvider timeProvider) : IAccountService
{
    private const string DefaultLocale = "tk";

    private static Error InvalidToken => Error.Validation(ErrorCodes.Auth.InvalidToken);

    /// <summary>
    /// Answers the same whether or not the email is taken, so this endpoint cannot be used to
    /// discover who has an account. The owner of an existing account is told by email instead.
    /// </summary>
    public async Task<Result> RegisterAsync(RegisterRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            Locale = request.Locale ?? DefaultLocale,
            CreatedAt = timeProvider.GetUtcNow(),
        };

        var passwordError = await ValidatePasswordAsync(user, request.Password, "password");

        if (passwordError is not null)
        {
            return Result.Failure(passwordError);
        }

        var existing = await users.FindByEmailAsync(request.Email);

        if (existing is not null)
        {
            // The new account path hashes a password, so do the same work here.
            passwordHasher.HashPassword(existing, request.Password);
            emailDispatcher.TryEnqueue(emails.ExistingAccountNotice(existing));

            return Result.Success();
        }

        IdentityResult created;

        try
        {
            created = await users.CreateAsync(user, request.Password);
        }
        catch (DbUpdateException)
        {
            // Two registrations for one email raced, and the unique index stopped the second.
            return Result.Success();
        }

        if (!created.Succeeded)
        {
            return Result.Success();
        }

        await users.AddToRoleAsync(user, Roles.Customer);
        await securityEvents.WriteAsync(SecurityEventType.Registered, user.Id, client, cancellationToken);
        await QueueConfirmationAsync(user);

        return Result.Success();
    }

    public async Task<Result> ConfirmEmailAsync(ConfirmEmailRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await users.FindByIdAsync(request.UserId.ToString());
        var token = DecodeToken(request.Token);

        if (user is null || token is null)
        {
            return Result.Failure(InvalidToken);
        }

        var result = await users.ConfirmEmailAsync(user, token);

        if (!result.Succeeded)
        {
            return Result.Failure(InvalidToken);
        }

        await securityEvents.WriteAsync(SecurityEventType.EmailConfirmed, user.Id, client, cancellationToken);

        return Result.Success();
    }

    public async Task ResendConfirmationAsync(EmailRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await users.FindByEmailAsync(request.Email);

        if (user is { EmailConfirmed: false })
        {
            await QueueConfirmationAsync(user);
        }
    }

    public async Task ForgotPasswordAsync(EmailRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await users.FindByEmailAsync(request.Email);

        if (user is null)
        {
            await securityEvents.WriteAsync(SecurityEventType.PasswordResetRequested, null, client, cancellationToken, request.Email);

            return;
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);
        emailDispatcher.TryEnqueue(emails.ResetPassword(user, EncodeToken(token)));

        await securityEvents.WriteAsync(SecurityEventType.PasswordResetRequested, user.Id, client, cancellationToken);
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await users.FindByIdAsync(request.UserId.ToString());
        var token = DecodeToken(request.Token);

        if (user is null || token is null)
        {
            return Result.Failure(InvalidToken);
        }

        // Resetting changes the security stamp, which the token is bound to, so it works only once.
        var result = await users.ResetPasswordAsync(user, token, request.NewPassword);

        if (!result.Succeeded)
        {
            return Result.Failure(IsInvalidToken(result) ? InvalidToken : PasswordErrors.ToValidationError(result, "newPassword"));
        }

        await users.ResetAccessFailedCountAsync(user);
        await users.SetLockoutEndDateAsync(user, null);
        await sessions.RevokeAllAsync(user.Id, SessionRevocationReasons.PasswordReset, cancellationToken);
        await securityEvents.WriteAsync(SecurityEventType.PasswordResetCompleted, user.Id, client, cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, Guid? currentSessionId, ChangePasswordRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await users.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return Result.Failure(Error.Unauthorized(ErrorCodes.Common.Unauthorized));
        }

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
            {
                await users.AccessFailedAsync(user);
                await securityEvents.WriteAsync(SecurityEventType.PasswordChangeFailed, user.Id, client, cancellationToken);

                return Result.Failure(Error.Validation(ErrorCodes.Auth.CurrentPasswordInvalid));
            }

            return Result.Failure(PasswordErrors.ToValidationError(result, "newPassword"));
        }

        await sessions.RevokeAllAsync(user.Id, SessionRevocationReasons.PasswordChanged, cancellationToken, currentSessionId);
        await securityEvents.WriteAsync(SecurityEventType.PasswordChanged, user.Id, client, cancellationToken);

        return Result.Success();
    }

    public async Task<Result<ProfileDto>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await users.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return Error.Unauthorized(ErrorCodes.Common.Unauthorized);
        }

        user.FullName = request.FullName.Trim();
        user.Locale = request.Locale ?? user.Locale;

        await users.UpdateAsync(user);
        await securityEvents.WriteAsync(SecurityEventType.ProfileUpdated, user.Id, client, cancellationToken);

        return await GetProfileAsync(userId, cancellationToken);
    }

    public async Task<Result> RequestEmailChangeAsync(Guid userId, ChangeEmailRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await users.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return Result.Failure(Error.Unauthorized(ErrorCodes.Common.Unauthorized));
        }

        // A stolen session alone must not be enough to take over the account.
        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            await securityEvents.WriteAsync(SecurityEventType.EmailChangeFailed, user.Id, client, cancellationToken);

            return Result.Failure(Error.Validation(ErrorCodes.Auth.CurrentPasswordInvalid));
        }

        var taken = await users.FindByEmailAsync(request.NewEmail);

        if (taken is not null)
        {
            // Its owner hears about it, and the person asking gets the same answer as for a free address.
            emailDispatcher.TryEnqueue(emails.ExistingAccountNotice(taken));
        }
        else
        {
            var token = await users.GenerateChangeEmailTokenAsync(user, request.NewEmail);

            emailDispatcher.TryEnqueue(emails.ConfirmEmailChange(user, request.NewEmail, EncodeToken(token)));
        }

        await securityEvents.WriteAsync(SecurityEventType.EmailChangeRequested, user.Id, client, cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ConfirmEmailChangeAsync(ConfirmEmailChangeRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await users.FindByIdAsync(request.UserId.ToString());
        var token = DecodeToken(request.Token);

        if (user is null || token is null || user.Email is not { } oldEmail)
        {
            return Result.Failure(InvalidToken);
        }

        // Someone may have registered the address since the link was sent.
        if (await users.FindByEmailAsync(request.NewEmail) is not null)
        {
            return Result.Failure(InvalidToken);
        }

        var changed = await users.ChangeEmailAsync(user, request.NewEmail, token);

        if (!changed.Succeeded)
        {
            return Result.Failure(InvalidToken);
        }

        await users.SetUserNameAsync(user, request.NewEmail);

        // The sign-in name changed, so every device signs in again, and the old address is told.
        await sessions.RevokeAllAsync(user.Id, SessionRevocationReasons.EmailChanged, cancellationToken);
        await securityEvents.WriteAsync(SecurityEventType.EmailChanged, user.Id, client, cancellationToken);
        emailDispatcher.TryEnqueue(AuthEmailComposer.EmailChangedNotice(user, oldEmail));

        return Result.Success();
    }

    public async Task<Result<ProfileDto>> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return Error.Unauthorized(ErrorCodes.Common.Unauthorized);
        }

        var roles = await users.GetRolesAsync(user);

        return new ProfileDto(
            user.Id,
            user.Email!,
            user.EmailConfirmed,
            user.PhoneNumber,
            user.PhoneNumberConfirmed,
            user.FullName,
            user.Locale,
            user.TwoFactorEnabled,
            [.. roles]);
    }

    private static string EncodeToken(string token) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static string? DecodeToken(string encoded)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encoded));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static bool IsInvalidToken(IdentityResult result) =>
        result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.InvalidToken));

    private async Task<Error?> ValidatePasswordAsync(ApplicationUser user, string password, string field)
    {
        var failures = new List<IdentityError>();

        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, user, password);
            failures.AddRange(result.Errors);
        }

        return failures.Count == 0
            ? null
            : PasswordErrors.ToValidationError(IdentityResult.Failed([.. failures]), field);
    }

    private async Task QueueConfirmationAsync(ApplicationUser user)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        emailDispatcher.TryEnqueue(emails.ConfirmEmail(user, EncodeToken(token)));
    }
}
