using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Services;

/// <summary>Przesyłanie i pobieranie plików. Pliki należą do firmy: manager działa na plikach swojej firmy.</summary>
public interface IMediaService
{
    Task<MediaUploadDto> UploadAsync(CurrentUser user, Stream content, string fileName, long length, CancellationToken ct);

    /// <summary>Ścieżka pliku firmy użytkownika oraz typ zawartości do odpowiedzi HTTP.</summary>
    Task<(string Path, string ContentType)> OpenAsync(CurrentUser user, string storageKey, CancellationToken ct);
}

public class MediaService(AppDbContext db, IMediaStorage storage) : IMediaService
{
    public async Task<MediaUploadDto> UploadAsync(CurrentUser user, Stream content, string fileName, long length,
        CancellationToken ct)
    {
        var companyId = await CompanyIdOf(user, ct);
        var stored = await storage.SaveAsync(companyId, content, fileName, length, ct);
        return new MediaUploadDto(stored.StorageKey, Path.GetFileName(fileName), stored.Kind, stored.Format, stored.SizeKb);
    }

    public async Task<(string Path, string ContentType)> OpenAsync(CurrentUser user, string storageKey, CancellationToken ct)
    {
        var companyId = await CompanyIdOf(user, ct);
        var path = storage.PhysicalPath(companyId, storageKey)
                   ?? throw new NotFoundException("Nie znaleziono pliku.");
        return (path, ContentTypeOf(storageKey));
    }

    private async Task<Guid> CompanyIdOf(CurrentUser user, CancellationToken ct)
    {
        if (user.IsCompany) return user.Id;
        var companyId = await db.ManagerUsers.AsNoTracking().Where(m => m.Id == user.Id)
            .Select(m => (Guid?)m.CompanyId).FirstOrDefaultAsync(ct);
        return companyId ?? throw new ForbiddenException("Brak dostępu do plików.");
    }

    private static string ContentTypeOf(string key) => Path.GetExtension(key).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".mp4" => "video/mp4",
        ".mp3" => "audio/mpeg",
        ".flac" => "audio/flac",
        _ => "application/octet-stream"
    };
}
