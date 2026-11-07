using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Controllers;

/// <summary>Nazwy ról do atrybutu [Authorize(Roles = ...)]; zgodne z wartościami claimu "role" w tokenie.</summary>
public static class Roles
{
    public const string Company = nameof(UserRole.CompanyUser);
    public const string Manager = nameof(UserRole.ManagerUser);
    public const string Client = nameof(UserRole.ClientUser);
    public const string CompanyOrManager = Company + "," + Manager;
}
