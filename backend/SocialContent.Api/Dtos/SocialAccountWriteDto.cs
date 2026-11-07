using System.ComponentModel.DataAnnotations;
using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Dtos;

/// <summary>Dane konta społecznościowego przy tworzeniu i pełnej aktualizacji.</summary>
public class SocialAccountWriteDto
{
    /// <summary>Identyfikator konta w systemie, unikalny w obrębie platformy.</summary>
    [Required, StringLength(100, MinimumLength = 1)] public string AccountId { get; set; } = "";

    [Required] public SocialPlatform? Platform { get; set; }

    /// <summary>Identyfikator konta po stronie platformy.</summary>
    [Required, StringLength(100, MinimumLength = 1)] public string ExternalPlatformId { get; set; } = "";

    [Required, StringLength(150, MinimumLength = 1)] public string AccountName { get; set; } = "";

    /// <summary>Token dostępu. Wymagany przy tworzeniu; przy aktualizacji pominięcie oznacza brak zmiany.</summary>
    [StringLength(2000)] public string? AccessToken { get; set; }

    /// <summary>Token odświeżania. Przy aktualizacji pominięcie oznacza brak zmiany.</summary>
    [StringLength(2000)] public string? RefreshToken { get; set; }

    /// <summary>Data wygaśnięcia tokenu dostępu (UTC).</summary>
    [Required] public DateTime? TokenExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Managerowie konta. Pełna lista: pominięci zostaną odpięci od konta.</summary>
    public List<Guid> ManagerUserIds { get; set; } = new();
}
