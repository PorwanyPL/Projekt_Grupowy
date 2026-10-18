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
        if (!Validators.ValidDateFormat(date))
            throw new DomainValidationException("Nieprawidłowa data utworzenia.");
        return new CreatedAt(date);
    }

    public static CreatedAt Create(string? value)
    {
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date))
            throw new DomainValidationException("Data ma nieprawidłowy format (oczekiwano ISO 8601).");
        return Create(date);
    }
}
