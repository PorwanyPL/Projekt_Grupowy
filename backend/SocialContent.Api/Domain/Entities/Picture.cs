using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Domain.ValueObjects;

namespace SocialContent.Api.Domain.Entities;

public sealed class Picture : FileType
{
    private Picture() { }

    public static Picture Create(FileName fileName, Format format, int sizeKb, string storageKey)
    {
        if (sizeKb <= 0) throw new DomainValidationException("Rozmiar zdjęcia musi być większy od 0.");
        var picture = new Picture
        {
            Id = Guid.NewGuid(), FileName = fileName, Format = format, SizeKb = sizeKb,
            StorageKey = Validate.StorageKey(storageKey)
        };
        if (!Validate.ValidFileFormat(picture))
            throw new DomainValidationException($"Format {format} nie jest dozwolony dla zdjęcia.");
        return picture;
    }
}
