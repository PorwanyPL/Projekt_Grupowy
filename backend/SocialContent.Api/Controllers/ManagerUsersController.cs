using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Controllers;

/// <summary>Zarządzanie managerami (ManagerUser) przez firmę. Dostępne wyłącznie dla roli CompanyUser i tylko dla managerów jej firmy.</summary>
[ApiController]
[Authorize(Roles = Roles.Company)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[Route("api/[controller]")]
public class ManagerUsersController(IManagerUserService service) : ControllerBase
{
    private Guid CompanyId => User.ToCurrentUser().Id;

    /// <summary>Pobierz listę managerów firmy</summary>
    /// <remarks>
    /// <para>Zwraca wszystkich managerów zalogowanej firmy posortowanych alfabetycznie (nazwisko, potem imię). Wymaga tokenu JWT z rolą CompanyUser.</para>
    /// <para>Odpowiedź nie zawiera adresów e-mail ani danych logowania. Pole socialAccountIds wskazuje konta społecznościowe przypisane managerowi, a hasLoginAccount i isAccessBlocked mówią, czy manager ma konto logowania i czy jego dostęp jest zablokowany.</para>
    /// </remarks>
    /// <response code="200">Lista managerów; pusta tablica, gdy nikogo jeszcze nie dodano.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    /// <response code="403">Zalogowany użytkownik nie ma roli CompanyUser.</response>
    [HttpGet]
    [ProducesResponseType<List<ManagerUserDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ManagerUserDto>>> GetAll(CancellationToken ct) =>
        Ok(await service.GetAllAsync(CompanyId, ct));

    /// <summary>Pobierz managera po identyfikatorze</summary>
    /// <remarks>Zwraca dane jednego managera swojej firmy. Manager innej firmy jest traktowany jak nieistniejący. Nie zwraca adresu e-mail ani danych logowania.</remarks>
    /// <param name="id">Identyfikator managera (GUID).</param>
    /// <response code="200">Znaleziony manager.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    /// <response code="403">Zalogowany użytkownik nie ma roli CompanyUser.</response>
    /// <response code="404">Manager o podanym identyfikatorze nie istnieje w tej firmie.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ManagerUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ManagerUserDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await service.GetAsync(CompanyId, id, ct));

    /// <summary>Utwórz managera w swojej firmie</summary>
    /// <remarks>
    /// <para>Tworzy managera (imię, nazwisko, wiek, opis) przypisanego do firmy zalogowanego CompanyUser. Email i password są wymagane: każdy nowy manager dostaje konto logowania i może tworzyć posty na przypisanych kontach.</para>
    /// <para>Wymagane pola: firstName i lastName (1-50 znaków; litery, myślnik, apostrof), age (18-100), description (1-2000 znaków). Adres e-mail musi być unikalny we wszystkich rolach, a hasło ma mieć 8-128 znaków, w tym małą literę, wielką literę i cyfrę.</para>
    /// </remarks>
    /// <response code="201">Manager został utworzony; w nagłówku Location adres nowego zasobu.</response>
    /// <response code="400">Dane nie spełniają reguł walidacji (w tym brak lub niepoprawny e-mail albo hasło).</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    /// <response code="403">Zalogowany użytkownik nie ma roli CompanyUser.</response>
    /// <response code="409">Konto z tym adresem e-mail już istnieje.</response>
    [HttpPost]
    [ProducesResponseType<ManagerUserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ManagerUserDto>> Create(ManagerUserCreateDto dto, CancellationToken ct)
    {
        var created = await service.CreateAsync(CompanyId, dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Zaktualizuj managera</summary>
    /// <remarks>
    /// <para>Pełna aktualizacja: zastępuje imię, nazwisko, wiek i opis, więc wszystkie cztery pola muszą być podane.</para>
    /// <para>Nie zmienia danych logowania (służy do tego PUT /api/ManagerUsers/{id}/credentials) ani przypisań do kont społecznościowych - te ustawia się przez PUT /api/SocialAccounts/{id}/managers.</para>
    /// </remarks>
    /// <param name="id">Identyfikator managera (GUID).</param>
    /// <param name="dto">Nowe dane managera.</param>
    /// <response code="200">Manager po aktualizacji.</response>
    /// <response code="400">Dane nie spełniają reguł walidacji.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    /// <response code="403">Zalogowany użytkownik nie ma roli CompanyUser.</response>
    /// <response code="404">Manager o podanym identyfikatorze nie istnieje w tej firmie.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<ManagerUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ManagerUserDto>> Update(Guid id, ManagerUserWriteDto dto, CancellationToken ct) =>
        Ok(await service.UpdateAsync(CompanyId, id, dto, ct));

    /// <summary>Usuń managera (tylko bez powiązań)</summary>
    /// <remarks>
    /// <para>Trwale usuwa managera wraz z jego danymi logowania (jeśli istnieją). Dozwolone wyłącznie wtedy, gdy manager nie ma powiązanych postów ani kont społecznościowych. Operacji nie można cofnąć.</para>
    /// <para>Jeśli powiązania istnieją, zwracany jest 409 Conflict - historia publikacji musi zostać zachowana. Zamiast usuwać, zablokuj dostęp (PUT /api/ManagerUsers/{id}/access) albo zanonimizuj dane (POST /api/ManagerUsers/{id}/anonymize).</para>
    /// </remarks>
    /// <param name="id">Identyfikator managera (GUID).</param>
    /// <response code="204">Manager został usunięty.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    /// <response code="403">Zalogowany użytkownik nie ma roli CompanyUser.</response>
    /// <response code="404">Manager o podanym identyfikatorze nie istnieje w tej firmie.</response>
    /// <response code="409">Manager ma powiązane posty lub konta społecznościowe - zablokuj dostęp zamiast usuwać.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(CompanyId, id, ct);
        return NoContent();
    }

    /// <summary>Zablokuj lub odblokuj dostęp managera</summary>
    /// <remarks>
    /// <para>Ustawia blokadę logowania managera (blocked = true) albo ją zdejmuje (blocked = false). Zablokowany manager zostaje w systemie wraz z powiązaniami z postami, ale nie może się zalogować, a jego wcześniej wydany token przestaje działać od razu.</para>
    /// <para>Dotyczy tylko managerów mających konto logowania.</para>
    /// </remarks>
    /// <param name="id">Identyfikator managera (GUID).</param>
    /// <param name="dto">Pole blocked: true = zablokuj, false = odblokuj.</param>
    /// <response code="200">Manager po zmianie; pole isAccessBlocked odzwierciedla nowy stan.</response>
    /// <response code="400">Brak pola blocked albo manager nie ma konta logowania.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    /// <response code="403">Zalogowany użytkownik nie ma roli CompanyUser.</response>
    /// <response code="404">Manager o podanym identyfikatorze nie istnieje w tej firmie.</response>
    [HttpPut("{id:guid}/access")]
    [ProducesResponseType<ManagerUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ManagerUserDto>> SetAccess(Guid id, ManagerAccessDto dto, CancellationToken ct) =>
        Ok(await service.SetAccessAsync(CompanyId, id, dto.Blocked!.Value, ct));

    /// <summary>Zmień e-mail i hasło managera</summary>
    /// <remarks>
    /// <para>Ustawia nowy adres e-mail (wymagany; unikalny we wszystkich rolach) i opcjonalnie nowe hasło (8-128 znaków, mała i wielka litera, cyfra). Pominięte password zostawia dotychczasowe hasło. Walidacja jest ta sama co przy tworzeniu managera (klasa Validate).</para>
    /// <para>Manager bez konta logowania (np. zanonimizowany) dostaje je dopiero po podaniu hasła.</para>
    /// </remarks>
    /// <param name="id">Identyfikator managera (GUID).</param>
    /// <param name="dto">Nowy e-mail i opcjonalnie nowe hasło.</param>
    /// <response code="200">Manager po zmianie.</response>
    /// <response code="400">Niepoprawny e-mail lub hasło.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    /// <response code="403">Zalogowany użytkownik nie ma roli CompanyUser.</response>
    /// <response code="404">Manager nie istnieje w tej firmie.</response>
    /// <response code="409">Konto z tym adresem e-mail już istnieje.</response>
    [HttpPut("{id:guid}/credentials")]
    [ProducesResponseType<ManagerUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ManagerUserDto>> UpdateCredentials(Guid id, ManagerCredentialsUpdateDto dto, CancellationToken ct) =>
        Ok(await service.UpdateCredentialsAsync(CompanyId, id, dto, ct));

    /// <summary>Zanonimizuj managera (skasowanie danych osobowych)</summary>
    /// <remarks>
    /// <para>Przeznaczone dla próśb o skasowanie danych. Zamienia imię i nazwisko na "Usunięty użytkownik", zastępuje opis informacją o usunięciu danych i usuwa konto logowania (e-mail i hasło), co natychmiast unieważnia wydane tokeny.</para>
    /// <para>Powiązania z postami oraz z kontami społecznościowymi pozostają nienaruszone, więc historia publikacji jest zachowana. Wiek nie jest zmieniany. Operacji nie można cofnąć. Można ją wykonać na każdym managerze firmy, także bez powiązań i bez konta logowania.</para>
    /// </remarks>
    /// <param name="id">Identyfikator managera (GUID).</param>
    /// <response code="200">Manager po anonimizacji.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    /// <response code="403">Zalogowany użytkownik nie ma roli CompanyUser.</response>
    /// <response code="404">Manager o podanym identyfikatorze nie istnieje w tej firmie.</response>
    [HttpPost("{id:guid}/anonymize")]
    [ProducesResponseType<ManagerUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ManagerUserDto>> Anonymize(Guid id, CancellationToken ct) =>
        Ok(await service.AnonymizeAsync(CompanyId, id, ct));
}
