using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Controllers;

/// <summary>Relacja firma - klient (n - m). Powstaje tylko po przyjęciu zaproszenia klienta (firmy nie dodają klientów same). Firma i jej managerowie widzą klientów (z pseudonimami nadanymi przez firmę), klient widzi swoje firmy i wybiera tryb akceptacji postów.</summary>
[ApiController]
[Route("api/[controller]")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class ClientUsersController(IClientUserService service) : ControllerBase
{
    /// <summary>Pobierz klientów firmy</summary>
    /// <remarks>
    /// <para>CompanyUser dostaje klientów powiązanych z firmą oraz tych, którzy czekają z zaproszeniem (pendingInvitations), a ManagerUser - tylko klientów, do których kont jest przypisany (bez adresów e-mail). Pole displayName to pseudonim nadany przez firmę albo imię i nazwisko; wynik jest alfabetyczny.</para>
    /// </remarks>
    /// <response code="200">Lista klientów firmy; pusta tablica, gdy nikogo nie powiązano.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    /// <response code="403">Zalogowany użytkownik jest klientem.</response>
    [HttpGet]
    [Authorize(Roles = Roles.CompanyOrManager)]
    [ProducesResponseType<List<ClientUserDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ClientUserDto>>> GetForCompany(CancellationToken ct)
    {
        var user = User.ToCurrentUser();
        return Ok(user.IsCompany
            ? await service.GetForCompanyAsync(user.Id, ct)
            : await service.GetForManagerAsync(user.Id, ct));
    }

    /// <summary>Odepnij klienta od firmy</summary>
    /// <remarks>Usuwa powiązanie firma - klient; konto klienta zostaje, a powiązania z innymi firmami nie są zmieniane. Klienta, którego konta firma nadal obsługuje, nie można odpiąć (409).</remarks>
    /// <param name="id">Identyfikator klienta (GUID).</param>
    /// <response code="204">Powiązanie zostało usunięte.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    /// <response code="403">Zalogowany użytkownik nie ma roli CompanyUser.</response>
    /// <response code="404">Klient nie jest powiązany z tą firmą.</response>
    /// <response code="409">Firma zarządza kontami tego klienta.</response>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Company)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unlink(Guid id, CancellationToken ct)
    {
        await service.UnlinkAsync(User.ToCurrentUser().Id, id, ct);
        return NoContent();
    }

    /// <summary>Pobierz firmy zalogowanego klienta</summary>
    /// <remarks>Zwraca firmy, z którymi współpracuje zalogowany klient (relacja powstaje po przyjęciu zaproszenia do zarządzania kontem), wraz z wybranym trybem akceptacji postów. Tylko dla ClientUser.</remarks>
    /// <response code="200">Lista firm; pusta tablica, gdy żadna firma jeszcze nie dodała klienta.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo konto nie istnieje.</response>
    /// <response code="403">Zalogowany użytkownik nie ma roli ClientUser.</response>
    [HttpGet("me/companies")]
    [Authorize(Roles = Roles.Client)]
    [ProducesResponseType<List<CompanyDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CompanyDto>>> MyCompanies(CancellationToken ct) =>
        Ok(await service.GetCompaniesAsync(User.ToCurrentUser().Id, ct));

    /// <summary>Nadaj klientowi pseudonim (firma)</summary>
    /// <remarks>Pseudonim jest widoczny tylko w widokach tej firmy i jej managerów (listy klientów i kont). Pusty usuwa pseudonim.</remarks>
    /// <param name="id">Identyfikator klienta (GUID).</param>
    /// <param name="dto">Pseudonim (do 100 znaków).</param>
    /// <response code="200">Klient z nowym pseudonimem.</response>
    /// <response code="404">Klient nie jest powiązany z tą firmą.</response>
    [HttpPut("{id:guid}/alias")]
    [Authorize(Roles = Roles.Company)]
    [ProducesResponseType<ClientUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientUserDto>> SetAlias(Guid id, ClientAliasDto dto, CancellationToken ct) =>
        Ok(await service.SetAliasAsync(User.ToCurrentUser().Id, id, dto, ct));

    /// <summary>Wybierz, kto akceptuje posty (klient)</summary>
    /// <remarks>Dla wskazanej firmy klient wybiera: Client - sam akceptuje każdy post przed publikacją (post trafia w status AwaitingApproval), Manager - powierza to menedżerowi/firmie (posty publikują się bez jego zgody). Dotyczy postów tworzonych od tej chwili.</remarks>
    /// <param name="companyId">Identyfikator firmy (GUID).</param>
    /// <param name="dto">Tryb akceptacji.</param>
    /// <response code="200">Firma z zapisanym trybem.</response>
    /// <response code="404">Klient nie współpracuje z tą firmą.</response>
    [HttpPut("me/companies/{companyId:guid}/approval")]
    [Authorize(Roles = Roles.Client)]
    [ProducesResponseType<CompanyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyDto>> SetApprovalMode(Guid companyId, ApprovalModeDto dto, CancellationToken ct) =>
        Ok(await service.SetApprovalModeAsync(User.ToCurrentUser().Id, companyId, dto.Mode!.Value, ct));
}
