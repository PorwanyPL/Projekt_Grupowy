using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SocialContent.Api.Data;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Tests;

/// <summary>Zapamiętuje wysłane wiadomości, żeby test mógł odczytać kod.</summary>
public sealed class CapturingEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body)> Sent { get; } = new();

    public Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        Sent.Add((to, subject, body));
        return Task.CompletedTask;
    }

    /// <summary>Kod z ostatniej wiadomości ("Twój kod logowania: 123456. ...").</summary>
    public string LastCode()
    {
        var body = Sent.Last().Body;
        const string marker = "kod logowania: ";
        var start = body.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        return body.Substring(start, TwoFactorService.CodeLength);
    }
}

public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}

public class TwoFactorTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly CapturingEmailSender _mail = new();
    private readonly FakeTimeProvider _time = new();

    public void Dispose() => _db.Dispose();

    private AuthService Auth(AppDbContext db)
    {
        var hasher = new Pbkdf2PasswordHasher();
        var tokens = new JwtTokenService(Options.Create(new JwtOptions
        {
            Key = new string('k', 40), Issuer = "test", Audience = "test", ExpiresMinutes = 60
        }));
        return new AuthService(db, hasher, tokens, _db.Mapper, new TwoFactorService(db, hasher, _mail, _time));
    }

    private const string Password = "Haslo1234";

    private async Task<Guid> RegisterClientAsync(string email, bool twoFactor)
    {
        await using var db = _db.NewContext();
        var result = await Auth(db).RegisterClientAsync(new RegisterClientDto
        {
            FirstName = "Karol", LastName = "Klient", Age = 40, Description = "Opis", Email = email, Password = Password
        }, CancellationToken.None);
        var id = result.User!.Id;
        if (twoFactor)
            await Auth(db).SetTwoFactorAsync(new CurrentUser(id, UserRole.ClientUser),
                new TwoFactorSettingDto { Enabled = true, Password = Password }, CancellationToken.None);
        return id;
    }

    private async Task<AuthResponseDto> LoginAsync(string email)
    {
        await using var db = _db.NewContext();
        return await Auth(db).LoginAsync(new LoginDto { Email = email, Password = Password }, CancellationToken.None);
    }

    private async Task<AuthResponseDto> VerifyAsync(Guid challengeId, string code)
    {
        await using var db = _db.NewContext();
        return await Auth(db).VerifyCodeAsync(new VerifyCodeDto { ChallengeId = challengeId, Code = code }, CancellationToken.None);
    }

    [Fact]
    public async Task Registration_DoesNotRequireVerification_AndTwoFactorIsOffByDefault()
    {
        await RegisterClientAsync("a@example.com", twoFactor: false);

        var login = await LoginAsync("a@example.com");

        Assert.False(login.TwoFactorRequired);
        Assert.False(string.IsNullOrEmpty(login.AccessToken));
        Assert.False(login.User!.TwoFactorEnabled);
        Assert.Empty(_mail.Sent);
    }

    [Fact]
    public async Task Login_WithTwoFactor_ReturnsChallengeInsteadOfToken_AndSendsCode()
    {
        await RegisterClientAsync("b@example.com", twoFactor: true);

        var login = await LoginAsync("b@example.com");

        Assert.True(login.TwoFactorRequired);
        Assert.Equal("", login.AccessToken);
        Assert.Null(login.User);
        Assert.NotNull(login.ChallengeId);
        Assert.Equal("b***@example.com", login.MaskedEmail);
        var mail = Assert.Single(_mail.Sent);
        Assert.Equal("b@example.com", mail.To);
        Assert.Matches(@"^\d{6}$", _mail.LastCode());
    }

    [Fact]
    public async Task WrongPassword_DoesNotSendCode()
    {
        await RegisterClientAsync("c@example.com", twoFactor: true);
        await using var db = _db.NewContext();

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            Auth(db).LoginAsync(new LoginDto { Email = "c@example.com", Password = "Zle12345" }, CancellationToken.None));
        Assert.Empty(_mail.Sent);
    }

    [Fact]
    public async Task VerifyCode_Correct_ReturnsTokenAndProfile_AndCodeIsSingleUse()
    {
        await RegisterClientAsync("d@example.com", twoFactor: true);
        var login = await LoginAsync("d@example.com");
        var code = _mail.LastCode();

        var result = await VerifyAsync(login.ChallengeId!.Value, code);

        Assert.False(string.IsNullOrEmpty(result.AccessToken));
        Assert.Equal("d@example.com", result.User!.Email);
        Assert.True(result.User.TwoFactorEnabled);
        await Assert.ThrowsAsync<UnauthorizedException>(() => VerifyAsync(login.ChallengeId.Value, code));
    }

    [Fact]
    public async Task Code_IsStoredOnlyAsHash()
    {
        await RegisterClientAsync("e@example.com", twoFactor: true);
        await LoginAsync("e@example.com");
        var code = _mail.LastCode();

        await using var db = _db.NewContext();
        var challenge = await db.TwoFactorChallenges.SingleAsync();
        Assert.DoesNotContain(code, challenge.CodeHash);
        Assert.StartsWith("v1.", challenge.CodeHash);
    }

    [Fact]
    public async Task VerifyCode_Expired_AfterFiveMinutes()
    {
        await RegisterClientAsync("f@example.com", twoFactor: true);
        var login = await LoginAsync("f@example.com");
        var code = _mail.LastCode();

        _time.Advance(TimeSpan.FromMinutes(TwoFactorService.ValidMinutes) + TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<UnauthorizedException>(() => VerifyAsync(login.ChallengeId!.Value, code));
    }

    [Fact]
    public async Task VerifyCode_StillValidJustBeforeExpiry()
    {
        await RegisterClientAsync("g@example.com", twoFactor: true);
        var login = await LoginAsync("g@example.com");
        var code = _mail.LastCode();

        _time.Advance(TimeSpan.FromMinutes(TwoFactorService.ValidMinutes) - TimeSpan.FromSeconds(1));

        Assert.False(string.IsNullOrEmpty((await VerifyAsync(login.ChallengeId!.Value, code)).AccessToken));
    }

    [Fact]
    public async Task VerifyCode_FiveWrongAttempts_InvalidateChallengeEvenForCorrectCode()
    {
        await RegisterClientAsync("h@example.com", twoFactor: true);
        var login = await LoginAsync("h@example.com");
        var code = _mail.LastCode();
        var wrong = code == "000000" ? "111111" : "000000";

        for (var i = 0; i < TwoFactorService.MaxAttempts; i++)
            await Assert.ThrowsAsync<UnauthorizedException>(() => VerifyAsync(login.ChallengeId!.Value, wrong));

        await Assert.ThrowsAsync<UnauthorizedException>(() => VerifyAsync(login.ChallengeId!.Value, code));
        await using var db = _db.NewContext();
        Assert.Empty(await db.TwoFactorChallenges.ToListAsync());
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    [InlineData("")]
    public async Task VerifyCode_MalformedCode_IsRejectedAndCountsAsAttempt(string bad)
    {
        await RegisterClientAsync("i@example.com", twoFactor: true);
        var login = await LoginAsync("i@example.com");

        await Assert.ThrowsAsync<UnauthorizedException>(() => VerifyAsync(login.ChallengeId!.Value, bad));

        await using var db = _db.NewContext();
        Assert.Equal(1, (await db.TwoFactorChallenges.SingleAsync()).FailedAttempts);
    }

    [Fact]
    public async Task NewLogin_InvalidatesPreviousCode()
    {
        await RegisterClientAsync("j@example.com", twoFactor: true);
        var first = await LoginAsync("j@example.com");
        var firstCode = _mail.LastCode();
        var second = await LoginAsync("j@example.com");

        await Assert.ThrowsAsync<UnauthorizedException>(() => VerifyAsync(first.ChallengeId!.Value, firstCode));
        Assert.False(string.IsNullOrEmpty((await VerifyAsync(second.ChallengeId!.Value, _mail.LastCode())).AccessToken));
    }

    [Fact]
    public async Task VerifyCode_UnknownChallenge_IsUnauthorized() =>
        await Assert.ThrowsAsync<UnauthorizedException>(() => VerifyAsync(Guid.NewGuid(), "123456"));

    [Fact]
    public async Task SetTwoFactor_RequiresCorrectPassword_AndCanBeTurnedOff()
    {
        var id = await RegisterClientAsync("k@example.com", twoFactor: true);
        var me = new CurrentUser(id, UserRole.ClientUser);
        await using var db = _db.NewContext();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Auth(db).SetTwoFactorAsync(me, new TwoFactorSettingDto { Enabled = false, Password = "Zle12345" }, CancellationToken.None));
        Assert.True((await Auth(db).GetProfileAsync(me, CancellationToken.None)).TwoFactorEnabled);

        var profile = await Auth(db).SetTwoFactorAsync(me, new TwoFactorSettingDto { Enabled = false, Password = Password }, CancellationToken.None);
        Assert.False(profile.TwoFactorEnabled);
        Assert.False((await LoginAsync("k@example.com")).TwoFactorRequired);
    }

    [Fact]
    public async Task TwoFactor_WorksForCompanyAndManager()
    {
        await using var db = _db.NewContext();
        var auth = Auth(db);
        var company = await auth.RegisterCompanyAsync(new RegisterCompanyDto { Name = "ACME", Email = "firma@example.com", Password = Password }, CancellationToken.None);
        await auth.SetTwoFactorAsync(new CurrentUser(company.User!.Id, UserRole.CompanyUser),
            new TwoFactorSettingDto { Enabled = true, Password = Password }, CancellationToken.None);

        var login = await LoginAsync("firma@example.com");
        Assert.True(login.TwoFactorRequired);
        var done = await VerifyAsync(login.ChallengeId!.Value, _mail.LastCode());
        Assert.Equal(nameof(UserRole.CompanyUser), done.User!.Role);

        var hasher = new Pbkdf2PasswordHasher();
        var manager = await _db.AddManagerAsync(company.User.Id);
        await _db.AddCredentialAsync(manager.Id, "manager@example.com", hasher.Hash(Password));
        await auth.SetTwoFactorAsync(new CurrentUser(manager.Id, UserRole.ManagerUser),
            new TwoFactorSettingDto { Enabled = true, Password = Password }, CancellationToken.None);
        var mLogin = await LoginAsync("manager@example.com");
        Assert.True(mLogin.TwoFactorRequired);
        Assert.Equal(nameof(UserRole.ManagerUser), (await VerifyAsync(mLogin.ChallengeId!.Value, _mail.LastCode())).User!.Role);
    }
}
