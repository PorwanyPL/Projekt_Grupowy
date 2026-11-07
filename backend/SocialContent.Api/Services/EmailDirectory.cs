using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;

namespace SocialContent.Api.Services;

/// <summary>
/// Adres e-mail jest identyfikatorem logowania i musi być unikalny we wszystkich rolach naraz
/// (CompanyUser, ManagerUser, ClientUser), bo logowanie jest jedno. Indeksy unikalne w bazie działają tylko per tabela.
/// </summary>
internal static class EmailDirectory
{
    /// <param name="exceptManagerId">Manager, którego własny e-mail pomijamy (zmiana danych logowania).</param>
    public static async Task<bool> IsTakenAsync(AppDbContext db, string normalizedEmail, CancellationToken ct,
        Guid? exceptManagerId = null) =>
        await db.CompanyUsers.AnyAsync(c => c.Email == normalizedEmail, ct)
        || await db.ManagerCredentials.AnyAsync(
            c => c.Email == normalizedEmail && (exceptManagerId == null || c.ManagerUserId != exceptManagerId), ct)
        || await db.ClientUsers.AnyAsync(c => c.Email == normalizedEmail, ct);
}
