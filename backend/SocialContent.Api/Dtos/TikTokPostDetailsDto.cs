using System.ComponentModel.DataAnnotations;
using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Dtos;

/// <summary>Dane specyficzne dla TikTokPost - zapis i odczyt.</summary>
public class TikTokPostDetailsDto
{
    [Required, StringLength(100, MinimumLength = 1)] public string TikTokAccountId { get; set; } = "";

    [Required] public TikTokPrivacyLevel? PrivacyLevel { get; set; }

    public bool DisableDuet { get; set; }
    public bool DisableStitch { get; set; }
    public bool DisableComment { get; set; }
    public bool BrandContentToggle { get; set; }
    public bool AutoAddMusic { get; set; }
}
