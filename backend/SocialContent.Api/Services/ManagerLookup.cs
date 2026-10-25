using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain;

namespace SocialContent.Api.Services;

/// <summary>Wspólne ładowanie managerów po identyfikatorach (posty i konta społecznościowe).</summary>
internal static class ManagerLookup
{
    public static async Task<List<ManagerUser>> LoadAsync(AppDbContext db, List<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0) return new List<ManagerUser>();

        var found = await db.ManagerUsers.Where(m => distinct.Contains(m.Id)).ToListAsync(ct);
        var missing = distinct.Except(found.Select(m => m.Id)).ToList();
        if (missing.Count > 0)
            throw new NotFoundException($"Nie znaleziono managerów: {string.Join(", ", missing)}.");
        return found;
    }
}
