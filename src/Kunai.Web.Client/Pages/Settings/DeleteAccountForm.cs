using System.ComponentModel.DataAnnotations;

namespace Kunai.Web.Client.Pages.Settings;

/// <summary>DELETE /me asks for the password again; the checkbox makes the user stop and read.</summary>
public sealed class DeleteAccountForm
{
    [Required(ErrorMessage = "Şifren gerekli.")]
    public string Password { get; set; } = string.Empty;

    [Range(typeof(bool), "true", "true", ErrorMessage = "Devam etmek için kutuyu işaretle.")]
    public bool Confirmed { get; set; }
}
