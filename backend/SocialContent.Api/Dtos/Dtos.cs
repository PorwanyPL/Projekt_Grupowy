using System.ComponentModel.DataAnnotations;
using SocialContent.Api.Domain;

namespace SocialContent.Api.Dtos;

// ---------- ManagerUser ----------

/// <summary>Dane managera przy tworzeniu i pełnej aktualizacji (wszystkie pola wymagane).</summary>
public class ManagerUserWriteDto
{
    /// <summary>Imię: 1-50 znaków, litery, myślnik lub apostrof.</summary>
    [Required, StringLength(50, MinimumLength = 1)] public string FirstName { get; set; } = "";

    /// <summary>Nazwisko: 1-50 znaków, litery, myślnik lub apostrof.</summary>
    [Required, StringLength(50, MinimumLength = 1)] public string LastName { get; set; } = "";

    /// <summary>Wiek w latach, od 18 do 100.</summary>
    [Range(18, 100)] public int Age { get; set; }

    /// <summary>Opis managera, 1-2000 znaków.</summary>
    [Required, StringLength(2000, MinimumLength = 1)] public string Description { get; set; } = "";
}

/// <summary>Manager (ManagerUser). Nie zawiera adresu e-mail ani żadnych danych logowania.</summary>
/// <param name="SocialAccountId">Konto społecznościowe, do którego manager jest przypisany; null, gdy do żadnego.</param>
/// <param name="HasLoginAccount">Czy manager ma konto logowania. Wypełniane tylko przez endpointy /api/ManagerUsers; w managerach zagnieżdżonych w postach i kontach zawsze null.</param>
/// <param name="IsAccessBlocked">Czy dostęp managera jest zablokowany. Tak samo jak HasLoginAccount: null w obiektach zagnieżdżonych.</param>
public record ManagerUserDto(
    Guid Id,
    string FirstName,
    string LastName,
    int Age,
    string Description,
    Guid? SocialAccountId,
    bool? HasLoginAccount = null,
    bool? IsAccessBlocked = null);

/// <summary>Żądanie zablokowania lub odblokowania dostępu managera.</summary>
public class ManagerAccessDto
{
    /// <summary>true = zablokuj dostęp, false = odblokuj.</summary>
    [Required] public bool? Blocked { get; set; }
}

// ---------- Media ----------

/// <summary>Metadane zdjęcia dodawanego do posta (sam plik nie jest przesyłany).</summary>
public class PictureWriteDto
{
    [Required, StringLength(150, MinimumLength = 1)] public string FileName { get; set; } = "";

    /// <summary>Dozwolone dla zdjęć: Png, Jpeg, Jpg, Gif, Webp.</summary>
    public Format Format { get; set; }

    /// <summary>Rozmiar w kilobajtach, co najmniej 1.</summary>
    [Range(1, int.MaxValue)] public int SizeKb { get; set; }
}

/// <summary>Metadane wideo dodawanego do posta (sam plik nie jest przesyłany).</summary>
public class VideoWriteDto
{
    [Required, StringLength(150, MinimumLength = 1)] public string FileName { get; set; } = "";

    /// <summary>Dozwolone dla wideo: Mp4, Mp3, Flac.</summary>
    public Format Format { get; set; }

    /// <summary>Czas trwania w sekundach, co najmniej 1.</summary>
    [Range(1, int.MaxValue)] public int Length { get; set; }
}

public record PictureDto(Guid Id, string FileName, Format Format, int SizeKb);
public record VideoDto(Guid Id, string FileName, Format Format, int Length);

// ---------- Platformy (podklasy Post) ----------

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

// ---------- Post ----------

/// <summary>Dane posta przy tworzeniu i pełnej aktualizacji.</summary>
public class PostWriteDto
{
    [Required, StringLength(120, MinimumLength = 1)] public string Title { get; set; } = "";
    [Required, StringLength(2000, MinimumLength = 1)] public string Description { get; set; } = "";

    /// <summary>Typ treści: Text (bez mediów), Photo (min. 1 zdjęcie, bez wideo), Video (min. 1 wideo).</summary>
    public FacebookPostType Visibility { get; set; }

    public List<PictureWriteDto> Pictures { get; set; } = new();
    public List<VideoWriteDto> Videos { get; set; } = new();
    public List<Guid> ManagerUserIds { get; set; } = new();

    /// <summary>Planowany termin publikacji (UTC, w przyszłości). Brak = szkic (Draft), podanie = Scheduled.</summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>Konto docelowe (SocialAccount); dla posta platformy musi być na tej samej platformie.</summary>
    public Guid? TargetAccountId { get; set; }

    /// <summary>Wypełnij dokładnie jeden z bloków platformy, aby utworzyć post tej platformy; żaden = post ogólny.</summary>
    public FacebookPostDetailsDto? Facebook { get; set; }
    public InstagramPostDetailsDto? Instagram { get; set; }
    public TikTokPostDetailsDto? TikTok { get; set; }
}

/// <summary>Post wraz z mediami, managerami, statusem publikacji i (dla postów platform) danymi platformy.</summary>
/// <param name="Platform">Rodzaj posta; null dla posta ogólnego.</param>
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
    TikTokPostDetailsDto? TikTok);

// ---------- SocialAccount ----------

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

// ---------- Uwierzytelnianie ----------

/// <summary>Rejestracja: dane managera (jak w ManagerUserWriteDto) oraz dane logowania.</summary>
public class RegisterDto : ManagerUserWriteDto
{
    /// <summary>Adres e-mail będący identyfikatorem logowania; unikalny, wielkość liter nie ma znaczenia.</summary>
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";

    /// <summary>Hasło: 8-128 znaków, co najmniej jedna mała litera, wielka litera i cyfra.</summary>
    [Required, StringLength(128, MinimumLength = 8)] public string Password { get; set; } = "";
}

/// <summary>Dane logowania.</summary>
public class LoginDto
{
    [Required, StringLength(254)] public string Email { get; set; } = "";
    [Required, StringLength(128)] public string Password { get; set; } = "";
}

/// <summary>Zalogowany manager wraz z adresem e-mail (widocznym tylko dla niego samego).</summary>
public record ManagerProfileDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    int Age,
    string Description,
    Guid? SocialAccountId);

/// <summary>Wynik rejestracji lub logowania.</summary>
/// <param name="AccessToken">Token JWT; przekazuj w nagłówku Authorization: Bearer.</param>
/// <param name="TokenType">Zawsze "Bearer".</param>
/// <param name="ExpiresAt">Moment wygaśnięcia tokenu (UTC).</param>
public record AuthResponseDto(string AccessToken, string TokenType, DateTime ExpiresAt, ManagerProfileDto Manager);
