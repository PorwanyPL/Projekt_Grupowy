using SocialContent.Api.Domain.ValueObjects;

namespace SocialContent.Api.Domain.Entities;

public class ManagerUser
{
    public Guid Id { get; private set; }
    public FirstName FirstName { get; private set; } = null!;
    public LastName LastName { get; private set; } = null!;
    public Age Age { get; private set; } = null!;
    public Description Description { get; private set; } = null!;

    /// <summary>Firma (CompanyUser), która utworzyła managera (CompanyUser 1 - n ManagerUser).</summary>
    public Guid CompanyId { get; private set; }
    public CompanyUser CompanyUser { get; private set; } = null!;

    /// <summary>Konta społecznościowe przypisane managerowi (ManagerUser n - m SocialAccount).</summary>
    public List<SocialAccount> SocialAccounts { get; private set; } = new();

    public List<Post> Posts { get; private set; } = new();

    private ManagerUser() { }

    public static ManagerUser Create(FirstName firstName, LastName lastName, Age age, Description description,
        Guid companyId) =>
        new()
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            FirstName = firstName,
            LastName = lastName,
            Age = age,
            Description = description
        };

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

    /// <summary>
    /// Usuwa dane osobowe (prośba o skasowanie danych): imię i nazwisko zamienia na "Usunięty użytkownik",
    /// a opis na informację o usunięciu (Description nie może być pusty). Wiek, powiązania z postami
    /// i z kontem społecznościowym zostają nienaruszone - dzięki temu historia publikacji jest zachowana.
    /// Konto logowania usuwa serwis.
    /// </summary>
    public void Anonymize()
    {
        FirstName = FirstName.Create("Usunięty");
        LastName = LastName.Create("użytkownik");
        Description = Description.Create("Dane usunięte na prośbę użytkownika.");
    }
}
