using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Dtos;

/// <summary>Konto społecznościowe. Nigdy nie zawiera tokenów (AccessToken, RefreshToken).</summary>
/// <param name="HasRefreshToken">Czy dla konta zapisano token odświeżania.</param>
/// <param name="Pictures">Zdjęcia z postów kierowanych na to konto.</param>
/// <param name="Videos">Wideo z postów kierowanych na to konto.</param>
public record SocialAccountDto(
    Guid Id,
    string AccountId,
    SocialPlatform Platform,
    string ExternalPlatformId,
    string AccountName,
    DateTime TokenExpiresAt,
    bool IsActive,
    string? ErrorMessage,
    bool HasRefreshToken,
    List<PictureDto> Pictures,
    List<VideoDto> Videos,
    List<ManagerUserDto> ManagerUsers);
