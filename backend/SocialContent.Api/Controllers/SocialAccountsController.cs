using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Controllers;

/// <summary>Zarządzanie kontami w mediach społecznościowych (Facebook, Instagram, TikTok).</summary>
[ApiController]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[Route("api/[controller]")]
public class SocialAccountsController(ISocialAccountService service) : ControllerBase
{
    /// <summary>Pobierz listę kont społecznościowych</summary>
    /// <remarks>
    /// <para>Zwraca wszystkie konta posortowane po nazwie (accountName). Wymaga tokenu JWT (nagłówek Authorization: Bearer).</para>
    /// <para>Odpowiedź nigdy nie zawiera tokenów - pole hasRefreshToken informuje tylko, czy token odświeżania jest zapisany. Pola pictures i videos zawierają media z postów kierowanych na dane konto, a managerUsers - przypisanych managerów.</para>
    /// </remarks>
    /// <response code="200">Lista kont; pusta tablica, gdy nic nie dodano.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpGet]
    [ProducesResponseType<List<SocialAccountDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SocialAccountDto>>> GetAll(CancellationToken ct) =>
        Ok(await service.GetAllAsync(ct));

    /// <summary>Pobierz konto społecznościowe po identyfikatorze</summary>
    /// <remarks>Zwraca jedno konto wraz z przypisanymi managerami oraz mediami z postów kierowanych na to konto. Nie zwraca tokenów.</remarks>
    /// <param name="id">Identyfikator konta w systemie (GUID, pole id - nie accountId).</param>
    /// <response code="200">Znalezione konto.</response>
    /// <response code="404">Konto o podanym identyfikatorze nie istnieje.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<SocialAccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SocialAccountDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    /// <summary>Utwórz konto społecznościowe</summary>
    /// <remarks>
    /// <para>Rejestruje konto platformy wraz z tokenami dostępu. Wymagane: accountId, platform (Facebook, Instagram, TikTok), externalPlatformId, accountName, accessToken i tokenExpiresAt (UTC). Para platform + accountId musi być unikalna.</para>
    /// <para>Listą managerUserIds można od razu przypisać managerów; manager może należeć tylko do jednego konta, więc przypisanie przenosi go z poprzedniego. Tokeny są zapisywane, ale API nigdy ich nie zwraca.</para>
    /// </remarks>
    /// <param name="dto">Dane nowego konta.</param>
    /// <response code="201">Konto zostało utworzone; w nagłówku Location adres nowego zasobu.</response>
    /// <response code="400">Dane nie spełniają reguł walidacji (np. brak accessToken).</response>
    /// <response code="404">Któryś z wskazanych managerów nie istnieje.</response>
    /// <response code="409">Konto tej platformy o takim accountId już istnieje.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpPost]
    [ProducesResponseType<SocialAccountDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SocialAccountDto>> Create(SocialAccountWriteDto dto, CancellationToken ct)
    {
        var created = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Zaktualizuj konto społecznościowe</summary>
    /// <remarks>
    /// <para>Pełna aktualizacja danych konta. Pominięcie accessToken lub refreshToken zachowuje dotychczasową wartość tokenu, bo API nigdy nie zwraca tokenów.</para>
    /// <para>Lista managerUserIds jest pełna: managerowie, których na niej brak, zostaną odpięci od konta, a podani - przypisani (przenoszeni z innych kont).</para>
    /// </remarks>
    /// <param name="id">Identyfikator konta w systemie (GUID).</param>
    /// <param name="dto">Nowe dane konta.</param>
    /// <response code="200">Konto po aktualizacji.</response>
    /// <response code="400">Dane nie spełniają reguł walidacji.</response>
    /// <response code="404">Konto lub któryś z wskazanych managerów nie istnieje.</response>
    /// <response code="409">Inne konto tej platformy ma już taki accountId.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<SocialAccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SocialAccountDto>> Update(Guid id, SocialAccountWriteDto dto, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, dto, ct));

    /// <summary>Usuń konto społecznościowe</summary>
    /// <remarks>Usuwa konto i odpina jego managerów (sami managerowie pozostają). Konta, do którego są przypisane posty, nie można usunąć - najpierw zmień konto docelowe tych postów lub je usuń. Operacji nie można cofnąć.</remarks>
    /// <param name="id">Identyfikator konta w systemie (GUID).</param>
    /// <response code="204">Konto zostało usunięte.</response>
    /// <response code="404">Konto o podanym identyfikatorze nie istnieje.</response>
    /// <response code="409">Do konta są przypisane posty.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
