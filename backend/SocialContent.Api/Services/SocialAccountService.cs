using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Services;

public interface ISocialAccountService
{
    Task<List<SocialAccountDto>> GetAllAsync(CancellationToken ct);
    Task<SocialAccountDto> GetAsync(Guid id, CancellationToken ct);
    Task<SocialAccountDto> CreateAsync(SocialAccountWriteDto dto, CancellationToken ct);
    Task<SocialAccountDto> UpdateAsync(Guid id, SocialAccountWriteDto dto, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public class SocialAccountService(AppDbContext db) : ISocialAccountService
{
    private IQueryable<SocialAccount> Query() =>
        db.SocialAccounts
            .Include(a => a.ManagerUsers)
            .Include(a => a.Posts).ThenInclude(p => p.Files)
            .AsSplitQuery();

    public async Task<List<SocialAccountDto>> GetAllAsync(CancellationToken ct)
    {
        var items = await Query().AsNoTracking().ToListAsync(ct);
        return items.OrderBy(a => a.AccountName).ThenBy(a => a.AccountId).Select(a => a.ToDto()).ToList();
    }

    public async Task<SocialAccountDto> GetAsync(Guid id, CancellationToken ct) =>
        (await Find(id, ct)).ToDto();

    public async Task<SocialAccountDto> CreateAsync(SocialAccountWriteDto dto, CancellationToken ct)
    {
        var managers = await ManagerLookup.LoadAsync(db, dto.ManagerUserIds, ct);

        var account = SocialAccount.Create(
            dto.AccountId,
            dto.Platform!.Value,
            dto.ExternalPlatformId,
            dto.AccountName,
            dto.AccessToken ?? "",
            dto.RefreshToken,
            dto.TokenExpiresAt!.Value,
            dto.IsActive,
            managers);

        if (await db.SocialAccounts.AnyAsync(a => a.Platform == account.Platform && a.AccountId == account.AccountId, ct))
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
        return account.ToDto();
    }

    public async Task<SocialAccountDto> UpdateAsync(Guid id, SocialAccountWriteDto dto, CancellationToken ct)
    {
        var account = await Find(id, ct);
        var managers = await ManagerLookup.LoadAsync(db, dto.ManagerUserIds, ct);

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
            a => a.Id != id && a.Platform == account.Platform && a.AccountId == account.AccountId, ct);
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
        return account.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var account = await db.SocialAccounts.FirstOrDefaultAsync(a => a.Id == id, ct)
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

    private async Task<SocialAccount> Find(Guid id, CancellationToken ct) =>
        await Query().FirstOrDefaultAsync(a => a.Id == id, ct)
        ?? throw new NotFoundException($"Nie znaleziono konta społecznościowego o id {id}.");
}
