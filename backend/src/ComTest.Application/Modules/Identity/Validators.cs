using ComTest.Application.Modules.Identity.Contracts;
using FluentValidation;

namespace ComTest.Application.Modules.Identity;

internal static class IdentityRules
{
    public const int MaxEmailLength = 254;

    // Bounded because hashing cost grows with input length, which an attacker could exploit.
    public const int MaxPasswordLength = 128;

    public const int MaxTokenLength = 2048;

    public const string PhonePattern = @"^\+[1-9][0-9]{7,14}$";

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(MaxEmailLength).EmailAddress();

    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(MaxPasswordLength);

    public static IRuleBuilderOptions<T, string> ValidToken<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(MaxTokenLength);

    public static IRuleBuilderOptions<T, string> ValidOtp<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Matches("^[0-9]{6}$");
}

internal sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    private static readonly string[] Locales = ["tk", "ru", "en"];

    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Password).ValidPassword();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Locale).Must(locale => locale is null || Locales.Contains(locale));
    }
}

internal sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    private static readonly string[] Locales = ["tk", "ru", "en"];

    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Locale).Must(locale => locale is null || Locales.Contains(locale));
    }
}

internal sealed class ChangeEmailRequestValidator : AbstractValidator<ChangeEmailRequest>
{
    public ChangeEmailRequestValidator()
    {
        RuleFor(x => x.Password).ValidPassword();
        RuleFor(x => x.NewEmail).ValidEmail();
    }
}

internal sealed class ConfirmEmailChangeRequestValidator : AbstractValidator<ConfirmEmailChangeRequest>
{
    public ConfirmEmailChangeRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.NewEmail).ValidEmail();
        RuleFor(x => x.Token).ValidToken();
    }
}

internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Login).NotEmpty().MaximumLength(IdentityRules.MaxEmailLength);
        RuleFor(x => x.Password).ValidPassword();
    }
}

internal sealed class TwoFactorLoginRequestValidator : AbstractValidator<TwoFactorLoginRequest>
{
    public TwoFactorLoginRequestValidator()
    {
        RuleFor(x => x.ChallengeToken).ValidToken();
        RuleFor(x => x.Code).Matches("^[0-9]{6}$").When(x => x.Code is not null);
        RuleFor(x => x.RecoveryCode).MaximumLength(32).When(x => x.RecoveryCode is not null);
        RuleFor(x => x).Must(x => x.Code is not null ^ x.RecoveryCode is not null).WithName("code");
    }
}

internal sealed class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Token).ValidToken();
    }
}

internal sealed class EmailRequestValidator : AbstractValidator<EmailRequest>
{
    public EmailRequestValidator()
    {
        RuleFor(x => x.Email).ValidEmail();
    }
}

internal sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Token).ValidToken();
        RuleFor(x => x.NewPassword).ValidPassword();
    }
}

internal sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).ValidPassword();
        RuleFor(x => x.NewPassword).ValidPassword();
    }
}

internal sealed class TwoFactorCodeRequestValidator : AbstractValidator<TwoFactorCodeRequest>
{
    public TwoFactorCodeRequestValidator()
    {
        RuleFor(x => x.Code).ValidOtp();
    }
}

internal sealed class DisableTwoFactorRequestValidator : AbstractValidator<DisableTwoFactorRequest>
{
    public DisableTwoFactorRequestValidator()
    {
        RuleFor(x => x.Password).ValidPassword();
        RuleFor(x => x.Code).ValidOtp();
    }
}

internal sealed class SendPhoneCodeRequestValidator : AbstractValidator<SendPhoneCodeRequest>
{
    public SendPhoneCodeRequestValidator()
    {
        RuleFor(x => x.PhoneNumber).NotEmpty().Matches(IdentityRules.PhonePattern);
    }
}

internal sealed class VerifyPhoneRequestValidator : AbstractValidator<VerifyPhoneRequest>
{
    public VerifyPhoneRequestValidator()
    {
        RuleFor(x => x.Code).ValidOtp();
    }
}
