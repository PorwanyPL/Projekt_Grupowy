using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Domain.ValueObjects;

namespace SocialContent.Api.Domain.Entities;

public sealed class Video : FileType
{
    /// <summary>Czas trwania w sekundach; null = nieznany (serwer nie analizuje zawartości przesłanych plików).</summary>
    public int? Length { get; private set; }

    private Video() { }

    public static Video Create(FileName fileName, Format format, int sizeKb, string storageKey, int? length = null)
    {
        if (sizeKb <= 0) throw new DomainValidationException("Rozmiar wideo musi być większy od 0.");
        if (length is <= 0) throw new DomainValidationException("Długość wideo musi być większa od 0.");
        var video = new Video
        {
            Id = Guid.NewGuid(), FileName = fileName, Format = format, SizeKb = sizeKb, Length = length,
            StorageKey = Validate.StorageKey(storageKey)
        };
        if (!Validate.ValidFileFormat(video))
            throw new DomainValidationException($"Format {format} nie jest dozwolony dla wideo.");
        return video;
    }
}
