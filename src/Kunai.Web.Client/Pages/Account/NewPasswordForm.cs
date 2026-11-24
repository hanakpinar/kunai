using System.ComponentModel.DataAnnotations;

namespace Kunai.Web.Client.Pages.Account;

public sealed class NewPasswordForm
{
    [Required(ErrorMessage = "Şifre gerekli.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "Şifre 12-128 karakter olmalı.")]
    public string Password { get; set; } = string.Empty;

    [Compare(nameof(Password), ErrorMessage = "Şifreler aynı değil.")]
    public string PasswordAgain { get; set; } = string.Empty;
}
