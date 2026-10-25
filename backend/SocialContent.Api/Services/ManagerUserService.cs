using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Services;

public interface IManagerUserService
{
    Task<List<ManagerUserDto>> GetAllAsync(CancellationToken ct);
    Task<ManagerUserDto> GetAsync(Guid id, CancellationToken ct);
    Task<ManagerUserDto> CreateAsync(ManagerUserWriteDto dto, CancellationToken ct);
    Task<ManagerUserDto> UpdateAsync(Guid id, ManagerUserWriteDto dto, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<ManagerUserDto> SetAccessAsync(Guid id, bool blocked, Guid? currentManagerId, CancellationToken ct);
    Task<ManagerUserDto> AnonymizeAsync(Guid id, CancellationToken ct);
}

public class ManagerUserService(AppDbContext db) : IManagerUserService
{
    public const string DeleteConflictMessage =
        "Nie można usunąć managera, który ma powiązane posty lub konta społecznościowe - " +
        "zablokuj dostęp zamiast usuwać, aby zachować historię publikacji.";

    public async Task<List<ManagerUserDto>> GetAllAsync(CancellationToken ct)
    {
        var items = await db.ManagerUsers.AsNoTracking().ToListAsync(ct);
        var flags = await db.ManagerCredentials.AsNoTracking()
            .ToDictionaryAsync(c => c.ManagerUserId, c => c.IsAccessBlocked, ct);

        return items
            .OrderBy(x => x.LastName.Value).ThenBy(x => x.FirstName.Value)
            .Select(x => x.ToDto(flags.ContainsKey(x.Id), flags.TryGetValue(x.Id, out var b) && b))
            .ToList();
    }

    public async Task<ManagerUserDto> GetAsync(Guid id, CancellationToken ct) =>
        await ToDtoAsync(await Find(id, ct), ct);

    public async Task<ManagerUserDto> CreateAsync(ManagerUserWriteDto dto, CancellationToken ct)
    {
        var entity = ManagerUser.Create(
            FirstName.Create(dto.FirstName),
            LastName.Create(dto.LastName),
            Age.Create(dto.Age),
            Description.Create(dto.Description));

        db.ManagerUsers.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.ToDto(false, false);
    }

    public async Task<ManagerUserDto> UpdateAsync(Guid id, ManagerUserWriteDto dto, CancellationToken ct)
    {
        var entity = await Find(id, ct);
        entity.Update(FirstName.Create(dto.FirstName));
        entity.Update(LastName.Create(dto.LastName));
        entity.Update(Age.Create(dto.Age));
        entity.Update(Description.Create(dto.Description));
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(entity, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var entity = await Find(id, ct);

        // Twarda reguła biznesowa: kto ma powiązane posty lub konto społecznościowe, nie może zostać usunięty.
        var hasPosts = await db.Posts.AnyAsync(p => p.ManagerUsers.Any(m => m.Id == id), ct);
        if (hasPosts || entity.SocialAccountId is not null)
            throw new ConflictException(DeleteConflictMessage);

        db.ManagerUsers.Remove(entity); // dane logowania znikają kaskadowo
        await db.SaveChangesAsync(ct);
    }

    public async Task<ManagerUserDto> SetAccessAsync(Guid id, bool blocked, Guid? currentManagerId, CancellationToken ct)
    {
        var entity = await Find(id, ct);

        if (blocked && currentManagerId == id)
            throw new DomainValidationException("Nie możesz zablokować własnego dostępu.");

        var credential = await db.ManagerCredentials.FirstOrDefaultAsync(c => c.ManagerUserId == id, ct)
            ?? throw new DomainValidationException("Ten manager nie ma konta logowania, więc nie ma czego blokować.");

        credential.SetAccessBlocked(blocked);
        await db.SaveChangesAsync(ct);
        return entity.ToDto(true, credential.IsAccessBlocked);
    }

    public async Task<ManagerUserDto> AnonymizeAsync(Guid id, CancellationToken ct)
    {
        var entity = await Find(id, ct);

        entity.Anonymize();

        // Konto logowania znika (to też unieważnia wydane tokeny), powiązania z postami i kontem zostają.
        var credential = await db.ManagerCredentials.FirstOrDefaultAsync(c => c.ManagerUserId == id, ct);
        if (credential is not null) db.ManagerCredentials.Remove(credential);

        await db.SaveChangesAsync(ct);
        return entity.ToDto(false, false);
    }

    private async Task<ManagerUserDto> ToDtoAsync(ManagerUser entity, CancellationToken ct)
    {
        var blocked = await db.ManagerCredentials.AsNoTracking()
            .Where(c => c.ManagerUserId == entity.Id)
            .Select(c => (bool?)c.IsAccessBlocked)
            .FirstOrDefaultAsync(ct);
        return entity.ToDto(blocked is not null, blocked == true);
    }

    private async Task<ManagerUser> Find(Guid id, CancellationToken ct) =>
        await db.ManagerUsers.FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new NotFoundException($"Nie znaleziono managera o id {id}.");
}
