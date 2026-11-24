using System.ComponentModel.DataAnnotations;

namespace Kunai.Web.Client.Pages.Account;

/// <summary>Form models with the same rules as the API, so most mistakes are caught before a request.</summary>
public sealed class LoginForm
{
    [Required(ErrorMessage = "Kullanıcı adı ya da e-posta gerekli.")]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre gerekli.")]
    public string Password { get; set; } = string.Empty;
}
