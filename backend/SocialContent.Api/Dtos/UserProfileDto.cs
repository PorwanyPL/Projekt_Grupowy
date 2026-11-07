namespace SocialContent.Api.Dtos;

/// <summary>Zalogowany użytkownik wraz z rolą i adresem e-mail (widocznym tylko dla niego samego).</summary>
/// <param name="Role">CompanyUser, ManagerUser lub ClientUser.</param>
/// <param name="DisplayName">Nazwa firmy (CompanyUser) albo imię i nazwisko (pozostałe role).</param>
/// <param name="CompanyId">Firma użytkownika: własna (CompanyUser) lub firma, która utworzyła managera; null dla klienta.</param>
/// <param name="Age">Wiek; null dla CompanyUser.</param>
/// <param name="Description">Opis; null dla CompanyUser.</param>
public record UserProfileDto(
    Guid Id,
    string Role,
    string Email,
    string DisplayName,
    Guid? CompanyId,
    int? Age,
    string? Description);
