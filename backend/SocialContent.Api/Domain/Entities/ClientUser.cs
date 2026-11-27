using SocialContent.Api.Domain.ValueObjects;

namespace SocialContent.Api.Domain.Entities;

/// <summary>
/// Klient firm. Rejestruje się samodzielnie, tworzy własne konta społecznościowe i zaprasza firmy do zarządzania nimi (ClientUser n - m CompanyUser).
/// Diagram nie podaje danych logowania klienta - przyjęto te same pola co w CompanyUser (Email, PasswordHash).
/// </summary>
public class ClientUser
{
    public Guid Id { get; private set; }
    public FirstName FirstName { get; private set; } = null!;
    public LastName LastName { get; private set; } = null!;
    public Age Age { get; private set; } = null!;
    public Description Description { get; private set; } = null!;

    /// <summary>Identyfikator logowania: adres e-mail w postaci znormalizowanej.</summary>
    public string Email { get; private set; } = null!;

    /// <summary>Hasz PBKDF2 w formacie "v1.iteracje.sól.hasz". Nigdy nie opuszcza warstwy serwisów.</summary>
    public string PasswordHash { get; private set; } = null!;

    /// <summary>Weryfikacja dwuetapowa: gdy true, logowanie wymaga dodatkowo kodu wysłanego na adres e-mail.</summary>
    public bool TwoFactorEnabled { get; private set; }

    /// <summary>Relacje z firmami (ClientUser n - m CompanyUser).</summary>
    public List<CompanyClient> Companies { get; private set; } = new();

    /// <summary>Konta społecznościowe klienta (ClientUser 1 - n SocialAccount).</summary>
    public List<SocialAccount> SocialAccounts { get; private set; } = new();

    private ClientUser() { }

    public static ClientUser Create(FirstName firstName, LastName lastName, Age age, Description description,
        string normalizedEmail, string passwordHash) =>
        new()
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Age = age,
            Description = description,
            Email = normalizedEmail,
            PasswordHash = passwordHash
        };

    public void SetTwoFactorEnabled(bool enabled) => TwoFactorEnabled = enabled;

    public void Update<T>(T value) where T : notnull
    {
        switch (value)
        {
            case FirstName f: FirstName = f; break;
            case LastName l: LastName = l; break;
            case Age a: Age = a; break;
            case Description d: Description = d; break;
            default: throw new ArgumentException($"Nieobsługiwany typ aktualizacji: {typeof(T).Name}");
        }
    }
}
