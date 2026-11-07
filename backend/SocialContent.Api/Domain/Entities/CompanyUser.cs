using SocialContent.Api.Domain.Exceptions;

namespace SocialContent.Api.Domain.Entities;

/// <summary>
/// Administrator firmy. Rejestruje się jako pierwszy, tworzy ManagerUser i SocialAccount oraz ustala relacje między nimi.
/// Dane logowania (e-mail, hasz hasła) są częścią encji, tak jak na diagramie.
/// </summary>
public class CompanyUser
{
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = null!;

    /// <summary>Identyfikator logowania: adres e-mail w postaci znormalizowanej.</summary>
    public string Email { get; private set; } = null!;

    /// <summary>Hasz PBKDF2 w formacie "v1.iteracje.sól.hasz". Nigdy nie opuszcza warstwy serwisów.</summary>
    public string PasswordHash { get; private set; } = null!;

    public bool IsAccessBlocked { get; private set; }

    public List<ManagerUser> ManagerUsers { get; private set; } = new();
    public List<SocialAccount> SocialAccounts { get; private set; } = new();

    /// <summary>Klienci firmy (CompanyUser n - m ClientUser).</summary>
    public List<ClientUser> ClientUsers { get; private set; } = new();

    private CompanyUser() { }

    public static CompanyUser Create(string name, string normalizedEmail, string passwordHash) =>
        new()
        {
            CompanyId = Guid.NewGuid(),
            Name = Validate.Text(name, "Nazwa firmy", 150),
            Email = normalizedEmail,
            PasswordHash = passwordHash
        };

    public void SetAccessBlocked(bool blocked) => IsAccessBlocked = blocked;

    /// <summary>Dodaje klienta do firmy; ponowne dodanie tego samego klienta jest błędem.</summary>
    public void AddClient(ClientUser client)
    {
        if (ClientUsers.Any(c => c.Id == client.Id))
            throw new ConflictException("Ten klient jest już powiązany z firmą.");
        ClientUsers.Add(client);
    }

    public void RemoveClient(ClientUser client)
    {
        if (ClientUsers.RemoveAll(c => c.Id == client.Id) == 0)
            throw new NotFoundException("Ten klient nie jest powiązany z firmą.");
    }
}
