namespace SocialContent.Api.Dtos;

/// <summary>Manager (ManagerUser). Nie zawiera adresu e-mail ani żadnych danych logowania.</summary>
/// <param name="SocialAccountIds">Konta społecznościowe przypisane managerowi. Wypełniane tylko przez endpointy /api/ManagerUsers; w managerach zagnieżdżonych w postach i kontach zawsze null.</param>
/// <param name="HasLoginAccount">Czy manager ma konto logowania. Wypełniane tylko przez endpointy /api/ManagerUsers; w managerach zagnieżdżonych zawsze null.</param>
/// <param name="Email">Adres e-mail logowania. Wypełniany wyłącznie przez endpointy /api/ManagerUsers (dostępne tylko dla firmy); w obiektach zagnieżdżonych zawsze null.</param>
/// <param name="IsAccessBlocked">Czy dostęp managera jest zablokowany. Tak samo jak HasLoginAccount: null w obiektach zagnieżdżonych.</param>
public record ManagerUserDto(
    Guid Id,
    string FirstName,
    string LastName,
    int Age,
    string Description,
    List<Guid>? SocialAccountIds = null,
    bool? HasLoginAccount = null,
    bool? IsAccessBlocked = null,
    string? Email = null);
