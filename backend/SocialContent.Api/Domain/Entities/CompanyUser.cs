using SocialContent.Api.Domain.Exceptions;

namespace SocialContent.Api.Domain.Entities;

/// <summary>
/// Administrator firmy. Rejestruje się jako pierwszy, tworzy ManagerUser, przyjmuje zaproszenia klientów do zarządzania ich kontami (SocialAccount) i przypisuje do nich managerów.
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

    /// <summary>Weryfikacja dwuetapowa: gdy true, logowanie wymaga dodatkowo kodu wysłanego na adres e-mail.</summary>
    public bool TwoFactorEnabled { get; private set; }

    /// <summary>
    /// Manager upoważniony przez właściciela do czatowania ze wszystkimi klientami (także niewspółpracującymi z firmą).
    /// Brak wartości = żaden manager nie ma tego uprawnienia. Zwykła kolumna bez klucza obcego; serwis czatu sprawdza,
    /// czy wskazany manager nadal istnieje, należy do firmy i nie jest zablokowany.
    /// </summary>
    public Guid? ChatManagerId { get; private set; }

    public List<ManagerUser> ManagerUsers { get; private set; } = new();
    public List<SocialAccount> SocialAccounts { get; private set; } = new();

    /// <summary>Relacje z klientami firmy (CompanyUser n - m ClientUser) wraz z pseudonimem i ustawieniem akceptacji.</summary>
    public List<CompanyClient> Clients { get; private set; } = new();

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

    public void SetTwoFactorEnabled(bool enabled) => TwoFactorEnabled = enabled;

    public void SetChatManager(Guid? managerId) => ChatManagerId = managerId;

    /// <summary>Dodaje klienta do firmy; ponowne dodanie tego samego klienta jest błędem.</summary>
    public CompanyClient AddClient(ClientUser client)
    {
        if (Clients.Any(c => c.ClientId == client.Id))
            throw new ConflictException("Ten klient jest już powiązany z firmą.");
        var link = CompanyClient.Create(CompanyId, client.Id);
        Clients.Add(link);
        return link;
    }

    public void RemoveClient(Guid clientId)
    {
        if (Clients.RemoveAll(c => c.ClientId == clientId) == 0)
            throw new NotFoundException("Ten klient nie jest powiązany z firmą.");
    }
}
