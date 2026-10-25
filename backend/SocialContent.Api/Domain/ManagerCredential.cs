using System.Text.RegularExpressions;

namespace SocialContent.Api.Domain;

/// <summary>
/// Dane logowania użytkownika ManagerUser (relacja 1:1). Trzymane osobno, aby encja ManagerUser
/// pozostała zgodna z diagramem, a hasło nigdy nie trafiało do DTO przez przypadek.
/// </summary>
public class ManagerCredential
{
    public Guid ManagerUserId { get; private set; }
    public ManagerUser ManagerUser { get; private set; } = null!;

    /// <summary>Identyfikator logowania: adres e-mail w postaci znormalizowanej (małe litery, bez spacji).</summary>
    public string Email { get; private set; } = null!;

    /// <summary>Hasz PBKDF2 w formacie "v1.iteracje.sól.hasz". Nigdy nie opuszcza warstwy serwisów.</summary>
    public string PasswordHash { get; private set; } = null!;

    /// <summary>
    /// Blokada dostępu: manager i jego powiązania z postami zostają, ale nie może się zalogować,
    /// a wydane wcześniej tokeny przestają być akceptowane.
    /// </summary>
    public bool IsAccessBlocked { get; private set; }

    private ManagerCredential() { }

    public void SetAccessBlocked(bool blocked) => IsAccessBlocked = blocked;

    public static ManagerCredential Create(ManagerUser manager, string normalizedEmail, string passwordHash) =>
        new()
        {
            ManagerUserId = manager.Id,
            ManagerUser = manager,
            Email = normalizedEmail,
            PasswordHash = passwordHash
        };
}

public static class CredentialRules
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;
    public const int MaxEmailLength = 254;

    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    public static void ValidateEmail(string normalizedEmail)
    {
        if (normalizedEmail.Length == 0 || normalizedEmail.Length > MaxEmailLength || !EmailPattern.IsMatch(normalizedEmail))
            throw new DomainValidationException("Podaj poprawny adres e-mail (maksymalnie 254 znaki).");
    }

    public static void ValidatePassword(string? password)
    {
        var problems = new List<string>();
        var p = password ?? "";

        if (p.Length < MinPasswordLength || p.Length > MaxPasswordLength)
            problems.Add($"długość {MinPasswordLength}-{MaxPasswordLength} znaków");
        if (!p.Any(char.IsLower)) problems.Add("mała litera");
        if (!p.Any(char.IsUpper)) problems.Add("wielka litera");
        if (!p.Any(char.IsDigit)) problems.Add("cyfra");

        if (problems.Count > 0)
            throw new DomainValidationException($"Hasło nie spełnia wymagań. Wymagane: {string.Join(", ", problems)}.");
    }
}
