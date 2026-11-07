using System.Security.Claims;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;

namespace SocialContent.Api.Services;

/// <summary>Tożsamość zalogowanego użytkownika odczytana z tokenu JWT.</summary>
/// <param name="Id">CompanyId (CompanyUser), Id managera (ManagerUser) albo Id klienta (ClientUser).</param>
public record CurrentUser(Guid Id, UserRole Role)
{
    public bool IsCompany => Role == UserRole.CompanyUser;
    public bool IsManager => Role == UserRole.ManagerUser;
}

public static class CurrentUserExtensions
{
    public static CurrentUser ToCurrentUser(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirst("sub")?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = principal.FindFirst("role")?.Value ?? principal.FindFirst(ClaimTypes.Role)?.Value;

        if (!Guid.TryParse(subject, out var id) || !Enum.TryParse<UserRole>(role, out var parsedRole))
            throw new UnauthorizedException("Token nie zawiera poprawnej tożsamości.");

        return new CurrentUser(id, parsedRole);
    }
}
