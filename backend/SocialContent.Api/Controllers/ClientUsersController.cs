using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Controllers;

/// <summary>Relacja firma - klient (n - m). Firma zarządza swoimi klientami, klient widzi firmy, z którymi jest powiązany.</summary>
[ApiController]
[Route("api/[controller]")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class ClientUsersController(IClientUserService service) : ControllerBase
{
    /// <summary>Pobierz klientów firmy</summary>
    /// <remarks>Zwraca klientów powiązanych z zalogowaną firmą, alfabetycznie. Tylko dla CompanyUser.</remarks>
    /// <response code="200">Lista klientów firmy; pusta tablica, gdy nikogo nie powiązano.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    /// <response code="403">Zalogowany użytkownik nie ma roli CompanyUser.</response>
    [HttpGet]
    [Authorize(Roles = Roles.Company)]
    [ProducesResponseType<List<ClientUserDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ClientUserDto>>> GetForCompany(CancellationToken ct) =>
        Ok(await service.GetForCompanyAsync(User.ToCurrentUser().Id, ct));

    /// <summary>Powiąż klienta z firmą</summary>
    /// <remarks>Dodaje do firmy klienta, który wcześniej sam się zarejestrował (POST /api/Auth/register-client). Klienta wskazuje się adresem e-mail użytym przy rejestracji. Klient może być powiązany z wieloma firmami, a firma z wieloma klientami.</remarks>
    /// <param name="dto">Adres e-mail klienta.</param>
    /// <response code="201">Klient został powiązany z firmą.</response>
    /// <response code="400">Błędny adres e-mail.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    /// <response code="403">Zalogowany użytkownik nie ma roli CompanyUser.</response>
    /// <response code="404">Nie ma zarejestrowanego klienta o takim adresie e-mail.</response>
    /// <response code="409">Klient jest już powiązany z tą firmą.</response>
    [HttpPost]
    [Authorize(Roles = Roles.Company)]
    [ProducesResponseType<ClientUserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClientUserDto>> Link(LinkClientDto dto, CancellationToken ct)
    {
        var client = await service.LinkAsync(User.ToCurrentUser().Id, dto, ct);
        return StatusCode(StatusCodes.Status201Created, client);
    }

    /// <summary>Odepnij klienta od firmy</summary>
    /// <remarks>Usuwa powiązanie firma - klient; konto klienta zostaje, a powiązania z innymi firmami nie są zmieniane.</remarks>
    /// <param name="id">Identyfikator klienta (GUID).</param>
    /// <response code="204">Powiązanie zostało usunięte.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo dostęp konta został zablokowany.</response>
    /// <response code="403">Zalogowany użytkownik nie ma roli CompanyUser.</response>
    /// <response code="404">Klient nie jest powiązany z tą firmą.</response>
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
    /// <remarks>Zwraca firmy, z którymi powiązany jest zalogowany klient. Tylko dla ClientUser.</remarks>
    /// <response code="200">Lista firm; pusta tablica, gdy żadna firma jeszcze nie dodała klienta.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo konto nie istnieje.</response>
    /// <response code="403">Zalogowany użytkownik nie ma roli ClientUser.</response>
    [HttpGet("me/companies")]
    [Authorize(Roles = Roles.Client)]
    [ProducesResponseType<List<CompanyDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CompanyDto>>> MyCompanies(CancellationToken ct) =>
        Ok(await service.GetCompaniesAsync(User.ToCurrentUser().Id, ct));
}
