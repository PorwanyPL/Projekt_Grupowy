using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Controllers;

/// <summary>
/// Konta w mediach społecznościowych (Facebook, Instagram, TikTok). Tworzy je klient i zaprasza firmę do zarządzania;
/// firma przyjmuje zaproszenie i przypisuje managerów. Odczyt: klient (własne), firma, przypisani managerowie.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Any)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[Route("api/[controller]")]
public class SocialAccountsController(ISocialAccountService service) : ControllerBase
{
    /// <summary>Identyfikator zalogowanego użytkownika: klienta (konta własne) albo firmy (zaproszenia, managerowie).</summary>
    private Guid UserId => User.ToCurrentUser().Id;

    /// <summary>Pobierz listę kont społecznościowych</summary>
    /// <remarks>
    /// <para>ClientUser dostaje swoje konta (w każdym stanie zaproszenia), CompanyUser - konta, do których został zaproszony lub którymi zarządza (odrzucone znikają), ManagerUser - tylko konta, do których został przypisany. Wynik jest posortowany po nazwie (accountName). Wymaga tokenu JWT (nagłówek Authorization: Bearer).</para>
    /// <para>Pole ownerName to klient-właściciel: dla firmy i managera jego pseudonim nadany przez firmę (albo imię i nazwisko). Opcjonalne filtry: clientId (konta jednego klienta - widoki firmy i managera) i companyId (konta zarządzane przez daną firmę - widok klienta).</para>
    /// <para>Odpowiedź nigdy nie zawiera tokenów - pole hasRefreshToken informuje tylko, czy token odświeżania jest zapisany. Pola pictures i videos zawierają media z postów kierowanych na dane konto, a managerUsers - przypisanych managerów.</para>
    /// </remarks>
    /// <response code="200">Lista kont; pusta tablica, gdy nic nie dodano.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    /// <param name="clientId">Tylko konta tego klienta.</param>
    /// <param name="companyId">Tylko konta zarządzane przez tę firmę.</param>
    [HttpGet]
    [ProducesResponseType<List<SocialAccountDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SocialAccountDto>>> GetAll(Guid? clientId, Guid? companyId, CancellationToken ct) =>
        Ok(await service.GetAllAsync(User.ToCurrentUser(), clientId, companyId, ct));

    /// <summary>Pobierz konto społecznościowe po identyfikatorze</summary>
    /// <remarks>Zwraca jedno konto wraz z przypisanymi managerami oraz mediami z postów kierowanych na to konto. Nie zwraca tokenów.</remarks>
    /// <param name="id">Identyfikator konta w systemie (GUID, pole id - nie accountId).</param>
    /// <response code="200">Znalezione konto.</response>
    /// <response code="404">Konto nie istnieje albo nie jest widoczne dla zalogowanego użytkownika.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    /// <response code="403">Rola użytkownika nie ma dostępu do tego zasobu.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<SocialAccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SocialAccountDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await service.GetAsync(User.ToCurrentUser(), id, ct));

    /// <summary>Utwórz konto społecznościowe</summary>
    /// <remarks>
    /// <para>Klient rejestruje własne konto platformy wraz z tokenami dostępu i podaje adres e-mail firmy (companyEmail), która ma nim zarządzać - firma dostaje zaproszenie (managementStatus = Invited). Wymagane: accountId, platform (Facebook, Instagram, TikTok), externalPlatformId, accountName, accessToken, tokenExpiresAt (UTC) i companyEmail. Para platform + accountId musi być unikalna w obrębie klienta. Dostępne tylko dla ClientUser.</para>
    /// <para>Docelowo konta będą dodawane przez API platform - ta sama logika (SocialAccount.Create) przyjmie wtedy dane z API zamiast z formularza. Tokeny są zapisywane, ale API nigdy ich nie zwraca.</para>
    /// </remarks>
    /// <param name="dto">Dane nowego konta.</param>
    /// <response code="201">Konto zostało utworzone; w nagłówku Location adres nowego zasobu.</response>
    /// <response code="400">Dane nie spełniają reguł walidacji (np. brak accessToken).</response>
    /// <response code="404">Firma o podanym adresie e-mail nie istnieje.</response>
    /// <response code="409">Konto tej platformy o takim accountId już istnieje.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpPost]
    [Authorize(Roles = Roles.Client)]
    [ProducesResponseType<SocialAccountDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SocialAccountDto>> Create(SocialAccountWriteDto dto, CancellationToken ct)
    {
        var created = await service.CreateAsync(UserId, dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Zaktualizuj konto społecznościowe</summary>
    /// <remarks>
    /// <para>Pełna aktualizacja danych własnego konta klienta. Pominięcie accessToken lub refreshToken zachowuje dotychczasową wartość tokenu, bo API nigdy nie zwraca tokenów. Firmy i przypisanych managerów ta operacja nie zmienia (firmę zmienia się zaproszeniem).</para>
    /// </remarks>
    /// <param name="id">Identyfikator konta w systemie (GUID).</param>
    /// <param name="dto">Nowe dane konta.</param>
    /// <response code="200">Konto po aktualizacji.</response>
    /// <response code="400">Dane nie spełniają reguł walidacji.</response>
    /// <response code="404">Konto lub któryś z wskazanych managerów nie istnieje.</response>
    /// <response code="409">Inne konto tej platformy ma już taki accountId.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Client)]
    [ProducesResponseType<SocialAccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SocialAccountDto>> Update(Guid id, SocialAccountWriteDto dto, CancellationToken ct) =>
        Ok(await service.UpdateAsync(UserId, id, dto, ct));

    /// <summary>Przypisz managerów do konta społecznościowego</summary>
    /// <remarks>
    /// <para>Ustawia pełną listę managerów, którzy mogą tworzyć posty na tym koncie (zastępuje dotychczasową). Tylko przypisani managerowie zobaczą konto i dodadzą do niego posty. Wszyscy wskazani managerowie muszą należeć do tej samej firmy co konto, a firma musi wcześniej przyjąć zaproszenie (inaczej 400). Pusta lista odpina wszystkich.</para>
    /// <para>Relacja jest wiele-do-wielu: jeden manager może obsługiwać kilka kont, a konto - kilku managerów.</para>
    /// </remarks>
    /// <param name="id">Identyfikator konta w systemie (GUID).</param>
    /// <param name="dto">Pełna lista identyfikatorów managerów.</param>
    /// <response code="200">Konto po zmianie przypisań.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    /// <response code="403">Zalogowany użytkownik nie ma roli CompanyUser.</response>
    /// <response code="404">Konto lub któryś z managerów nie istnieje w tej firmie.</response>
    [HttpPut("{id:guid}/managers")]
    [Authorize(Roles = Roles.Company)]
    [ProducesResponseType<SocialAccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SocialAccountDto>> AssignManagers(Guid id, AssignManagersDto dto, CancellationToken ct) =>
        Ok(await service.AssignManagersAsync(UserId, id, dto, ct));

    /// <summary>Zaproś firmę do zarządzania kontem (klient)</summary>
    /// <remarks>Wysyła zaproszenie do firmy o podanym adresie e-mail. Dozwolone dla konta oczekującego (zmiana firmy) lub odrzuconego (ponowne zaproszenie); konta już zarządzanego przez firmę nie można przekazać innej.</remarks>
    /// <param name="id">Identyfikator konta w systemie (GUID).</param>
    /// <param name="dto">Adres e-mail konta firmy.</param>
    /// <response code="200">Konto ze statusem zaproszenia Invited.</response>
    /// <response code="404">Konto (nie własne) lub firma nie istnieje.</response>
    /// <response code="409">Konto jest już zarządzane przez firmę.</response>
    [HttpPost("{id:guid}/invite")]
    [Authorize(Roles = Roles.Client)]
    [ProducesResponseType<SocialAccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SocialAccountDto>> Invite(Guid id, InviteCompanyDto dto, CancellationToken ct) =>
        Ok(await service.InviteAsync(UserId, id, dto, ct));

    /// <summary>Przyjmij zaproszenie do zarządzania kontem (firma)</summary>
    /// <remarks>Firma zaczyna zarządzać kontem klienta (managementStatus = Accepted); powstaje relacja firma - klient (jeśli jej nie było), z domyślnym trybem akceptacji postów Manager.</remarks>
    /// <param name="id">Identyfikator konta w systemie (GUID).</param>
    /// <response code="200">Konto po przyjęciu zaproszenia.</response>
    /// <response code="404">Nie ma takiego zaproszenia dla tej firmy.</response>
    [HttpPost("{id:guid}/accept")]
    [Authorize(Roles = Roles.Company)]
    [ProducesResponseType<SocialAccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SocialAccountDto>> Accept(Guid id, CancellationToken ct) =>
        Ok(await service.AcceptInvitationAsync(UserId, id, ct));

    /// <summary>Odrzuć zaproszenie do zarządzania kontem (firma)</summary>
    /// <remarks>Konto dostaje status Declined i znika z widoków firmy; klient widzi odrzucenie i może zaprosić ponownie.</remarks>
    /// <param name="id">Identyfikator konta w systemie (GUID).</param>
    /// <response code="200">Konto po odrzuceniu zaproszenia.</response>
    /// <response code="404">Nie ma takiego zaproszenia dla tej firmy.</response>
    [HttpPost("{id:guid}/decline")]
    [Authorize(Roles = Roles.Company)]
    [ProducesResponseType<SocialAccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SocialAccountDto>> Decline(Guid id, CancellationToken ct) =>
        Ok(await service.DeclineInvitationAsync(UserId, id, ct));

    /// <summary>Usuń konto społecznościowe</summary>
    /// <remarks>Usuwa własne konto klienta i odpina jego managerów (sami managerowie pozostają). Konta, do którego są przypisane posty, nie można usunąć - najpierw zmień konto docelowe tych postów lub je usuń. Operacji nie można cofnąć.</remarks>
    /// <param name="id">Identyfikator konta w systemie (GUID).</param>
    /// <response code="204">Konto zostało usunięte.</response>
    /// <response code="404">Konto o podanym identyfikatorze nie istnieje.</response>
    /// <response code="409">Do konta są przypisane posty.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Client)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(UserId, id, ct);
        return NoContent();
    }
}
