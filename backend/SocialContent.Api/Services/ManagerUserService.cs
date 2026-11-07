using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Domain.ValueObjects;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Services;

/// <summary>Zarządzanie managerami przez firmę. Każda metoda działa wyłącznie na managerach firmy <c>companyId</c>.</summary>
public interface IManagerUserService
{
    Task<List<ManagerUserDto>> GetAllAsync(Guid companyId, CancellationToken ct);
    Task<ManagerUserDto> GetAsync(Guid companyId, Guid id, CancellationToken ct);
    Task<ManagerUserDto> CreateAsync(Guid companyId, ManagerUserCreateDto dto, CancellationToken ct);
    Task<ManagerUserDto> UpdateAsync(Guid companyId, Guid id, ManagerUserWriteDto dto, CancellationToken ct);
    Task<ManagerUserDto> UpdateCredentialsAsync(Guid companyId, Guid id, ManagerCredentialsUpdateDto dto, CancellationToken ct);
    Task DeleteAsync(Guid companyId, Guid id, CancellationToken ct);
    Task<ManagerUserDto> SetAccessAsync(Guid companyId, Guid id, bool blocked, CancellationToken ct);
    Task<ManagerUserDto> AnonymizeAsync(Guid companyId, Guid id, CancellationToken ct);
}

public class ManagerUserService(AppDbContext db, IMapper mapper, IPasswordHasher hasher) : IManagerUserService
{
    public const string DeleteConflictMessage =
        "Nie można usunąć managera, który ma powiązane posty lub konta społecznościowe - " +
        "zablokuj dostęp zamiast usuwać, aby zachować historię publikacji.";

    public async Task<List<ManagerUserDto>> GetAllAsync(Guid companyId, CancellationToken ct)
    {
        var items = await db.ManagerUsers.AsNoTracking()
            .Include(m => m.SocialAccounts)
            .Where(m => m.CompanyId == companyId)
            .ToListAsync(ct);
        var credentials = await db.ManagerCredentials.AsNoTracking()
            .Where(c => c.ManagerUser.CompanyId == companyId)
            .ToDictionaryAsync(c => c.ManagerUserId, ct);

        return items
            .OrderBy(x => x.LastName.Value).ThenBy(x => x.FirstName.Value)
            .Select(x => credentials.TryGetValue(x.Id, out var c)
                ? ToDto(x, true, c.IsAccessBlocked, c.Email)
                : ToDto(x, false, false, null))
            .ToList();
    }

    public async Task<ManagerUserDto> GetAsync(Guid companyId, Guid id, CancellationToken ct) =>
        await ToDtoAsync(await Find(companyId, id, ct), ct);

    public async Task<ManagerUserDto> CreateAsync(Guid companyId, ManagerUserCreateDto dto, CancellationToken ct)
    {
        var entity = ManagerUser.Create(
            FirstName.Create(dto.FirstName),
            LastName.Create(dto.LastName),
            Age.Create(dto.Age),
            Description.Create(dto.Description),
            companyId);

        // Konto logowania powstaje zawsze razem z managerem (e-mail i hasło są wymagane).
        var email = Validate.NormalizeEmail(dto.Email);
        Validate.Email(email);
        Validate.Password(dto.Password);
        if (await EmailDirectory.IsTakenAsync(db, email, ct))
            throw new ConflictException("Konto z tym adresem e-mail już istnieje.");
        var credential = ManagerCredential.Create(entity, email, hasher.Hash(dto.Password));

        db.ManagerUsers.Add(entity);
        db.ManagerCredentials.Add(credential);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Wyścig dwóch rejestracji tego samego e-maila - zatrzymuje je unikalny indeks.
            throw new ConflictException("Konto z tym adresem e-mail już istnieje.");
        }
        return ToDto(entity, true, false, email);
    }

    public async Task<ManagerUserDto> UpdateAsync(Guid companyId, Guid id, ManagerUserWriteDto dto, CancellationToken ct)
    {
        var entity = await Find(companyId, id, ct);
        entity.Update(FirstName.Create(dto.FirstName));
        entity.Update(LastName.Create(dto.LastName));
        entity.Update(Age.Create(dto.Age));
        entity.Update(Description.Create(dto.Description));
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(entity, ct);
    }

    public async Task<ManagerUserDto> UpdateCredentialsAsync(Guid companyId, Guid id, ManagerCredentialsUpdateDto dto,
        CancellationToken ct)
    {
        var entity = await Find(companyId, id, ct);

        // Cała walidacja przez klasę Validate - te same reguły co przy tworzeniu managera i rejestracji.
        var email = Validate.NormalizeEmail(dto.Email);
        Validate.Email(email);
        var changePassword = !string.IsNullOrEmpty(dto.Password);
        if (changePassword) Validate.Password(dto.Password);

        var credential = await db.ManagerCredentials.FirstOrDefaultAsync(c => c.ManagerUserId == id, ct);
        if (await EmailDirectory.IsTakenAsync(db, email, ct, exceptManagerId: id))
            throw new ConflictException("Konto z tym adresem e-mail już istnieje.");

        if (credential is null)
        {
            // Manager utworzony przed wprowadzeniem obowiązkowego logowania: konto logowania powstaje teraz.
            if (!changePassword)
                throw new DomainValidationException("Ten manager nie ma jeszcze konta logowania - podaj także hasło.");
            credential = ManagerCredential.Create(entity, email, hasher.Hash(dto.Password!));
            db.ManagerCredentials.Add(credential);
        }
        else
        {
            credential.ChangeEmail(email);
            if (changePassword) credential.ChangePasswordHash(hasher.Hash(dto.Password!));
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("Konto z tym adresem e-mail już istnieje.");
        }
        return ToDto(entity, true, credential.IsAccessBlocked, credential.Email);
    }

    public async Task DeleteAsync(Guid companyId, Guid id, CancellationToken ct)
    {
        var entity = await Find(companyId, id, ct);

        // Twarda reguła biznesowa: kto ma powiązane posty lub konta społecznościowe, nie może zostać usunięty.
        var hasPosts = await db.Posts.AnyAsync(p => p.ManagerUsers.Any(m => m.Id == id), ct);
        if (hasPosts || entity.SocialAccounts.Count > 0)
            throw new ConflictException(DeleteConflictMessage);

        db.ManagerUsers.Remove(entity); // dane logowania znikają kaskadowo
        await db.SaveChangesAsync(ct);
    }

    public async Task<ManagerUserDto> SetAccessAsync(Guid companyId, Guid id, bool blocked, CancellationToken ct)
    {
        var entity = await Find(companyId, id, ct);

        var credential = await db.ManagerCredentials.FirstOrDefaultAsync(c => c.ManagerUserId == id, ct)
            ?? throw new DomainValidationException("Ten manager nie ma konta logowania, więc nie ma czego blokować.");

        credential.SetAccessBlocked(blocked);
        await db.SaveChangesAsync(ct);
        return ToDto(entity, true, credential.IsAccessBlocked, credential.Email);
    }

    public async Task<ManagerUserDto> AnonymizeAsync(Guid companyId, Guid id, CancellationToken ct)
    {
        var entity = await Find(companyId, id, ct);

        entity.Anonymize();

        // Konto logowania znika (to też unieważnia wydane tokeny), powiązania z postami i kontami zostają.
        var credential = await db.ManagerCredentials.FirstOrDefaultAsync(c => c.ManagerUserId == id, ct);
        if (credential is not null) db.ManagerCredentials.Remove(credential);

        await db.SaveChangesAsync(ct);
        return ToDto(entity, false, false, null);
    }

    /// <summary>Mapowanie przez AutoMapper plus pola, których nie da się wyliczyć z samej encji.</summary>
    private ManagerUserDto ToDto(ManagerUser m, bool hasLoginAccount, bool isAccessBlocked, string? email) =>
        mapper.Map<ManagerUserDto>(m) with
        {
            SocialAccountIds = m.SocialAccounts.Select(a => a.Id).ToList(),
            HasLoginAccount = hasLoginAccount,
            IsAccessBlocked = isAccessBlocked,
            Email = email
        };

    private async Task<ManagerUserDto> ToDtoAsync(ManagerUser entity, CancellationToken ct)
    {
        var credential = await db.ManagerCredentials.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ManagerUserId == entity.Id, ct);
        return ToDto(entity, credential is not null, credential?.IsAccessBlocked == true, credential?.Email);
    }

    private async Task<ManagerUser> Find(Guid companyId, Guid id, CancellationToken ct) =>
        await db.ManagerUsers.Include(x => x.SocialAccounts)
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId, ct)
        ?? throw new NotFoundException($"Nie znaleziono managera o id {id}.");
}
