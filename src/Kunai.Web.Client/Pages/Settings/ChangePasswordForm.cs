using System.ComponentModel.DataAnnotations;

namespace Kunai.Web.Client.Pages.Settings;

/// <summary>POST /me/password, with the API's length rule and a repeat field.</summary>
public sealed class ChangePasswordForm
{
    [Required(ErrorMessage = "Mevcut şifren gerekli.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni şifre gerekli.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "Şifre 12-128 karakter olmalı.")]
    public string NewPassword { get; set; } = string.Empty;

    [Compare(nameof(NewPassword), ErrorMessage = "Şifreler aynı değil.")]
    public string NewPasswordAgain { get; set; } = string.Empty;
}
