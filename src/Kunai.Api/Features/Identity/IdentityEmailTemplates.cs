using System.Net;

namespace Kunai.Api.Features.Identity;

/// <summary>Email texts in Turkish and English, chosen by the user's locale.</summary>
internal static class IdentityEmailTemplates
{
    public static (string Subject, string Html, string Text) ConfirmEmail(string locale, string displayName, string link) =>
        IsEnglish(locale)
            ? Build(
                "Confirm your Kunai email",
                $"Hi {displayName},",
                "Confirm your email address to start filling your shelf.",
                "Confirm email",
                link,
                "If you did not create a Kunai account, you can ignore this email.")
            : Build(
                "Kunai e-posta adresini onayla",
                $"Merhaba {displayName},",
                "Rafını doldurmaya başlamak için e-posta adresini onayla.",
                "E-postamı onayla",
                link,
                "Kunai hesabı açmadıysan bu e-postayı görmezden gelebilirsin.");

    public static (string Subject, string Html, string Text) AccountExists(string locale, string link) =>
        IsEnglish(locale)
            ? Build(
                "You already have a Kunai account",
                "Hi,",
                "Someone tried to sign up with this email address, but it already has a Kunai account. If it was you, sign in or reset your password.",
                "Reset password",
                link,
                "If it was not you, no action is needed.")
            : Build(
                "Zaten bir Kunai hesabın var",
                "Merhaba,",
                "Bu e-posta adresiyle kayıt olunmaya çalışıldı ama adresin zaten bir Kunai hesabı var. Sen yaptıysan giriş yap ya da şifreni sıfırla.",
                "Şifremi sıfırla",
                link,
                "Sen değilsen bir şey yapmana gerek yok.");

    public static (string Subject, string Html, string Text) PasswordReset(string locale, string link) =>
        IsEnglish(locale)
            ? Build(
                "Reset your Kunai password",
                "Hi,",
                "Use the button below to choose a new password. The link is valid for one hour.",
                "Choose a new password",
                link,
                "If you did not ask for this, you can ignore this email; your password stays the same.")
            : Build(
                "Kunai şifreni sıfırla",
                "Merhaba,",
                "Yeni bir şifre belirlemek için aşağıdaki butonu kullan. Bağlantı bir saat geçerli.",
                "Yeni şifre belirle",
                link,
                "Bunu sen istemediysen e-postayı görmezden gelebilirsin; şifren değişmez.");

    private static bool IsEnglish(string locale) => string.Equals(locale, "en", StringComparison.Ordinal);

    private static (string Subject, string Html, string Text) Build(
        string subject, string greeting, string body, string action, string link, string footer)
    {
        var html = $"""
            <p>{WebUtility.HtmlEncode(greeting)}</p>
            <p>{WebUtility.HtmlEncode(body)}</p>
            <p><a href="{WebUtility.HtmlEncode(link)}">{WebUtility.HtmlEncode(action)}</a></p>
            <p style="color:#666">{WebUtility.HtmlEncode(footer)}</p>
            """;
        var text = $"{greeting}\n\n{body}\n\n{action}: {link}\n\n{footer}\n";
        return (subject, html, text);
    }
}
