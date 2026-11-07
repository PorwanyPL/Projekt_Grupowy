using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Controllers;

/// <summary>Pliki mediów (zdjęcia, wideo) przesyłane do posta. Pliki są prywatne dla firmy.</summary>
[ApiController]
[Authorize(Roles = Roles.CompanyOrManager)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[Route("api/[controller]")]
public class MediaController(IMediaService service) : ControllerBase
{
    /// <summary>Prześlij plik medium</summary>
    /// <remarks>
    /// <para>Żądanie multipart/form-data z polem pliku "file". Serwer sprawdza rozszerzenie (png, jpg, jpeg, gif, webp, mp4, mp3, flac), rozmiar (zdjęcia do 10 MB, wideo do 50 MB) oraz zgodność zawartości z formatem (sygnatura pliku).</para>
    /// <para>Zwrócony storageKey podaje się potem w polach pictures/videos posta. Plik trafia do magazynu firmy użytkownika.</para>
    /// </remarks>
    /// <param name="file">Przesyłany plik.</param>
    /// <response code="200">Plik zapisany; zwraca klucz, rodzaj, format i rozmiar.</response>
    /// <response code="400">Brak pliku, niedozwolone rozszerzenie, zbyt duży rozmiar lub zawartość niezgodna z formatem.</response>
    [HttpPost]
    [RequestSizeLimit(52_428_800)]
    [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<MediaUploadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MediaUploadDto>> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new Domain.Exceptions.DomainValidationException("Dołącz plik w polu 'file'.");
        await using var stream = file.OpenReadStream();
        return Ok(await service.UploadAsync(User.ToCurrentUser(), stream, file.FileName, file.Length, ct));
    }

    /// <summary>Pobierz plik medium</summary>
    /// <remarks>Dostępne dla użytkowników firmy, do której należy plik (do podglądu w formularzu i na liście postów).</remarks>
    /// <param name="storageKey">Klucz pliku zwrócony przy przesłaniu.</param>
    /// <response code="200">Zawartość pliku.</response>
    /// <response code="404">Plik nie istnieje.</response>
    [HttpGet("{storageKey}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(string storageKey, CancellationToken ct)
    {
        var (path, contentType) = await service.OpenAsync(User.ToCurrentUser(), storageKey, ct);
        return PhysicalFile(path, contentType);
    }
}
