using System.Globalization;

namespace SocialContent.Api.Domain;

internal static class Rules
{
    public static string Name(string? value, string field)
    {
        if (!Validators.NotNull(value) || !Validators.NotEmpty(value) || !Validators.ValidNameElement(value))
            throw new DomainValidationException(
                $"{field} musi mieć 1-50 znaków i składać się z liter, '-' lub apostrofu.");
        return value!.Trim();
    }

    public static string Text(string? value, string field, int max)
    {
        if (!Validators.NotNull(value) || !Validators.NotWhiteSpace(value))
            throw new DomainValidationException($"{field} nie może być pusty.");
        var v = value!.Trim();
        if (v.Length > max)
            throw new DomainValidationException($"{field} może mieć maksymalnie {max} znaków.");
        return v;
    }

    /// <summary>Tekst opcjonalny: null lub same spacje dają null, w przeciwnym razie obowiązuje limit długości.</summary>
    public static string? OptionalText(string? value, string field, int max) =>
        Validators.NotWhiteSpace(value) ? Text(value, field, max) : null;

    /// <summary>Normalizuje datę do UTC (Unspecified traktowane jako UTC - tak są zapisywane w bazie).</summary>
    public static DateTime ToUtc(DateTime date) => date.Kind switch
    {
        DateTimeKind.Local => date.ToUniversalTime(),
        DateTimeKind.Utc => date,
        _ => DateTime.SpecifyKind(date, DateTimeKind.Utc)
    };

    public static DateTime ParseIsoDate(string? value)
    {
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date))
            throw new DomainValidationException("Data ma nieprawidłowy format (oczekiwano ISO 8601).");
        return date;
    }
}

public sealed record FirstName
{
    public string Value { get; }
    private FirstName(string value) => Value = value;
    public static FirstName Create(string? value) => new(Rules.Name(value, "Imię"));
    public override string ToString() => Value;
}

public sealed record LastName
{
    public string Value { get; }
    private LastName(string value) => Value = value;
    public static LastName Create(string? value) => new(Rules.Name(value, "Nazwisko"));
    public override string ToString() => Value;
}

public sealed record Age
{
    public int Value { get; }
    private Age(int value) => Value = value;
    public static Age Create(int value)
    {
        if (value is < 18 or > 100)
            throw new DomainValidationException("Wiek musi mieścić się w przedziale 18-100.");
        return new Age(value);
    }
}

public sealed record Description
{
    public string Value { get; }
    public int Length => Value.Length;
    private Description(string value) => Value = value;
    public static Description Create(string? value) => new(Rules.Text(value, "Opis", 2000));
    public override string ToString() => Value;
}

public sealed record Title
{
    public string Value { get; }
    private Title(string value) => Value = value;
    public static Title Create(string? value) => new(Rules.Text(value, "Tytuł", 120));
    public override string ToString() => Value;
}

public sealed record FileName
{
    private static readonly char[] Invalid = Path.GetInvalidFileNameChars();
    public string Value { get; }
    private FileName(string value) => Value = value;
    public static FileName Create(string? value)
    {
        var v = Rules.Text(value, "Nazwa pliku", 150);
        if (v.IndexOfAny(Invalid) >= 0)
            throw new DomainValidationException("Nazwa pliku zawiera niedozwolone znaki.");
        return new FileName(v);
    }
    public override string ToString() => Value;
}

public sealed record CreatedAt
{
    public DateTime Date { get; }
    private CreatedAt(DateTime date) => Date = date;

    public static CreatedAt Create(DateTime date)
    {
        // Data utworzenia nie może pochodzić z przyszłości (tolerancja 5 minut na różnice zegarów).
        if (!Validators.ValidDateFormat(date) || date > DateTime.UtcNow.AddMinutes(5))
            throw new DomainValidationException("Nieprawidłowa data utworzenia.");
        return new CreatedAt(date);
    }

    public static CreatedAt Create(string? value) => Create(Rules.ParseIsoDate(value));
}

/// <summary>Termin publikacji (ScheduledAt) lub faktyczny czas publikacji (PublishedAt). Zawsze w UTC.</summary>
public sealed record ScheduledTime
{
    public DateTime Date { get; }
    private ScheduledTime(DateTime date) => Date = date;

    public static ScheduledTime Create(DateTime date)
    {
        if (!Validators.ValidDateFormat(date))
            throw new DomainValidationException("Nieprawidłowa data (oczekiwano daty od roku 2000).");
        return new ScheduledTime(Rules.ToUtc(date));
    }

    public static ScheduledTime Create(string? value) => Create(Rules.ParseIsoDate(value));
}

/// <summary>
/// Identyfikator obiektu po stronie zewnętrznej platformy (strona Facebooka, konto Instagrama/TikToka, lokalizacja, post).
/// Diagram podaje atrybut "Date: DateTime", ale metody Create(string)/Read(): string wskazują na wartość tekstową
/// - przyjęto atrybut Value: string (patrz założenia).
/// </summary>
public sealed record ExternalId
{
    public string Value { get; }
    private ExternalId(string value) => Value = value;
    public static ExternalId Create(string? value)
    {
        var v = Rules.Text(value, "Identyfikator zewnętrzny", 100);
        if (v.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            throw new DomainValidationException("Identyfikator zewnętrzny nie może zawierać białych znaków.");
        return new ExternalId(v);
    }
    public override string ToString() => Value;
}

public sealed record WebUrl
{
    public string Url { get; }
    private WebUrl(string url) => Url = url;
    public static WebUrl Create(string? value)
    {
        var v = Rules.Text(value, "Adres URL", 2000);
        if (!Uri.TryCreate(v, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new DomainValidationException("Adres URL musi być bezwzględnym adresem http lub https.");
        return new WebUrl(v);
    }
    public override string ToString() => Url;
}
