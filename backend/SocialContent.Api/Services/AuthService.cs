using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Domain.ValueObjects;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterCompanyAsync(RegisterCompanyDto dto, CancellationToken ct);
    Task<AuthResponseDto> RegisterClientAsync(RegisterClientDto dto, CancellationToken ct);
    Task<AuthResponseDto> LoginAsync(LoginDto dto, CancellationToken ct);
    Task<UserProfileDto> GetProfileAsync(CurrentUser user, CancellationToken ct);

    /// <summary>Drugi krok logowania z weryfikacją dwuetapową: kod z e-maila.</summary>
    Task<AuthResponseDto> VerifyCodeAsync(VerifyCodeDto dto, CancellationToken ct);

    /// <summary>Włącza lub wyłącza weryfikację dwuetapową (po potwierdzeniu hasłem).</summary>
    Task<UserProfileDto> SetTwoFactorAsync(CurrentUser user, TwoFactorSettingDto dto, CancellationToken ct);

    /// <summary>Czy konto z tokenu nadal istnieje i nie jest zablokowane (sprawdzane przy każdym żądaniu).</summary>
    Task<bool> IsSessionValidAsync(CurrentUser user, CancellationToken ct);
}

public class AuthService(AppDbContext db, IPasswordHasher hasher, ITokenService tokens, IMapper mapper,
    ITwoFactorService twoFactor) : IAuthService
{
    private const string InvalidCredentials = "Nieprawidłowy adres e-mail lub hasło.";
    private const string EmailTaken = "Konto z tym adresem e-mail już istnieje.";

    // Hasz-wabik: gdy e-maila nie ma w bazie, i tak wykonujemy weryfikację hasła,
    // żeby czas odpowiedzi nie zdradzał, czy konto istnieje.
    private static readonly string DummyHash =
        $"v1.{Pbkdf2PasswordHasher.Iterations}.{Convert.ToBase64String(new byte[16])}.{Convert.ToBase64String(new byte[32])}";

    public async Task<AuthResponseDto> RegisterCompanyAsync(RegisterCompanyDto dto, CancellationToken ct)
    {
        var email = ValidateCredentials(dto.Email, dto.Password);
        var company = CompanyUser.Create(dto.Name, email, hasher.Hash(dto.Password));
        await EnsureEmailFreeAsync(email, ct);

        db.CompanyUsers.Add(company);
        await SaveRegistrationAsync(ct);

        var (token, expiresAt) = tokens.Create(company.CompanyId, email, UserRole.CompanyUser);
        return new AuthResponseDto(token, "Bearer", expiresAt, mapper.Map<UserProfileDto>(company));
    }

    public async Task<AuthResponseDto> RegisterClientAsync(RegisterClientDto dto, CancellationToken ct)
    {
        var email = ValidateCredentials(dto.Email, dto.Password);
        var client = ClientUser.Create(
            FirstName.Create(dto.FirstName),
            LastName.Create(dto.LastName),
            Age.Create(dto.Age),
            Description.Create(dto.Description),
            email,
            hasher.Hash(dto.Password));
        await EnsureEmailFreeAsync(email, ct);

        db.ClientUsers.Add(client);
        await SaveRegistrationAsync(ct);

        var (token, expiresAt) = tokens.Create(client.Id, email, UserRole.ClientUser);
        return new AuthResponseDto(token, "Bearer", expiresAt, mapper.Map<UserProfileDto>(client));
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto, CancellationToken ct)
    {
        var email = Validate.NormalizeEmail(dto.Email);

        // E-mail jest unikalny we wszystkich rolach, więc co najwyżej jedno z poniższych jest znalezione.
        var company = await db.CompanyUsers.FirstOrDefaultAsync(c => c.Email == email, ct);
        var manager = company is null
            ? await db.ManagerCredentials.Include(c => c.ManagerUser).FirstOrDefaultAsync(c => c.Email == email, ct)
            : null;
        var client = company is null && manager is null
            ? await db.ClientUsers.FirstOrDefaultAsync(c => c.Email == email, ct)
            : null;

        var hash = company?.PasswordHash ?? manager?.PasswordHash ?? client?.PasswordHash ?? DummyHash;
        var passwordOk = hasher.Verify(dto.Password, hash);
        if (!passwordOk || (company is null && manager is null && client is null))
            throw new UnauthorizedException(InvalidCredentials);

        // Komunikat o blokadzie pokazujemy dopiero po poprawnym haśle, żeby nie ujawniać istnienia kont.
        if (company?.IsAccessBlocked == true || manager?.IsAccessBlocked == true)
            throw new UnauthorizedException("Dostęp tego konta został zablokowany. Skontaktuj się z administratorem.");

        if (company is not null)
            return await FinishLoginAsync(company.CompanyId, UserRole.CompanyUser, company.Email, company.TwoFactorEnabled,
                mapper.Map<UserProfileDto>(company), ct);
        if (manager is not null)
            return await FinishLoginAsync(manager.ManagerUserId, UserRole.ManagerUser, manager.Email, manager.TwoFactorEnabled,
                mapper.Map<UserProfileDto>(manager), ct);
        return await FinishLoginAsync(client!.Id, UserRole.ClientUser, client.Email, client.TwoFactorEnabled,
            mapper.Map<UserProfileDto>(client), ct);
    }

    /// <summary>Hasło jest już sprawdzone: bez 2FA wydaje token, z 2FA wysyła kod i zwraca wyzwanie.</summary>
    private async Task<AuthResponseDto> FinishLoginAsync(Guid id, UserRole role, string email, bool twoFactorEnabled,
        UserProfileDto profile, CancellationToken ct)
    {
        if (!twoFactorEnabled)
        {
            var (token, expiresAt) = tokens.Create(id, email, role);
            return new AuthResponseDto(token, "Bearer", expiresAt, profile);
        }

        var challengeId = await twoFactor.StartAsync(id, role, email, ct);
        return new AuthResponseDto("", "Bearer", default, null, true, challengeId, MaskEmail(email));
    }

    public async Task<AuthResponseDto> VerifyCodeAsync(VerifyCodeDto dto, CancellationToken ct)
    {
        var (id, role) = await twoFactor.VerifyAsync(dto.ChallengeId, dto.Code, ct);

        // Konto mogło zostać zablokowane lub usunięte między hasłem a kodem.
        var profile = await GetProfileAsync(new CurrentUser(id, role), ct);
        var (token, expiresAt) = tokens.Create(id, profile.Email, role);
        return new AuthResponseDto(token, "Bearer", expiresAt, profile);
    }

    public async Task<UserProfileDto> SetTwoFactorAsync(CurrentUser user, TwoFactorSettingDto dto, CancellationToken ct)
    {
        switch (user.Role)
        {
            case UserRole.CompanyUser:
            {
                var company = await db.CompanyUsers.FirstOrDefaultAsync(c => c.CompanyId == user.Id, ct)
                              ?? throw new UnauthorizedException("Konto powiązane z tokenem już nie istnieje.");
                RequirePassword(dto.Password, company.PasswordHash);
                company.SetTwoFactorEnabled(dto.Enabled);
                break;
            }
            case UserRole.ManagerUser:
            {
                var credential = await db.ManagerCredentials.FirstOrDefaultAsync(c => c.ManagerUserId == user.Id, ct)
                                 ?? throw new UnauthorizedException("Konto powiązane z tokenem już nie istnieje.");
                RequirePassword(dto.Password, credential.PasswordHash);
                credential.SetTwoFactorEnabled(dto.Enabled);
                break;
            }
            default:
            {
                var client = await db.ClientUsers.FirstOrDefaultAsync(c => c.Id == user.Id, ct)
                             ?? throw new UnauthorizedException("Konto powiązane z tokenem już nie istnieje.");
                RequirePassword(dto.Password, client.PasswordHash);
                client.SetTwoFactorEnabled(dto.Enabled);
                break;
            }
        }

        await db.SaveChangesAsync(ct);
        return await GetProfileAsync(user, ct);
    }

    // Błędne hasło to 400, nie 401: 401 po stronie klienta oznacza "sesja wygasła" i wylogowuje użytkownika.
    private void RequirePassword(string? password, string hash)
    {
        if (string.IsNullOrEmpty(password) || !hasher.Verify(password, hash))
            throw new DomainValidationException("Nieprawidłowe hasło.");
    }

    /// <summary>"kowalski@demo.pl" -> "k***@demo.pl".</summary>
    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        return at <= 0 ? "***" : email[0] + "***" + email[at..];
    }

    public async Task<UserProfileDto> GetProfileAsync(CurrentUser user, CancellationToken ct)
    {
        const string gone = "Konto powiązane z tokenem już nie istnieje.";
        const string blocked = "Dostęp tego konta został zablokowany.";

        switch (user.Role)
        {
            case UserRole.CompanyUser:
            {
                var company = await db.CompanyUsers.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == user.Id, ct)
                              ?? throw new UnauthorizedException(gone);
                if (company.IsAccessBlocked) throw new UnauthorizedException(blocked);
                return mapper.Map<UserProfileDto>(company);
            }
            case UserRole.ManagerUser:
            {
                var credential = await db.ManagerCredentials.AsNoTracking().Include(c => c.ManagerUser)
                                     .FirstOrDefaultAsync(c => c.ManagerUserId == user.Id, ct)
                                 ?? throw new UnauthorizedException(gone);
                if (credential.IsAccessBlocked) throw new UnauthorizedException(blocked);
                return mapper.Map<UserProfileDto>(credential);
            }
            default:
            {
                var client = await db.ClientUsers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == user.Id, ct)
                             ?? throw new UnauthorizedException(gone);
                return mapper.Map<UserProfileDto>(client);
            }
        }
    }

    public async Task<bool> IsSessionValidAsync(CurrentUser user, CancellationToken ct)
    {
        switch (user.Role)
        {
            case UserRole.CompanyUser:
            {
                var blocked = await db.CompanyUsers.AsNoTracking().Where(c => c.CompanyId == user.Id)
                    .Select(c => (bool?)c.IsAccessBlocked).FirstOrDefaultAsync(ct);
                return blocked == false;
            }
            case UserRole.ManagerUser:
            {
                var blocked = await db.ManagerCredentials.AsNoTracking().Where(c => c.ManagerUserId == user.Id)
                    .Select(c => (bool?)c.IsAccessBlocked).FirstOrDefaultAsync(ct);
                return blocked == false;
            }
            default:
                return await db.ClientUsers.AsNoTracking().AnyAsync(c => c.Id == user.Id, ct);
        }
    }

    /// <summary>Normalizuje i waliduje e-mail oraz hasło; zwraca znormalizowany e-mail.</summary>
    private static string ValidateCredentials(string? rawEmail, string? password)
    {
        var email = Validate.NormalizeEmail(rawEmail);
        Validate.Email(email);
        Validate.Password(password);
        return email;
    }

    private async Task EnsureEmailFreeAsync(string email, CancellationToken ct)
    {
        if (await EmailDirectory.IsTakenAsync(db, email, ct))
            throw new ConflictException(EmailTaken);
    }

    private async Task SaveRegistrationAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Wyścig dwóch rejestracji tego samego e-maila - zatrzymuje je unikalny indeks.
            throw new ConflictException(EmailTaken);
        }
    }
}
