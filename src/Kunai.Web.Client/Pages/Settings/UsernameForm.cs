using System.ComponentModel.DataAnnotations;

namespace Kunai.Web.Client.Pages.Settings;

/// <summary>PUT /me/username, with the same pattern as registration.</summary>
public sealed class UsernameForm
{
    [Required(ErrorMessage = "Kullanıcı adı gerekli.")]
    [RegularExpression("^[a-z][a-z0-9_]{2,23}$", ErrorMessage = "3-24 karakter: küçük harf, rakam ve alt çizgi; harfle başlamalı.")]
    public string Username { get; set; } = string.Empty;
}
