using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Controllers;

/// <summary>Rejestracja (firma, klient) i logowanie wszystkich ról (JWT).</summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService service) : ControllerBase
{
    /// <summary>Zarejestruj firmę (CompanyUser)</summary>
    /// <remarks>
    /// <para>Tworzy konto administratora firmy (nazwa firmy, e-mail, hasło) i od razu go loguje. CompanyUser rejestruje się jako pierwszy: dopiero z jego konta można tworzyć managerów i konta społecznościowe. Managerowie nie rejestrują się sami, tworzy ich firma przez POST /api/ManagerUsers.</para>
    /// <para>Adres e-mail jest identyfikatorem logowania i musi być unikalny we wszystkich rolach (firma, manager, klient); wielkość liter nie ma znaczenia. Hasło musi mieć 8-128 znaków, w tym małą literę, wielką literę i cyfrę. Jest przechowywane wyłącznie jako hasz PBKDF2 i nigdy nie jest zwracane.</para>
    /// <para>W odpowiedzi dostajesz token JWT (accessToken) do przekazywania w nagłówku Authorization: Bearer oraz profil z rolą CompanyUser.</para>
    /// </remarks>
    /// <param name="dto">Nazwa firmy, e-mail i hasło.</param>
    /// <response code="201">Konto firmy zostało utworzone; w odpowiedzi token i profil.</response>
    /// <response code="400">Dane nie spełniają reguł walidacji (np. słabe hasło, błędny e-mail, pusta nazwa firmy).</response>
    /// <response code="409">Konto z tym adresem e-mail już istnieje.</response>
    [HttpPost("register-company")]
    [ProducesResponseType<AuthResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponseDto>> RegisterCompany(RegisterCompanyDto dto, CancellationToken ct)
    {
        var result = await service.RegisterCompanyAsync(dto, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Zarejestruj klienta (ClientUser)</summary>
    /// <remarks>
    /// <para>Klient rejestruje się samodzielnie (imię, nazwisko, wiek, opis, e-mail, hasło) i od razu jest logowany. Po rejestracji nie jest powiązany z żadną firmą - powiązanie tworzy firma przez POST /api/ClientUsers, podając adres e-mail klienta.</para>
    /// <para>Reguły adresu e-mail i hasła są takie same jak przy rejestracji firmy. Wymagane pola: firstName i lastName (1-50 znaków; litery, myślnik, apostrof), age (18-100), description (1-2000 znaków).</para>
    /// </remarks>
    /// <param name="dto">Dane klienta oraz e-mail i hasło.</param>
    /// <response code="201">Konto klienta zostało utworzone; w odpowiedzi token i profil.</response>
    /// <response code="400">Dane nie spełniają reguł walidacji.</response>
    /// <response code="409">Konto z tym adresem e-mail już istnieje.</response>
    [HttpPost("register-client")]
    [ProducesResponseType<AuthResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponseDto>> RegisterClient(RegisterClientDto dto, CancellationToken ct)
    {
        var result = await service.RegisterClientAsync(dto, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Zaloguj się</summary>
    /// <remarks>
    /// <para>Logowanie adresem e-mail i hasłem dla każdej roli (CompanyUser, ManagerUser, ClientUser). Zwraca token JWT, który zawiera rolę, oraz profil zalogowanego.</para>
    /// <para>Przy błędnym e-mailu lub haśle zwracany jest ten sam komunikat, aby nie ujawniać, czy konto istnieje. Managerowie utworzeni bez e-maila i hasła nie mają danych logowania i nie mogą się zalogować. Zablokowane konto dostaje 401 z komunikatem o blokadzie (po podaniu poprawnego hasła).</para>
    /// </remarks>
    /// <param name="dto">Adres e-mail i hasło.</param>
    /// <response code="200">Logowanie udane; w odpowiedzi token i profil.</response>
    /// <response code="400">Brakuje e-maila lub hasła.</response>
    /// <response code="401">Nieprawidłowy adres e-mail lub hasło albo konto jest zablokowane.</response>
    [HttpPost("login")]
    [ProducesResponseType<AuthResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto, CancellationToken ct) =>
        Ok(await service.LoginAsync(dto, ct));

    /// <summary>Pobierz profil zalogowanego użytkownika</summary>
    /// <remarks>Zwraca rolę (CompanyUser, ManagerUser lub ClientUser), adres e-mail i dane profilu na podstawie tokenu. Frontend używa go do odtworzenia sesji po ponownym uruchomieniu aplikacji.</remarks>
    /// <response code="200">Profil zalogowanego użytkownika.</response>
    /// <response code="401">Brak tokenu, token nieprawidłowy lub wygasły albo konto już nie istnieje lub jest zablokowane.</response>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<UserProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileDto>> Me(CancellationToken ct) =>
        Ok(await service.GetProfileAsync(User.ToCurrentUser(), ct));
}
