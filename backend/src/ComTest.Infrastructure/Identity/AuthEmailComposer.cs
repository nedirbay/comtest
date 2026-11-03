using ComTest.Infrastructure.Messaging;
using Microsoft.Extensions.Options;

namespace ComTest.Infrastructure.Identity;

/// <summary>
/// Builds account emails as plain text, which leaves no HTML to inject into. Tokens go in the
/// URL fragment, which browsers do not send to servers or put in Referer headers.
/// </summary>
public sealed class AuthEmailComposer(IOptions<AuthOptions> options)
{
    private static readonly Dictionary<string, EmailText> Confirm = new(StringComparer.Ordinal)
    {
        ["tk"] = new("E-poçtaňyzy tassyklaň", "Hasabyňyzy işjeňleşdirmek üçin şu salgyny açyň (24 sagat işleýär):"),
        ["ru"] = new("Подтвердите e-mail", "Чтобы активировать аккаунт, откройте ссылку (действует 24 часа):"),
        ["en"] = new("Confirm your email", "Open this link to activate your account (valid for 24 hours):"),
    };

    private static readonly Dictionary<string, EmailText> Reset = new(StringComparer.Ordinal)
    {
        ["tk"] = new("Açar sözüni dikeltmek", "Täze açar sözi bellemek üçin şu salgyny açyň (30 minut işleýär). Muny siz haýyş etmedik bolsaňyz, bu haty ähmiýetsiz goýuň:"),
        ["ru"] = new("Восстановление пароля", "Чтобы задать новый пароль, откройте ссылку (действует 30 минут). Если вы этого не запрашивали, просто проигнорируйте письмо:"),
        ["en"] = new("Reset your password", "Open this link to set a new password (valid for 30 minutes). If you did not ask for this, you can ignore this email:"),
    };

    private static readonly Dictionary<string, EmailText> AccountExists = new(StringComparer.Ordinal)
    {
        ["tk"] = new("Hasabyňyz eýýäm bar", "Bu e-poçta bilen täzeden hasaba alynmaga synanyşyk boldy. Bu siz bolsaňyz, ulgama giriň ýa-da açar sözüni dikeldiň:"),
        ["ru"] = new("У вас уже есть аккаунт", "Кто-то пытался зарегистрироваться с этим e-mail. Если это были вы, войдите или восстановите пароль:"),
        ["en"] = new("You already have an account", "Someone tried to register with this email. If that was you, sign in or reset your password:"),
    };

    public EmailMessage ConfirmEmail(ApplicationUser user, string token)
    {
        ArgumentNullException.ThrowIfNull(user);

        return Compose(user, Confirm, $"confirm-email#userId={user.Id:D}&token={Uri.EscapeDataString(token)}");
    }

    public EmailMessage ResetPassword(ApplicationUser user, string token)
    {
        ArgumentNullException.ThrowIfNull(user);

        return Compose(user, Reset, $"reset-password#userId={user.Id:D}&token={Uri.EscapeDataString(token)}");
    }

    public EmailMessage ExistingAccountNotice(ApplicationUser user) => Compose(user, AccountExists, "login");

    /// <summary>The invitee may have no account yet, so all three languages are included.</summary>
    public EmailMessage StaffInvitation(string email, string tenantName, string token)
    {
        var link = $"{options.Value.FrontendBaseUrl.TrimEnd('/')}/accept-invitation#token={Uri.EscapeDataString(token)}";

        return new EmailMessage(
            email,
            "ComTest",
            $"""
            "{tenantName}" sizi işgär hökmünde çagyrýar. Kabul etmek üçin şu salgyny açyň (7 gün işleýär):
            "{tenantName}" приглашает вас как сотрудника. Чтобы принять, откройте ссылку (действует 7 дней):
            "{tenantName}" has invited you as staff. Open this link to accept (valid for 7 days):

            {link}
            """);
    }

    private EmailMessage Compose(ApplicationUser user, Dictionary<string, EmailText> texts, string path)
    {
        var text = texts.GetValueOrDefault(user.Locale) ?? texts["tk"];
        var baseUrl = options.Value.FrontendBaseUrl.TrimEnd('/');

        return new EmailMessage(user.Email!, text.Subject, $"{text.Body}\n\n{baseUrl}/{path}\n");
    }

    private sealed record EmailText(string Subject, string Body);
}
