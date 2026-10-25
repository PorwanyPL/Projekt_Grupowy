using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SocialContent.Api.Domain;
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
    public async Task Login_BlockedAccount_IsRejectedOnlyAfterCorrectPassword()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var manager = await _db.AddManagerAsync();
        await _db.AddCredentialAsync(manager.Id, "anna@example.com", hasher.Hash("Haslo1234"));

        await using (var db = _db.NewContext())
            await new ManagerUserService(db).SetAccessAsync(manager.Id, true, null, CancellationToken.None);

        await using var authDb = _db.NewContext();
        var auth = new AuthService(authDb, hasher, tokens: null!);

        var blocked = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            auth.LoginAsync(new LoginDto { Email = "anna@example.com", Password = "Haslo1234" }, CancellationToken.None));
        Assert.Contains("zablokowany", blocked.Message);

        var wrong = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            auth.LoginAsync(new LoginDto { Email = "anna@example.com", Password = "Zle12345" }, CancellationToken.None));
        Assert.DoesNotContain("zablokowany", wrong.Message);
    }
}
