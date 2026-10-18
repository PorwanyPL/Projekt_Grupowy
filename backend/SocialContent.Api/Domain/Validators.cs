namespace SocialContent.Api.Domain;

public static class Validators
{
    private static readonly HashSet<Format> ImageFormats =
        [Format.Png, Format.Jpeg, Format.Jpg, Format.Gif, Format.Webp];

    private static readonly HashSet<Format> VideoFormats =
        [Format.Mp4, Format.Mp3, Format.Flac];

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

    public static bool ValidDateFormat(DateTime date) =>
        date != default && date.Year >= 2000 && date <= DateTime.UtcNow.AddMinutes(5);
}
