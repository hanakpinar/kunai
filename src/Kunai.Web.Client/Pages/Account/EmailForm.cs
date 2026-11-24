using System.ComponentModel.DataAnnotations;

namespace Kunai.Web.Client.Pages.Account;

public sealed class EmailForm
{
    [Required(ErrorMessage = "E-posta gerekli.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi gir.")]
    public string Email { get; set; } = string.Empty;
}
