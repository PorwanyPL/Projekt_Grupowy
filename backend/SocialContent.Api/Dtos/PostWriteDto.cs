using System.ComponentModel.DataAnnotations;
using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Dtos;

/// <summary>Dane posta przy tworzeniu i pełnej aktualizacji.</summary>
public class PostWriteDto
{
    [Required, StringLength(120, MinimumLength = 1)] public string Title { get; set; } = "";
    [Required, StringLength(2000, MinimumLength = 1)] public string Description { get; set; } = "";

    /// <summary>Typ treści: Text (bez mediów), Photo (min. 1 zdjęcie, bez wideo), Video (min. 1 wideo).</summary>
    public FacebookPostType Visibility { get; set; }

    public List<PictureWriteDto> Pictures { get; set; } = new();
    public List<VideoWriteDto> Videos { get; set; } = new();
    /// <summary>Planowany termin publikacji (UTC, w przyszłości) -> status Scheduled. Brak (i PublishNow = false) = szkic (Draft).</summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>true = opublikuj od razu po zapisie. Wykluczone z scheduledAt. Wynik widać w polu status (Published albo Failed).</summary>
    public bool PublishNow { get; set; }

    /// <summary>Konto docelowe (SocialAccount); manager musi być do niego przypisany. Dla jednego konta; przy wielu kontach użyj <see cref="TargetAccountIds"/> (można podać oba pola - listy są łączone).</summary>
    public Guid? TargetAccountId { get; set; }

    /// <summary>Konta docelowe: jeden post przeznaczony na kilka platform (wspólna grupa wariantów). Wymagane jest co najmniej jedno konto (tu albo w targetAccountId).</summary>
    public List<Guid> TargetAccountIds { get; set; } = new();

    /// <summary>Tagi treści (maks. 10, każdy do 30 znaków) - służą do filtrowania postów.</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>Bloki platform: dla jednego konta wypełnij dokładnie jeden blok (post tej platformy) albo żaden (post ogólny). Przy wielu kontach blok dotyczy kont tej platformy, a pozostałe konta dostają posty ogólne.</summary>
    public FacebookPostDetailsDto? Facebook { get; set; }
    public InstagramPostDetailsDto? Instagram { get; set; }
    public TikTokPostDetailsDto? TikTok { get; set; }
}
