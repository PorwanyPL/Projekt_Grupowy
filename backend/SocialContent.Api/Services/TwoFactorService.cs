using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;

namespace SocialContent.Api.Services;

public interface ITwoFactorService
{
    /// <summary>Tworzy wyzwanie (unieważniając poprzednie), wysyła kod na e-mail i zwraca Id wyzwania.</summary>
    Task<Guid> StartAsync(Guid userId, UserRole role, string email, CancellationToken ct);

    /// <summary>Sprawdza kod; przy sukcesie zużywa wyzwanie i zwraca konto, którego dotyczyło.</summary>
    Task<(Guid UserId, UserRole Role)> VerifyAsync(Guid challengeId, string? code, CancellationToken ct);
}

public class TwoFactorService(AppDbContext db, IPasswordHasher hasher, IEmailSender email, TimeProvider time) : ITwoFactorService
{
    public const int CodeLength = 6;
    public const int ValidMinutes = 5;
    public const int MaxAttempts = 5;

    private const string InvalidChallenge = "Kod wygasł lub jest nieprawidłowy. Zaloguj się ponownie.";

    public async Task<Guid> StartAsync(Guid userId, UserRole role, string to, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;

        // Jedno aktywne wyzwanie na konto: nowe logowanie unieważnia poprzedni kod.
        var old = await db.TwoFactorChallenges.Where(c => c.UserId == userId && c.UserRole == role).ToListAsync(ct);
        db.TwoFactorChallenges.RemoveRange(old);

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D" + CodeLength);
        var challenge = TwoFactorChallenge.Create(userId, role, hasher.Hash(code), now, TimeSpan.FromMinutes(ValidMinutes));
        db.TwoFactorChallenges.Add(challenge);
        await db.SaveChangesAsync(ct);

        await email.SendAsync(to, "Kod logowania SocialContent",
            $"Twój kod logowania: {code}. Jest ważny {ValidMinutes} minut. Jeśli to nie Ty, zignoruj tę wiadomość i zmień hasło.", ct);
        return challenge.Id;
    }

    public async Task<(Guid UserId, UserRole Role)> VerifyAsync(Guid challengeId, string? code, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var challenge = await db.TwoFactorChallenges.FirstOrDefaultAsync(c => c.Id == challengeId, ct);
        if (challenge is null) throw new UnauthorizedException(InvalidChallenge);

        if (challenge.IsExpired(now))
        {
            db.TwoFactorChallenges.Remove(challenge);
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException(InvalidChallenge);
        }

        if (challenge.FailedAttempts >= MaxAttempts)
        {
            db.TwoFactorChallenges.Remove(challenge);
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException("Przekroczono limit prób. Zaloguj się ponownie, aby dostać nowy kod.");
        }

        var trimmed = (code ?? "").Trim();
        var ok = trimmed.Length == CodeLength && trimmed.All(char.IsAsciiDigit) && hasher.Verify(trimmed, challenge.CodeHash);
        if (!ok)
        {
            challenge.RegisterFailedAttempt();
            await db.SaveChangesAsync(ct);
            var left = MaxAttempts - challenge.FailedAttempts;
            throw new UnauthorizedException(left > 0
                ? $"Nieprawidłowy kod. Pozostało prób: {left}."
                : "Nieprawidłowy kod. Przekroczono limit prób - zaloguj się ponownie.");
        }

        db.TwoFactorChallenges.Remove(challenge); // kod jest jednorazowy
        await db.SaveChangesAsync(ct);
        return (challenge.UserId, challenge.UserRole);
    }
}
