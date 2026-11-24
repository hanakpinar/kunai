using System.ComponentModel.DataAnnotations;

namespace Kunai.Web.Client.Pages.Settings;

/// <summary>PUT /me/profile, with the API's limits.</summary>
public sealed class ProfileForm
{
    public const int DisplayNameMaxLength = 50;
    public const int BioMaxLength = 300;

    [Required(ErrorMessage = "Görünen ad gerekli.")]
    [StringLength(DisplayNameMaxLength, ErrorMessage = "Görünen ad en fazla 50 karakter olabilir.")]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(BioMaxLength, ErrorMessage = "Hakkında yazısı en fazla 300 karakter olabilir.")]
    public string? Bio { get; set; }
}
