using SocialContent.Api.Domain;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;

namespace SocialContent.Api.Services;

/// <summary>Plik zapisany w magazynie; rozmiar i format pochodzą z dysku, nie od klienta.</summary>
public record StoredMedia(string StorageKey, MediaKind Kind, Format Format, int SizeKb);

/// <summary>Magazyn przesłanych plików: katalog na dysku serwera, osobny podkatalog dla każdej firmy.</summary>
public interface IMediaStorage
{
    /// <summary>Waliduje (typ, rozmiar, sygnatura) i zapisuje plik. Rzuca DomainValidationException dla plików niedozwolonych.</summary>
    Task<StoredMedia> SaveAsync(Guid companyId, Stream content, string originalFileName, long length, CancellationToken ct);

    /// <summary>Plik firmy o danym kluczu; null, gdy nie istnieje (także dla nieprawidłowego klucza).</summary>
    StoredMedia? Find(Guid companyId, string storageKey);

    /// <summary>Ścieżka pliku na dysku albo null.</summary>
    string? PhysicalPath(Guid companyId, string storageKey);

    void Delete(Guid companyId, string storageKey);
}

public class MediaStorage(string rootPath) : IMediaStorage
{
    public async Task<StoredMedia> SaveAsync(Guid companyId, Stream content, string originalFileName, long length,
        CancellationToken ct)
    {
        var header = new byte[16];
        var read = 0;
        while (read < header.Length)
        {
            var n = await content.ReadAsync(header.AsMemory(read, header.Length - read), ct);
            if (n == 0) break;
            read += n;
        }

        var format = Validate.MediaUpload(originalFileName, length, header.AsSpan(0, read));
        var kind = Validate.KindOf(format);
        var max = kind == MediaKind.Picture ? Validate.MaxImageBytes : Validate.MaxVideoBytes;

        var key = Guid.NewGuid().ToString("N") + Path.GetExtension(originalFileName).ToLowerInvariant();
        Directory.CreateDirectory(CompanyDirectory(companyId));
        var path = Path.Combine(CompanyDirectory(companyId), key);

        long written = 0;
        try
        {
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await file.WriteAsync(header.AsMemory(0, read), ct);
            written = read;

            var buffer = new byte[81920];
            int count;
            while ((count = await content.ReadAsync(buffer, ct)) > 0)
            {
                written += count;
                if (written > max) // deklarowana długość mogła kłamać
                    throw new DomainValidationException("Plik jest za duży.");
                await file.WriteAsync(buffer.AsMemory(0, count), ct);
            }
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }

        return new StoredMedia(key, kind, format, ToKb(written));
    }

    public StoredMedia? Find(Guid companyId, string storageKey)
    {
        var path = PhysicalPath(companyId, storageKey);
        if (path is null) return null;
        var format = Validate.FormatFromExtension(storageKey)!.Value;
        return new StoredMedia(storageKey, Validate.KindOf(format), format, ToKb(new FileInfo(path).Length));
    }

    public string? PhysicalPath(Guid companyId, string storageKey)
    {
        if (!Validate.IsStorageKey(storageKey)) return null;
        var path = Path.Combine(CompanyDirectory(companyId), storageKey);
        return File.Exists(path) ? path : null;
    }

    public void Delete(Guid companyId, string storageKey)
    {
        var path = PhysicalPath(companyId, storageKey);
        if (path is not null) File.Delete(path);
    }

    private string CompanyDirectory(Guid companyId) => Path.Combine(rootPath, companyId.ToString("N"));

    private static int ToKb(long bytes) => (int)Math.Max(1, (bytes + 1023) / 1024);
}
