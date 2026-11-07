using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.ValueObjects;

namespace SocialContent.Api.Domain.Entities;

public abstract class FileType
{
    public Guid Id { get; protected set; }

    /// <summary>Nazwa pliku widoczna dla użytkownika (oryginalna nazwa z przesłania).</summary>
    public FileName FileName { get; protected set; } = null!;
    public Format Format { get; protected set; }

    /// <summary>Rozmiar pliku w KB - mierzony przez serwer po zapisie pliku, nie podawany przez klienta.</summary>
    public int SizeKb { get; protected set; }

    /// <summary>Klucz pliku w magazynie (nazwa pliku na dysku serwera, np. "3f2a...c9.png").</summary>
    public string StorageKey { get; protected set; } = null!;

    public Guid PostId { get; private set; }
}
