using System.ComponentModel.DataAnnotations;
using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Dtos;

/// <summary>Dane specyficzne dla InstagramPost - zapis i odczyt.</summary>
public class InstagramPostDetailsDto
{
    [Required, StringLength(100, MinimumLength = 1)] public string InstagramAccountId { get; set; } = "";

    [Required] public InstagramMediaType? MediaType { get; set; }

    /// <summary>Oznaczeni użytkownicy; maksymalnie 30 unikalnych wpisów.</summary>
    public List<string> UserTags { get; set; } = new();

    [StringLength(100)] public string? LocationId { get; set; }

    public bool ShareToFeed { get; set; } = true;
}
