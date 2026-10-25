using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Controllers;

/// <summary>Rejestracja i logowanie użytkowników typu ManagerUser (JWT).</summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService service) : ControllerBase
{
    /// <summary>Zarejestruj managera</summary>
    /// <remarks>
    /// <para>Tworzy managera (imię, nazwisko, wiek, opis) razem z kontem logowania i od razu go loguje. Identyfikatorem logowania jest adres e-mail - musi być unikalny, a wielkość liter nie ma znaczenia.</para>
    /// <para>Hasło musi mieć 8-128 znaków, w tym małą literę, wielką literę i cyfrę. Jest przechowywane wyłącznie jako hasz PBKDF2 i nigdy nie jest zwracane.</para>
    /// <para>W odpowiedzi dostajesz token JWT (accessToken) do przekazywania w nagłówku Authorization: Bearer oraz profil nowego managera.</para>
    /// </remarks>
    /// <param name="dto">Dane managera oraz e-mail i hasło.</param>
    /// <response code="201">Konto zostało utworzone; w odpowiedzi token i profil.</response>
    /// <response code="400">Dane nie spełniają reguł walidacji (np. słabe hasło, błędny e-mail, wiek poza 18-100).</response>
    /// <response code="409">Konto z tym adresem e-mail już istnieje.</response>
    [HttpPost("register")]
    [ProducesResponseType<AuthResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto, CancellationToken ct)
    {
        var result = await service.RegisterAsync(dto, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Zaloguj managera</summary>
    /// <remarks>
    /// <para>Sprawdza adres e-mail i hasło (wielkość liter w e-mailu nie ma znaczenia) i zwraca token JWT ważny przez czas z konfiguracji (domyślnie 60 minut) oraz profil managera.</para>
    /// <para>Przy błędnym e-mailu lub haśle zwracany jest ten sam komunikat, aby nie ujawniać, czy konto istnieje. Managerowie utworzeni przez POST /api/ManagerUsers nie mają danych logowania i nie mogą się zalogować.</para>
    /// </remarks>
    /// <param name="dto">Adres e-mail i hasło.</param>
    /// <response code="200">Logowanie udane; w odpowiedzi token i profil.</response>
    /// <response code="400">Brakuje e-maila lub hasła.</response>
    /// <response code="401">Nieprawidłowy adres e-mail lub hasło.</response>
    [HttpPost("login")]
    [ProducesResponseType<AuthResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto, CancellationToken ct) =>
        Ok(await service.LoginAsync(dto, ct));

    /// <summary>Pobierz profil zalogowanego managera</summary>
    /// <remarks>Wymaga nagłówka Authorization: Bearer z tokenem JWT. Zwraca dane managera, do którego należy token, wraz z jego adresem e-mail.</remarks>
    /// <response code="200">Profil zalogowanego managera.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo konto już nie istnieje.</response>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<ManagerProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ManagerProfileDto>> Me(CancellationToken ct)
    {
        var subject = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(subject, out var managerId))
            return Unauthorized();

        return Ok(await service.GetProfileAsync(managerId, ct));
    }
}
