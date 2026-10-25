namespace SocialContent.Api.Domain;

/// <summary>
/// Konto w mediach społecznościowych (Facebook, Instagram, TikTok).
/// Relacje z diagramu: 1 - n ManagerUser (managerowie konta) oraz 1 - n Post (konto docelowe postów).
/// </summary>
public class SocialAccount
{
    public Guid Id { get; private set; }

    /// <summary>Identyfikator konta nadawany przez użytkownika systemu; unikalny w obrębie platformy.</summary>
    public string AccountId { get; private set; } = null!;
    public SocialPlatform Platform { get; private set; }

    /// <summary>Identyfikator konta po stronie platformy (np. id strony Facebooka).</summary>
    public string ExternalPlatformId { get; private set; } = null!;
    public string AccountName { get; private set; } = null!;

    /// <summary>Sekret - nigdy nie jest zwracany przez API.</summary>
    public string AccessToken { get; private set; } = null!;

    /// <summary>Sekret - nigdy nie jest zwracany przez API.</summary>
    public string? RefreshToken { get; private set; }
    public DateTime TokenExpiresAt { get; private set; }
    public bool IsActive { get; private set; }
    public string? ErrorMessage { get; private set; }

    public List<ManagerUser> ManagerUsers { get; private set; } = new();
    public List<Post> Posts { get; private set; } = new();

    /// <summary>Zdjęcia z postów kierowanych na to konto (wyliczane, nie przechowywane osobno).</summary>
    public IReadOnlyList<Picture> Pictures => Posts.SelectMany(p => p.Pictures).ToList();

    /// <summary>Wideo z postów kierowanych na to konto (wyliczane, nie przechowywane osobno).</summary>
    public IReadOnlyList<Video> Videos => Posts.SelectMany(p => p.Videos).ToList();

    private SocialAccount() { }

    public static SocialAccount Create(string accountId, SocialPlatform platform, string externalPlatformId,
        string accountName, string accessToken, string? refreshToken, DateTime tokenExpiresAt, bool isActive,
        IEnumerable<ManagerUser> managers)
    {
        var account = new SocialAccount { Id = Guid.NewGuid() };
        account.Apply(accountId, platform, externalPlatformId, accountName, accessToken, refreshToken,
            tokenExpiresAt, isActive, managers);
        return account;
    }

    /// <param name="accessToken">null = bez zmian.</param>
    /// <param name="refreshToken">null = bez zmian.</param>
    public void Update(string accountId, SocialPlatform platform, string externalPlatformId, string accountName,
        string? accessToken, string? refreshToken, DateTime tokenExpiresAt, bool isActive,
        IEnumerable<ManagerUser> managers) =>
        Apply(accountId, platform, externalPlatformId, accountName, accessToken ?? AccessToken,
            refreshToken ?? RefreshToken, tokenExpiresAt, isActive, managers);

    /// <summary>
    /// Lokalna kontrola połączenia: konto aktywne, token dostępu obecny i niewygasły.
    /// Zapisuje przyczynę w ErrorMessage (lub czyści ją). Nie wykonuje żądań do platformy.
    /// </summary>
    public bool VerifyConnection()
    {
        if (!IsActive)
        {
            ErrorMessage = "Konto jest nieaktywne.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(AccessToken))
        {
            ErrorMessage = "Brak tokenu dostępu.";
            return false;
        }
        if (TokenExpiresAt <= DateTime.UtcNow)
        {
            ErrorMessage = "Token dostępu wygasł.";
            return false;
        }
        ErrorMessage = null;
        return true;
    }

    private void Apply(string accountId, SocialPlatform platform, string externalPlatformId, string accountName,
        string accessToken, string? refreshToken, DateTime tokenExpiresAt, bool isActive,
        IEnumerable<ManagerUser> managers)
    {
        // Najpierw cała walidacja, dopiero potem zmiana stanu.
        var id = Rules.Text(accountId, "AccountId", 100);
        var externalId = Rules.Text(externalPlatformId, "ExternalPlatformId", 100);
        var name = Rules.Text(accountName, "AccountName", 150);
        var access = Rules.Text(accessToken, "AccessToken", 2000);
        var refresh = Rules.OptionalText(refreshToken, "RefreshToken", 2000);
        if (!Validators.ValidDateFormat(tokenExpiresAt))
            throw new DomainValidationException("Nieprawidłowa data wygaśnięcia tokenu (TokenExpiresAt).");

        AccountId = id;
        Platform = platform;
        ExternalPlatformId = externalId;
        AccountName = name;
        AccessToken = access;
        RefreshToken = refresh;
        TokenExpiresAt = Rules.ToUtc(tokenExpiresAt);
        IsActive = isActive;

        ManagerUsers.Clear(); // odpięci managerowie dostają SocialAccountId = null (relacja opcjonalna)
        ManagerUsers.AddRange(managers.DistinctBy(m => m.Id));
    }
}
