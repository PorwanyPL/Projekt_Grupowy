using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Dtos;

/// <summary>Post wraz z mediami, managerami, statusem publikacji i (dla postów platform) danymi platformy.</summary>
/// <param name="Id">Identyfikator logicznego posta (GroupId): dla posta na wiele platform wspólny dla wszystkich wariantów.</param>
/// <param name="Status">Status zbiorczy: gdy warianty mają różne statusy, wygrywa najpilniejszy (Publishing, Failed, AwaitingApproval, Rejected, Scheduled, Draft, Published); statusy poszczególnych platform są w targets.</param>
/// <param name="Targets">Konta i platformy, na które post jest przeznaczony (jeden wpis na wariant).</param>
/// <param name="Tags">Tagi treści.</param>
/// <param name="CounterpartName">Dla firmy i managera: klient (pseudonim nadany przez firmę), dla klienta: firma, która obsługuje konto posta.</param>
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
    PostLockDto? Lock = null,
    bool CanApprove = false,
    List<string>? Tags = null,
    List<PostTargetDto>? Targets = null,
    string? CounterpartName = null);

/// <summary>Jedno konto/platforma posta (wariant w grupie).</summary>
public record PostTargetDto(
    Guid PostId,
    Guid? AccountId,
    string AccountName,
    SocialPlatform? Platform,
    PostStatus Status,
    string? ErrorMessage);
