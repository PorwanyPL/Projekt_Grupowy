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
}

public class ManagerUserService(AppDbContext db) : IManagerUserService
{
    public async Task<List<ManagerUserDto>> GetAllAsync(CancellationToken ct)
    {
        var items = await db.ManagerUsers.AsNoTracking().ToListAsync(ct);
        return items.OrderBy(x => x.LastName.Value).ThenBy(x => x.FirstName.Value).Select(x => x.ToDto()).ToList();
    }

    public async Task<ManagerUserDto> GetAsync(Guid id, CancellationToken ct) =>
        (await Find(id, ct)).ToDto();

    public async Task<ManagerUserDto> CreateAsync(ManagerUserWriteDto dto, CancellationToken ct)
    {
        var entity = ManagerUser.Create(
            FirstName.Create(dto.FirstName),
            LastName.Create(dto.LastName),
            Age.Create(dto.Age),
            Description.Create(dto.Description));

        db.ManagerUsers.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<ManagerUserDto> UpdateAsync(Guid id, ManagerUserWriteDto dto, CancellationToken ct)
    {
        var entity = await Find(id, ct);
        entity.Update(FirstName.Create(dto.FirstName));
        entity.Update(LastName.Create(dto.LastName));
        entity.Update(Age.Create(dto.Age));
        entity.Update(Description.Create(dto.Description));
        await db.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var entity = await Find(id, ct);
        db.ManagerUsers.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    private async Task<ManagerUser> Find(Guid id, CancellationToken ct) =>
        await db.ManagerUsers.FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new NotFoundException($"Nie znaleziono managera o id {id}.");
}
