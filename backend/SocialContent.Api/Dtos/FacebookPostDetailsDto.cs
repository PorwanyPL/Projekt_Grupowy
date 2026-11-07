using System.ComponentModel.DataAnnotations;
using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Dtos;

/// <summary>Dane specyficzne dla FacebookPost - zapis i odczyt.</summary>
public class FacebookPostDetailsDto
{
    /// <summary>Identyfikator strony Facebooka, na której post ma się ukazać.</summary>
    [Required, StringLength(100, MinimumLength = 1)] public string TargetPageId { get; set; } = "";

    /// <summary>Opcjonalny link dołączany do posta (bezwzględny adres http lub https).</summary>
    [StringLength(2000)] public string? LinkUrl { get; set; }

    [StringLength(100)] public string? PrivacySetting { get; set; }

    [StringLength(500)] public string? FeedTargeting { get; set; }

    /// <summary>Typ posta na Facebooku; gdy pominięty, przyjmowana jest wartość pola visibility.</summary>
    public FacebookPostType? PostType { get; set; }
}
