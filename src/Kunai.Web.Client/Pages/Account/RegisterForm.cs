using System.ComponentModel.DataAnnotations;

namespace Kunai.Web.Client.Pages.Account;

public sealed class RegisterForm
{
    [Required(ErrorMessage = "E-posta gerekli.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi gir.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kullanıcı adı gerekli.")]
    [RegularExpression("^[a-z][a-z0-9_]{2,23}$", ErrorMessage = "3-24 karakter: küçük harf, rakam ve alt çizgi; harfle başlamalı.")]
    public string Username { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "En fazla 50 karakter.")]
    public string? DisplayName { get; set; }

    [Required(ErrorMessage = "Şifre gerekli.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "Şifre 12-128 karakter olmalı.")]
    public string Password { get; set; } = string.Empty;

    [Compare(nameof(Password), ErrorMessage = "Şifreler aynı değil.")]
    public string PasswordAgain { get; set; } = string.Empty;
}
