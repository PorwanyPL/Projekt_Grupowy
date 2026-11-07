using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Dtos;

/// <summary>Post wraz z mediami, managerami, statusem publikacji i (dla postów platform) danymi platformy.</summary>
/// <param name="Platform">Rodzaj posta; null dla posta ogólnego.</param>
/// <param name="CanEdit">Czy zalogowany użytkownik może teraz edytować post (uprawnienia do konta i status; nie uwzględnia blokady edycji przez inną osobę).</param>
/// <param name="CanDelete">Czy zalogowany użytkownik może usunąć post.</param>
/// <param name="Lock">Aktywna blokada edycji (kto edytuje); null, gdy nikt nie edytuje.</param>
/// <param name="ExternalPostId">Identyfikator posta po stronie platformy, ustawiany po publikacji.</param>
public record PostDto(
    Guid Id,
    string Title,
    DateTime CreatedAt,
    string Description,
    FacebookPostType Visibility,
    List<PictureDto> Pictures,
    List<VideoDto> Videos,
    List<ManagerUserDto> ManagerUsers,
    PostStatus Status,
    SocialPlatform? Platform,
    DateTime? ScheduledAt,
    DateTime? PublishedAt,
    string? ExternalPostId,
    string? ErrorMessage,
    Guid? TargetAccountId,
    FacebookPostDetailsDto? Facebook,
    InstagramPostDetailsDto? Instagram,
    TikTokPostDetailsDto? TikTok,
    bool CanEdit = false,
    bool CanDelete = false,
    PostLockDto? Lock = null);
