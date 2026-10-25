using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto, CancellationToken ct);
    Task<AuthResponseDto> LoginAsync(LoginDto dto, CancellationToken ct);
    Task<ManagerProfileDto> GetProfileAsync(Guid managerId, CancellationToken ct);
}

public class AuthService(AppDbContext db, IPasswordHasher hasher, ITokenService tokens) : IAuthService
{
    private const string InvalidCredentials = "Nieprawidłowy adres e-mail lub hasło.";

    // Hasz-wabik: gdy e-maila nie ma w bazie, i tak wykonujemy weryfikację hasła,
    // żeby czas odpowiedzi nie zdradzał, czy konto istnieje.
    private static readonly string DummyHash =
        $"v1.{Pbkdf2PasswordHasher.Iterations}.{Convert.ToBase64String(new byte[16])}.{Convert.ToBase64String(new byte[32])}";

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto, CancellationToken ct)
    {
        var email = CredentialRules.NormalizeEmail(dto.Email);
        CredentialRules.ValidateEmail(email);
        CredentialRules.ValidatePassword(dto.Password);

        var manager = ManagerUser.Create(
            FirstName.Create(dto.FirstName),
            LastName.Create(dto.LastName),
            Age.Create(dto.Age),
            Description.Create(dto.Description));

        if (await db.ManagerCredentials.AnyAsync(c => c.Email == email, ct))
            throw new ConflictException("Konto z tym adresem e-mail już istnieje.");

        var credential = ManagerCredential.Create(manager, email, hasher.Hash(dto.Password));

        db.ManagerUsers.Add(manager);
        db.ManagerCredentials.Add(credential);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Wyścig dwóch rejestracji tego samego e-maila - zatrzymuje je unikalny indeks.
            throw new ConflictException("Konto z tym adresem e-mail już istnieje.");
        }

        var (token, expiresAt) = tokens.Create(manager.Id, email);
        return new AuthResponseDto(token, "Bearer", expiresAt, manager.ToProfileDto(email));
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto, CancellationToken ct)
    {
        var email = CredentialRules.NormalizeEmail(dto.Email);

        var credential = await db.ManagerCredentials
            .Include(c => c.ManagerUser)
            .FirstOrDefaultAsync(c => c.Email == email, ct);

        var passwordOk = hasher.Verify(dto.Password, credential?.PasswordHash ?? DummyHash);
        if (credential is null || !passwordOk)
            throw new UnauthorizedException(InvalidCredentials);

        // Komunikat o blokadzie pokazujemy dopiero po poprawnym haśle, żeby nie ujawniać istnienia kont.
        if (credential.IsAccessBlocked)
            throw new UnauthorizedException("Dostęp tego konta został zablokowany. Skontaktuj się z administratorem.");

        var (token, expiresAt) = tokens.Create(credential.ManagerUserId, credential.Email);
        return new AuthResponseDto(token, "Bearer", expiresAt, credential.ManagerUser.ToProfileDto(credential.Email));
    }

    public async Task<ManagerProfileDto> GetProfileAsync(Guid managerId, CancellationToken ct)
    {
        var credential = await db.ManagerCredentials
            .Include(c => c.ManagerUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ManagerUserId == managerId, ct)
            ?? throw new UnauthorizedException("Konto powiązane z tokenem już nie istnieje.");

        if (credential.IsAccessBlocked)
            throw new UnauthorizedException("Dostęp tego konta został zablokowany.");

        return credential.ManagerUser.ToProfileDto(credential.Email);
    }
}
