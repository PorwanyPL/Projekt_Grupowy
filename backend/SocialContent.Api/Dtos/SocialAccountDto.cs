using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Dtos;

/// <summary>Konto społecznościowe. Nigdy nie zawiera tokenów (AccessToken, RefreshToken).</summary>
/// <param name="HasRefreshToken">Czy dla konta zapisano token odświeżania.</param>
/// <param name="Pictures">Zdjęcia z postów kierowanych na to konto.</param>
/// <param name="Videos">Wideo z postów kierowanych na to konto.</param>
/// <param name="OwnerName">Klient-właściciel: dla firmy i managera jego pseudonim nadany przez firmę (albo imię i nazwisko), dla klienta jego własne imię i nazwisko.</param>
/// <param name="ManagementStatus">Stan zaproszenia firmy (Invited, Accepted, Declined).</param>
/// <param name="ApprovalMode">Kto akceptuje posty tego konta (ustawienie klienta dla firmy zarządzającej).</param>
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
    List<ManagerUserDto> ManagerUsers,
    Guid OwnerClientId = default,
    string OwnerName = "",
    Guid CompanyId = default,
    string CompanyName = "",
    ManagementStatus ManagementStatus = ManagementStatus.Invited,
    PostApprovalMode ApprovalMode = PostApprovalMode.Manager);
