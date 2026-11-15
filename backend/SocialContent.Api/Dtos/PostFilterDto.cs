using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Dtos;

/// <summary>
/// Filtry listy postów (parametry zapytania GET /api/Posts). Pola różnych kategorii łączą się koniunkcją (I),
/// wartości w obrębie jednej listy - alternatywą (LUB). Filtry działają na logicznych postach (grupach wariantów)
/// i zawsze w zakresie postów widocznych dla zalogowanej roli.
/// </summary>
public class PostFilterDto
{
    /// <summary>Typ treści (visibility): Text, Photo, Video.</summary>
    public FacebookPostType? Type { get; set; }

    /// <summary>Post ma wariant na tym koncie.</summary>
    public Guid? AccountId { get; set; }

    /// <summary>Przypisana osoba (autor/współautor): id managera.</summary>
    public Guid? ManagerId { get; set; }

    /// <summary>Status zbiorczy posta; można podać kilka (status=Draft&amp;status=Failed).</summary>
    public List<PostStatus> Status { get; set; } = new();

    /// <summary>Tagi treści: post ma którykolwiek z podanych (bez względu na wielkość liter).</summary>
    public List<string> Tag { get; set; } = new();

    /// <summary>Profile (konta): post ma wariant na którymkolwiek z podanych kont.</summary>
    public List<Guid> AccountIds { get; set; } = new();

    /// <summary>Platformy: post ma wariant na którejkolwiek z podanych platform.</summary>
    public List<SocialPlatform> Platform { get; set; } = new();

    /// <summary>Początek zakresu dat (UTC, włącznie) liczony po dacie publikacji posta (termin, a po publikacji - czas publikacji; dla szkiców - data utworzenia).</summary>
    public DateTime? From { get; set; }

    /// <summary>Koniec zakresu dat (UTC, włącznie).</summary>
    public DateTime? To { get; set; }
}
