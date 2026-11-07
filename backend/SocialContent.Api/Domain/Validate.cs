using System.Globalization;
using System.Text.RegularExpressions;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;

namespace SocialContent.Api.Domain;

/// <summary>
/// Jedno miejsce na wszystkie metody walidacyjne domeny: predykaty (zwracają bool), reguły rzucające
/// <see cref="DomainValidationException"/> oraz normalizacje ściśle związane z walidacją (e-mail, data UTC).
/// </summary>
public static class Validate
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;
    public const int MaxEmailLength = 254;
    public const int MaxUserTags = 30;
    public const int MaxImageBytes = 10 * 1024 * 1024;
    public const int MaxVideoBytes = 50 * 1024 * 1024;

    private static readonly HashSet<Format> ImageFormats =
        [Format.Png, Format.Jpeg, Format.Jpg, Format.Gif, Format.Webp];

    private static readonly HashSet<Format> VideoFormats =
        [Format.Mp4, Format.Mp3, Format.Flac];

    private static readonly Dictionary<string, Format> ExtensionFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = Format.Png, [".jpg"] = Format.Jpg, [".jpeg"] = Format.Jpeg, [".gif"] = Format.Gif,
        [".webp"] = Format.Webp, [".mp4"] = Format.Mp4, [".mp3"] = Format.Mp3, [".flac"] = Format.Flac
    };

    private static readonly Regex StorageKeyPattern =
        new(@"^[0-9a-f]{32}\.(png|jpe?g|gif|webp|mp4|mp3|flac)$", RegexOptions.Compiled);

    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    // ---------- Predykaty ----------

    public static bool NotNull(string? value) => value is not null;

    public static bool NotEmpty(string? value) => !string.IsNullOrEmpty(value);

    /// <summary>Metoda bez nazwy na diagramie "+ (string): bool" - przyjęto: tekst nie jest pusty ani z samych spacji.</summary>
    public static bool NotWhiteSpace(string? value) => !string.IsNullOrWhiteSpace(value);

    public static bool ValidNameElement(string? value)
    {
        if (!NotNull(value) || !NotWhiteSpace(value)) return false;
        var v = value!.Trim();
        if (v.Length is < 1 or > 50) return false;
        if (!char.IsLetter(v[0]) || !char.IsLetter(v[^1])) return false;
        return v.All(c => char.IsLetter(c) || c == '-' || c == '\'');
    }

    public static bool ValidFileFormat(FileType file) => file switch
    {
        Picture p => ImageFormats.Contains(p.Format),
        Video v => VideoFormats.Contains(v.Format),
        _ => false
    };

    /// <summary>
    /// Sprawdza tylko poprawność formatu daty (nie jest wartością domyślną, rok od 2000).
    /// Reguły "nie z przyszłości" (CreatedAt) i "w przyszłości" (ScheduledTime) stosują same value objecty.
    /// </summary>
    public static bool ValidDateFormat(DateTime date) =>
        date != default && date.Year >= 2000;

    // ---------- Reguły rzucające DomainValidationException ----------

    public static string Name(string? value, string field)
    {
        if (!NotNull(value) || !NotEmpty(value) || !ValidNameElement(value))
            throw new DomainValidationException(
                $"{field} musi mieć 1-50 znaków i składać się z liter, '-' lub apostrofu.");
        return value!.Trim();
    }

    public static string Text(string? value, string field, int max)
    {
        if (!NotNull(value) || !NotWhiteSpace(value))
            throw new DomainValidationException($"{field} nie może być pusty.");
        var v = value!.Trim();
        if (v.Length > max)
            throw new DomainValidationException($"{field} może mieć maksymalnie {max} znaków.");
        return v;
    }

    /// <summary>Tekst opcjonalny: null lub same spacje dają null, w przeciwnym razie obowiązuje limit długości.</summary>
    public static string? OptionalText(string? value, string field, int max) =>
        NotWhiteSpace(value) ? Text(value, field, max) : null;

    /// <param name="normalizedEmail">Adres po <see cref="NormalizeEmail"/>.</param>
    public static void Email(string normalizedEmail)
    {
        if (normalizedEmail.Length == 0 || normalizedEmail.Length > MaxEmailLength || !EmailPattern.IsMatch(normalizedEmail))
            throw new DomainValidationException("Podaj poprawny adres e-mail (maksymalnie 254 znaki).");
    }

    public static void Password(string? password)
    {
        var problems = new List<string>();
        var p = password ?? "";

        if (p.Length < MinPasswordLength || p.Length > MaxPasswordLength)
            problems.Add($"długość {MinPasswordLength}-{MaxPasswordLength} znaków");
        if (!p.Any(char.IsLower)) problems.Add("mała litera");
        if (!p.Any(char.IsUpper)) problems.Add("wielka litera");
        if (!p.Any(char.IsDigit)) problems.Add("cyfra");

        if (problems.Count > 0)
            throw new DomainValidationException($"Hasło nie spełnia wymagań. Wymagane: {string.Join(", ", problems)}.");
    }

    public static DateTime ParseIsoDate(string? value)
    {
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date))
            throw new DomainValidationException("Data ma nieprawidłowy format (oczekiwano ISO 8601).");
        return date;
    }

    // ---------- Normalizacje ----------

    public static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    /// <summary>Normalizuje datę do UTC (Unspecified traktowane jako UTC - tak są zapisywane w bazie).</summary>
    public static DateTime ToUtc(DateTime date) => date.Kind switch
    {
        DateTimeKind.Local => date.ToUniversalTime(),
        DateTimeKind.Utc => date,
        _ => DateTime.SpecifyKind(date, DateTimeKind.Utc)
    };

    // ---------- Media (upload) ----------

    /// <summary>Rodzaj pliku dla formatu: zdjęcie albo wideo (Mp4, Mp3, Flac).</summary>
    public static MediaKind KindOf(Format format) => ImageFormats.Contains(format) ? MediaKind.Picture : MediaKind.Video;

    /// <summary>Format na podstawie rozszerzenia nazwy pliku; null, gdy rozszerzenie jest niedozwolone.</summary>
    public static Format? FormatFromExtension(string? fileNameOrExtension)
    {
        var ext = Path.GetExtension(fileNameOrExtension ?? "");
        return ExtensionFormats.TryGetValue(ext, out var f) ? f : null;
    }

    /// <summary>Poprawny klucz pliku w magazynie (32 znaki hex + dozwolone rozszerzenie); chroni przed wyjściem poza katalog.</summary>
    public static string StorageKey(string? key)
    {
        if (key is null || !StorageKeyPattern.IsMatch(key))
            throw new DomainValidationException("Nieprawidłowy identyfikator przesłanego pliku (storageKey).");
        return key;
    }

    public static bool IsStorageKey(string? key) => key is not null && StorageKeyPattern.IsMatch(key);

    /// <summary>
    /// Waliduje przesyłany plik: dozwolone rozszerzenie, rozmiar (zdjęcie do 10 MB, wideo do 50 MB)
    /// i zgodność początku zawartości (sygnatury) z deklarowanym formatem. Zwraca format pliku.
    /// </summary>
    public static Format MediaUpload(string? fileName, long length, ReadOnlySpan<byte> header)
    {
        var format = FormatFromExtension(fileName)
            ?? throw new DomainValidationException(
                "Niedozwolony typ pliku. Zdjęcia: png, jpg, jpeg, gif, webp. Wideo: mp4, mp3, flac.");
        if (length <= 0)
            throw new DomainValidationException("Plik jest pusty.");

        var kind = KindOf(format);
        var max = kind == MediaKind.Picture ? MaxImageBytes : MaxVideoBytes;
        if (length > max)
            throw new DomainValidationException(
                $"Plik jest za duży. Maksymalny rozmiar: {max / (1024 * 1024)} MB dla {(kind == MediaKind.Picture ? "zdjęć" : "wideo")}.");

        if (!HasSignature(format, header))
            throw new DomainValidationException("Zawartość pliku nie odpowiada jego rozszerzeniu.");
        return format;
    }

    private static bool HasSignature(Format format, ReadOnlySpan<byte> h)
    {
        static bool Starts(ReadOnlySpan<byte> data, params byte[] prefix) =>
            data.Length >= prefix.Length && data[..prefix.Length].SequenceEqual(prefix);

        return format switch
        {
            Format.Png => Starts(h, 0x89, 0x50, 0x4E, 0x47),
            Format.Jpeg or Format.Jpg => Starts(h, 0xFF, 0xD8, 0xFF),
            Format.Gif => Starts(h, 0x47, 0x49, 0x46, 0x38),
            Format.Webp => Starts(h, 0x52, 0x49, 0x46, 0x46) && h.Length >= 12 && Starts(h[8..], 0x57, 0x45, 0x42, 0x50),
            Format.Mp4 => h.Length >= 8 && Starts(h[4..], 0x66, 0x74, 0x79, 0x70),
            Format.Mp3 => Starts(h, 0x49, 0x44, 0x33) || (h.Length >= 2 && h[0] == 0xFF && (h[1] & 0xE0) == 0xE0),
            Format.Flac => Starts(h, 0x66, 0x4C, 0x61, 0x43),
            _ => false
        };
    }

    // ---------- Oznaczenia użytkowników (Instagram) ----------

    /// <summary>Przycina, usuwa puste i powtórzone (bez względu na wielkość liter) oznaczenia; sprawdza limity Instagrama.</summary>
    public static List<string> NormalizeTags(IEnumerable<string>? tags)
    {
        var list = (tags ?? Array.Empty<string>())
            .Select(t => (t ?? "").Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (list.Count > MaxUserTags)
            throw new DomainValidationException($"Post na Instagramie może mieć maksymalnie {MaxUserTags} oznaczeń użytkowników.");
        if (list.Any(t => t.Length > 100))
            throw new DomainValidationException("Pojedyncze oznaczenie użytkownika może mieć maksymalnie 100 znaków.");
        return list;
    }

    // ---------- Połączenie z kontem społecznościowym ----------

    /// <summary>
    /// Lokalna kontrola połączenia: konto aktywne, token dostępu obecny i niewygasły. Nie wykonuje żądań do platformy.
    /// Zwraca false i przyczynę w <paramref name="error"/>; wynik zapisuje wywołujący (SocialAccount.SetConnectionError).
    /// </summary>
    public static bool VerifyConnection(SocialAccount account, out string? error)
    {
        if (!account.IsActive)
            error = "Konto jest nieaktywne.";
        else if (string.IsNullOrWhiteSpace(account.AccessToken))
            error = "Brak tokenu dostępu.";
        else if (account.TokenExpiresAt <= DateTime.UtcNow)
            error = "Token dostępu wygasł.";
        else
            error = null;
        return error is null;
    }
}
