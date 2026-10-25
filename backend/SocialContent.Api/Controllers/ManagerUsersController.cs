using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Controllers;

/// <summary>Zarządzanie użytkownikami typu ManagerUser (content managerami).</summary>
[ApiController]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[Route("api/[controller]")]
public class ManagerUsersController(IManagerUserService service) : ControllerBase
{
    /// <summary>Pobierz listę managerów</summary>
    /// <remarks>
    /// <para>Zwraca wszystkich managerów posortowanych alfabetycznie (nazwisko, potem imię). Wymaga tokenu JWT (nagłówek Authorization: Bearer).</para>
    /// <para>Odpowiedź nie zawiera adresów e-mail ani danych logowania. Pole socialAccountId wskazuje konto społecznościowe, do którego manager jest przypisany (null, jeśli do żadnego). Pola hasLoginAccount i isAccessBlocked mówią, czy manager ma konto logowania i czy jego dostęp jest zablokowany.</para>
    /// </remarks>
    /// <response code="200">Lista managerów; pusta tablica, gdy nikogo jeszcze nie dodano.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpGet]
    [ProducesResponseType<List<ManagerUserDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ManagerUserDto>>> GetAll(CancellationToken ct) =>
        Ok(await service.GetAllAsync(ct));

    /// <summary>Pobierz managera po identyfikatorze</summary>
    /// <remarks>Zwraca dane jednego managera. Nie zwraca adresu e-mail ani danych logowania.</remarks>
    /// <param name="id">Identyfikator managera (GUID).</param>
    /// <response code="200">Znaleziony manager.</response>
    /// <response code="404">Manager o podanym identyfikatorze nie istnieje.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ManagerUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ManagerUserDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    /// <summary>Utwórz managera bez konta logowania</summary>
    /// <remarks>
    /// <para>Tworzy rekord managera (imię, nazwisko, wiek, opis) bez danych logowania - taki manager nie może się zalogować. Aby założyć managera z możliwością logowania, użyj POST /api/Auth/register.</para>
    /// <para>Wymagane pola: firstName i lastName (1-50 znaków; litery, myślnik, apostrof), age (18-100), description (1-2000 znaków).</para>
    /// </remarks>
    /// <response code="201">Manager został utworzony; w nagłówku Location adres nowego zasobu.</response>
    /// <response code="400">Dane nie spełniają reguł walidacji; szczegóły w polach errors lub detail.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpPost]
    [ProducesResponseType<ManagerUserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ManagerUserDto>> Create(ManagerUserWriteDto dto, CancellationToken ct)
    {
        var created = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Zaktualizuj managera</summary>
    /// <remarks>
    /// <para>Pełna aktualizacja: zastępuje imię, nazwisko, wiek i opis, więc wszystkie cztery pola muszą być podane.</para>
    /// <para>Nie zmienia danych logowania ani przypisania do konta społecznościowego - to ostatnie ustawia się przez PUT /api/SocialAccounts/{id}.</para>
    /// </remarks>
    /// <param name="id">Identyfikator managera (GUID).</param>
    /// <param name="dto">Nowe dane managera.</param>
    /// <response code="200">Manager po aktualizacji.</response>
    /// <response code="400">Dane nie spełniają reguł walidacji.</response>
    /// <response code="404">Manager o podanym identyfikatorze nie istnieje.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<ManagerUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ManagerUserDto>> Update(Guid id, ManagerUserWriteDto dto, CancellationToken ct) =>
        Ok(await service.UpdateAsync(id, dto, ct));

    /// <summary>Usuń managera (tylko bez powiązań)</summary>
    /// <remarks>
    /// <para>Trwale usuwa managera wraz z jego danymi logowania (jeśli istnieją). Dozwolone wyłącznie wtedy, gdy manager nie ma powiązanych postów ani konta społecznościowego. Operacji nie można cofnąć.</para>
    /// <para>Jeśli powiązania istnieją, zwracany jest 409 Conflict - historia publikacji musi zostać zachowana. Zamiast usuwać, zablokuj dostęp (PUT /api/ManagerUsers/{id}/access) albo zanonimizuj dane (POST /api/ManagerUsers/{id}/anonymize).</para>
    /// </remarks>
    /// <param name="id">Identyfikator managera (GUID).</param>
    /// <response code="204">Manager został usunięty.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    /// <response code="404">Manager o podanym identyfikatorze nie istnieje.</response>
    /// <response code="409">Manager ma powiązane posty lub konto społecznościowe - zablokuj dostęp zamiast usuwać.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Zablokuj lub odblokuj dostęp managera</summary>
    /// <remarks>
    /// <para>Ustawia blokadę logowania managera (blocked = true) albo ją zdejmuje (blocked = false). Zablokowany manager zostaje w systemie wraz z powiązaniami z postami, ale nie może się zalogować, a jego wcześniej wydany token przestaje działać od razu.</para>
    /// <para>Dotyczy tylko managerów mających konto logowania. Nie można zablokować samego siebie.</para>
    /// </remarks>
    /// <param name="id">Identyfikator managera (GUID).</param>
    /// <param name="dto">Pole blocked: true = zablokuj, false = odblokuj.</param>
    /// <response code="200">Manager po zmianie; pole isAccessBlocked odzwierciedla nowy stan.</response>
    /// <response code="400">Brak pola blocked, manager nie ma konta logowania albo próba zablokowania samego siebie.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    /// <response code="404">Manager o podanym identyfikatorze nie istnieje.</response>
    [HttpPut("{id:guid}/access")]
    [ProducesResponseType<ManagerUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ManagerUserDto>> SetAccess(Guid id, ManagerAccessDto dto, CancellationToken ct) =>
        Ok(await service.SetAccessAsync(id, dto.Blocked!.Value, CurrentManagerId(), ct));

    /// <summary>Zanonimizuj managera (skasowanie danych osobowych)</summary>
    /// <remarks>
    /// <para>Przeznaczone dla próśb o skasowanie danych. Zamienia imię i nazwisko na "Usunięty użytkownik", zastępuje opis informacją o usunięciu danych i usuwa konto logowania (e-mail i hasło), co natychmiast unieważnia wydane tokeny.</para>
    /// <para>Powiązania z postami oraz z kontem społecznościowym pozostają nienaruszone, więc historia publikacji jest zachowana. Wiek nie jest zmieniany. Operacji nie można cofnąć. Można ją wykonać na każdym managerze, także bez powiązań i bez konta logowania.</para>
    /// </remarks>
    /// <param name="id">Identyfikator managera (GUID).</param>
    /// <response code="200">Manager po anonimizacji.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    /// <response code="404">Manager o podanym identyfikatorze nie istnieje.</response>
    [HttpPost("{id:guid}/anonymize")]
    [ProducesResponseType<ManagerUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ManagerUserDto>> Anonymize(Guid id, CancellationToken ct) =>
        Ok(await service.AnonymizeAsync(id, ct));

    private Guid? CurrentManagerId()
    {
        var subject = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(subject, out var id) ? id : null;
    }
}
