using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Services;

/// <summary>
/// Konta społecznościowe firmy. Odczyt: CompanyUser widzi wszystkie konta firmy, ManagerUser tylko przypisane do siebie.
/// Zapis (tworzenie, zmiana, usuwanie, przypisywanie managerów): wyłącznie firma, przez <c>companyId</c>.
/// </summary>
public interface ISocialAccountService
{
    Task<List<SocialAccountDto>> GetAllAsync(CurrentUser user, CancellationToken ct);
    Task<SocialAccountDto> GetAsync(CurrentUser user, Guid id, CancellationToken ct);
    Task<SocialAccountDto> CreateAsync(Guid companyId, SocialAccountWriteDto dto, CancellationToken ct);
    Task<SocialAccountDto> UpdateAsync(Guid companyId, Guid id, SocialAccountWriteDto dto, CancellationToken ct);
    Task<SocialAccountDto> AssignManagersAsync(Guid companyId, Guid id, AssignManagersDto dto, CancellationToken ct);
    Task DeleteAsync(Guid companyId, Guid id, CancellationToken ct);
}

public class SocialAccountService(AppDbContext db, IMapper mapper) : ISocialAccountService
{
    private IQueryable<SocialAccount> Query() =>
        db.SocialAccounts
            .Include(a => a.ManagerUsers)
            .Include(a => a.Posts).ThenInclude(p => p.Files)
            .AsSplitQuery();

    public async Task<List<SocialAccountDto>> GetAllAsync(CurrentUser user, CancellationToken ct)
    {
        var items = await VisibleTo(user, Query().AsNoTracking()).ToListAsync(ct);
        return items.OrderBy(a => a.AccountName).ThenBy(a => a.AccountId).Select(a => mapper.Map<SocialAccountDto>(a)).ToList();
    }

    public async Task<SocialAccountDto> GetAsync(CurrentUser user, Guid id, CancellationToken ct)
    {
        var account = await VisibleTo(user, Query().AsNoTracking()).FirstOrDefaultAsync(a => a.Id == id, ct)
                      ?? throw new NotFoundException($"Nie znaleziono konta społecznościowego o id {id}.");
        return mapper.Map<SocialAccountDto>(account);
    }

    public async Task<SocialAccountDto> CreateAsync(Guid companyId, SocialAccountWriteDto dto, CancellationToken ct)
    {
        var managers = await ManagerLookup.LoadAsync(db, companyId, dto.ManagerUserIds, ct);

        var account = SocialAccount.Create(
            companyId,
            dto.AccountId,
            dto.Platform!.Value,
            dto.ExternalPlatformId,
            dto.AccountName,
            dto.AccessToken ?? "",
            dto.RefreshToken,
            dto.TokenExpiresAt!.Value,
            dto.IsActive,
            managers);

        if (await db.SocialAccounts.AnyAsync(
                a => a.CompanyId == companyId && a.Platform == account.Platform && a.AccountId == account.AccountId, ct))
            throw new ConflictException($"Konto {account.Platform} o identyfikatorze '{account.AccountId}' już istnieje.");

        db.SocialAccounts.Add(account);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException($"Konto {account.Platform} o identyfikatorze '{account.AccountId}' już istnieje.");
        }
        return mapper.Map<SocialAccountDto>(account);
    }

    public async Task<SocialAccountDto> UpdateAsync(Guid companyId, Guid id, SocialAccountWriteDto dto, CancellationToken ct)
    {
        var account = await Find(companyId, id, ct);
        var managers = await ManagerLookup.LoadAsync(db, companyId, dto.ManagerUserIds, ct);

        // Posty kierowane na konto muszą zostać na tej samej platformie (reguła z Post.Apply).
        if (dto.Platform!.Value != account.Platform && account.Posts.Count > 0)
            throw new DomainValidationException("Nie można zmienić platformy konta, do którego są przypisane posty.");

        account.Update(
            dto.AccountId,
            dto.Platform!.Value,
            dto.ExternalPlatformId,
            dto.AccountName,
            dto.AccessToken,
            dto.RefreshToken,
            dto.TokenExpiresAt!.Value,
            dto.IsActive,
            managers);

        var duplicate = await db.SocialAccounts.AnyAsync(
            a => a.Id != id && a.CompanyId == companyId && a.Platform == account.Platform && a.AccountId == account.AccountId, ct);
        if (duplicate)
            throw new ConflictException($"Konto {account.Platform} o identyfikatorze '{account.AccountId}' już istnieje.");

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException($"Konto {account.Platform} o identyfikatorze '{account.AccountId}' już istnieje.");
        }
        return mapper.Map<SocialAccountDto>(account);
    }

    public async Task<SocialAccountDto> AssignManagersAsync(Guid companyId, Guid id, AssignManagersDto dto, CancellationToken ct)
    {
        var account = await Find(companyId, id, ct);
        var managers = await ManagerLookup.LoadAsync(db, companyId, dto.ManagerUserIds, ct);

        // Przypisanie zmienia tylko listę managerów; dane i tokeny konta zostają (null = bez zmian).
        account.Update(account.AccountId, account.Platform, account.ExternalPlatformId, account.AccountName,
            null, null, account.TokenExpiresAt, account.IsActive, managers);

        await db.SaveChangesAsync(ct);
        return mapper.Map<SocialAccountDto>(account);
    }

    public async Task DeleteAsync(Guid companyId, Guid id, CancellationToken ct)
    {
        var account = await db.SocialAccounts.FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId, ct)
                      ?? throw new NotFoundException($"Nie znaleziono konta społecznościowego o id {id}.");

        if (await db.Posts.AnyAsync(p => p.TargetAccountId == id, ct))
            throw new ConflictException("Nie można usunąć konta, do którego są przypisane posty. Najpierw zmień konto docelowe tych postów lub je usuń.");

        db.SocialAccounts.Remove(account);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Post przypisany do konta między sprawdzeniem a usunięciem - zatrzymała go relacja Restrict.
            throw new ConflictException("Nie można usunąć konta, do którego są przypisane posty.");
        }
    }

    /// <summary>CompanyUser: konta swojej firmy. ManagerUser: tylko konta, do których został przypisany.</summary>
    private static IQueryable<SocialAccount> VisibleTo(CurrentUser user, IQueryable<SocialAccount> query) =>
        user.IsCompany
            ? query.Where(a => a.CompanyId == user.Id)
            : query.Where(a => a.ManagerUsers.Any(m => m.Id == user.Id));

    private async Task<SocialAccount> Find(Guid companyId, Guid id, CancellationToken ct) =>
        await Query().FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId, ct)
        ?? throw new NotFoundException($"Nie znaleziono konta społecznościowego o id {id}.");
}
