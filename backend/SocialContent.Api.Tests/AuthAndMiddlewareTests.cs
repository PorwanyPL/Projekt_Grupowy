using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SocialContent.Api.Data;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Dtos;
using SocialContent.Api.Middleware;
using SocialContent.Api.Services;

namespace SocialContent.Api.Tests;

public class AuthAndMiddlewareTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ExceptionMiddleware_MapsConflictTo409WithMessage()
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionMiddleware(
            _ => throw new ConflictException(ManagerUserService.DeleteConflictMessage),
            NullLogger<ExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        // Porównujemy po sparsowaniu JSON-a, bo polskie znaki są w surowym tekście zapisane jako \uXXXX.
        Assert.Contains("zablokuj dostęp zamiast usuwać", json.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ExceptionMiddleware_MapsForbiddenTo403()
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionMiddleware(
            _ => throw new ForbiddenException("Brak dostępu"), NullLogger<ExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    private AuthService Auth(AppDbContext db) =>
        new(db, new Pbkdf2PasswordHasher(), new JwtTokenService(Options.Create(new JwtOptions
        {
            Key = new string('k', 40), Issuer = "test", Audience = "test", ExpiresMinutes = 60
        })), _db.Mapper);

    [Fact]
    public async Task RegisterCompany_ThenLogin_ReturnsCompanyRoleAndToken()
    {
        await using (var db = _db.NewContext())
        {
            var result = await Auth(db).RegisterCompanyAsync(
                new RegisterCompanyDto { Name = "ACME", Email = "Admin@Acme.com", Password = "Haslo1234" }, CancellationToken.None);
            Assert.Equal(nameof(UserRole.CompanyUser), result.User.Role.ToString());
            Assert.False(string.IsNullOrEmpty(result.AccessToken));
        }

        await using var loginDb = _db.NewContext();
        var login = await Auth(loginDb).LoginAsync(new LoginDto { Email = "admin@acme.com", Password = "Haslo1234" }, CancellationToken.None);
        Assert.Equal(nameof(UserRole.CompanyUser), login.User.Role.ToString());
    }

    [Fact]
    public async Task RegisterClient_CreatesClientWithoutCompany_AndEmailIsGloballyUnique()
    {
        await using (var db = _db.NewContext())
        {
            var result = await Auth(db).RegisterClientAsync(new RegisterClientDto
            {
                FirstName = "Karol", LastName = "Klient", Age = 33, Description = "Opis",
                Email = "klient@example.com", Password = "Haslo1234"
            }, CancellationToken.None);
            Assert.Equal(nameof(UserRole.ClientUser), result.User.Role.ToString());
        }

        // Ten sam e-mail nie może być użyty przez firmę.
        await using var again = _db.NewContext();
        await Assert.ThrowsAsync<ConflictException>(() => Auth(again).RegisterCompanyAsync(
            new RegisterCompanyDto { Name = "Firma", Email = "klient@example.com", Password = "Haslo1234" }, CancellationToken.None));
    }

    [Fact]
    public async Task Login_BlockedManager_IsRejectedOnlyAfterCorrectPassword()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var company = await _db.AddCompanyAsync();
        var manager = await _db.AddManagerAsync(company.CompanyId);
        await _db.AddCredentialAsync(manager.Id, "anna@example.com", hasher.Hash("Haslo1234"));

        await using (var db = _db.NewContext())
            await new ManagerUserService(db, _db.Mapper, hasher)
                .SetAccessAsync(company.CompanyId, manager.Id, true, CancellationToken.None);

        await using var authDb = _db.NewContext();
        var auth = Auth(authDb);

        var blocked = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            auth.LoginAsync(new LoginDto { Email = "anna@example.com", Password = "Haslo1234" }, CancellationToken.None));
        Assert.Contains("zablokowany", blocked.Message);

        var wrong = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            auth.LoginAsync(new LoginDto { Email = "anna@example.com", Password = "Zle12345" }, CancellationToken.None));
        Assert.DoesNotContain("zablokowany", wrong.Message);
    }

    [Fact]
    public async Task IsSessionValid_FalseForBlockedOrMissingAccounts()
    {
        var company = await _db.AddCompanyAsync();
        var manager = await _db.AddManagerAsync(company.CompanyId);
        await _db.AddCredentialAsync(manager.Id, "anna@example.com");

        await using var db = _db.NewContext();
        var auth = Auth(db);
        Assert.True(await auth.IsSessionValidAsync(new CurrentUser(company.CompanyId, UserRole.CompanyUser), CancellationToken.None));
        Assert.True(await auth.IsSessionValidAsync(new CurrentUser(manager.Id, UserRole.ManagerUser), CancellationToken.None));
        Assert.False(await auth.IsSessionValidAsync(new CurrentUser(Guid.NewGuid(), UserRole.ClientUser), CancellationToken.None));
    }
}
