using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Exceptions;

namespace SocialContent.Api.Services;

/// <summary>Wspólne ładowanie managerów firmy po identyfikatorach (konta społecznościowe i posty).</summary>
internal static class ManagerLookup
{
    public static async Task<List<ManagerUser>> LoadAsync(AppDbContext db, Guid companyId, List<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0) return new List<ManagerUser>();

        // Managerowie innych firm są traktowani jak nieistniejący, żeby nie ujawniać cudzych danych.
        var found = await db.ManagerUsers
            .Where(m => m.CompanyId == companyId && distinct.Contains(m.Id))
            .ToListAsync(ct);
        var missing = distinct.Except(found.Select(m => m.Id)).ToList();
        if (missing.Count > 0)
            throw new NotFoundException($"Nie znaleziono managerów: {string.Join(", ", missing)}.");
        return found;
    }
}
