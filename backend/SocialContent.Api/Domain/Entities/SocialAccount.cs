using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;

namespace SocialContent.Api.Domain.Entities;

/// <summary>
/// Konto w mediach społecznościowych (Facebook, Instagram, TikTok).
/// Relacje z diagramu: n - m ManagerUser (managerowie konta), 1 - n Post (konto docelowe postów)
/// oraz CompanyUser 1 - n SocialAccount (firma będąca właścicielem konta).
/// </summary>
public class SocialAccount
{
    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }
    public CompanyUser CompanyUser { get; private set; } = null!;

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

    public static SocialAccount Create(Guid companyId, string accountId, SocialPlatform platform, string externalPlatformId,
        string accountName, string accessToken, string? refreshToken, DateTime tokenExpiresAt, bool isActive,
        IEnumerable<ManagerUser> managers)
    {
        var account = new SocialAccount { Id = Guid.NewGuid(), CompanyId = companyId };
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

    /// <summary>Zapisuje wynik kontroli połączenia (Validate.VerifyConnection): przyczynę błędu albo null, gdy jest poprawne.</summary>
    public void SetConnectionError(string? message) => ErrorMessage = message;

    private void Apply(string accountId, SocialPlatform platform, string externalPlatformId, string accountName,
        string accessToken, string? refreshToken, DateTime tokenExpiresAt, bool isActive,
        IEnumerable<ManagerUser> managers)
    {
        // Najpierw cała walidacja, dopiero potem zmiana stanu.
        var id = Validate.Text(accountId, "AccountId", 100);
        var externalId = Validate.Text(externalPlatformId, "ExternalPlatformId", 100);
        var name = Validate.Text(accountName, "AccountName", 150);
        var access = Validate.Text(accessToken, "AccessToken", 2000);
        var refresh = Validate.OptionalText(refreshToken, "RefreshToken", 2000);
        if (!Validate.ValidDateFormat(tokenExpiresAt))
            throw new DomainValidationException("Nieprawidłowa data wygaśnięcia tokenu (TokenExpiresAt).");

        AccountId = id;
        Platform = platform;
        ExternalPlatformId = externalId;
        AccountName = name;
        AccessToken = access;
        RefreshToken = refresh;
        TokenExpiresAt = Validate.ToUtc(tokenExpiresAt);
        IsActive = isActive;

        var assigned = managers.DistinctBy(m => m.Id).ToList();
        if (assigned.Any(m => m.CompanyId != CompanyId))
            throw new DomainValidationException("Do konta można przypisać tylko managerów tej samej firmy.");

        ManagerUsers.Clear(); // tabela łącząca (n - m) jest aktualizowana przez EF
        ManagerUsers.AddRange(assigned);
    }
}
