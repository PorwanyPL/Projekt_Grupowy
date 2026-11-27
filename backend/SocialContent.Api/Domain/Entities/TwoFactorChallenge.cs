using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Domain.Entities;

/// <summary>
/// Jednorazowe wyzwanie weryfikacji dwuetapowej: po poprawnym haśle powstaje wyzwanie z haszem 6-cyfrowego kodu.
/// Kod w postaci jawnej nie jest nigdzie zapisywany (trafia tylko do wiadomości e-mail).
/// </summary>
public class TwoFactorChallenge
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public UserRole UserRole { get; private set; }

    /// <summary>Hasz kodu w formacie IPasswordHasher.</summary>
    public string CodeHash { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    /// <summary>Liczba dotychczasowych błędnych prób.</summary>
    public int FailedAttempts { get; private set; }

    private TwoFactorChallenge() { }

    public static TwoFactorChallenge Create(Guid userId, UserRole role, string codeHash, DateTime nowUtc, TimeSpan lifetime) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserRole = role,
            CodeHash = codeHash,
            CreatedAt = nowUtc,
            ExpiresAt = nowUtc.Add(lifetime)
        };

    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresAt;

    public void RegisterFailedAttempt() => FailedAttempts++;
}
