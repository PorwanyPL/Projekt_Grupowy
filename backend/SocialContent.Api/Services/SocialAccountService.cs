using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Services;

/// <summary>
/// Konta społecznościowe. Tworzy je klient (podając e-mail firmy, która ma nimi zarządzać) i zaprasza firmę; firma przyjmuje
/// lub odrzuca zaproszenie i przypisuje managerów. Odczyt: klient - własne konta, CompanyUser - konta zapraszających go
/// i obsługiwanych klientów, ManagerUser - tylko przypisane do siebie.
/// </summary>
public interface ISocialAccountService
{
    Task<List<SocialAccountDto>> GetAllAsync(CurrentUser user, Guid? clientId, Guid? companyId, CancellationToken ct);
    Task<SocialAccountDto> GetAsync(CurrentUser user, Guid id, CancellationToken ct);
    Task<SocialAccountDto> CreateAsync(Guid clientId, SocialAccountWriteDto dto, CancellationToken ct);
    Task<SocialAccountDto> UpdateAsync(Guid clientId, Guid id, SocialAccountWriteDto dto, CancellationToken ct);
    Task<SocialAccountDto> InviteAsync(Guid clientId, Guid id, InviteCompanyDto dto, CancellationToken ct);
    Task<SocialAccountDto> AcceptInvitationAsync(Guid companyId, Guid id, CancellationToken ct);
    Task<SocialAccountDto> DeclineInvitationAsync(Guid companyId, Guid id, CancellationToken ct);
    Task<SocialAccountDto> AssignManagersAsync(Guid companyId, Guid id, AssignManagersDto dto, CancellationToken ct);
    Task DeleteAsync(Guid clientId, Guid id, CancellationToken ct);
}

public class SocialAccountService(AppDbContext db, IMapper mapper) : ISocialAccountService
{
    private IQueryable<SocialAccount> Query() =>
        db.SocialAccounts
            .Include(a => a.OwnerClient)
            .Include(a => a.CompanyUser)
            .Include(a => a.ManagerUsers)
            .Include(a => a.Posts).ThenInclude(p => p.Files)
            .AsSplitQuery();

    public async Task<List<SocialAccountDto>> GetAllAsync(CurrentUser user, Guid? clientId, Guid? companyId, CancellationToken ct)
    {
        var query = VisibleTo(user, Query().AsNoTracking());
        if (clientId is not null) query = query.Where(a => a.OwnerClientId == clientId);
        if (companyId is not null) query = query.Where(a => a.CompanyId == companyId);

        var items = await query.ToListAsync(ct);
        var ordered = items.OrderBy(a => a.AccountName).ThenBy(a => a.AccountId).ToList();
        return await ToDtosAsync(user, ordered, ct);
    }

    public async Task<SocialAccountDto> GetAsync(CurrentUser user, Guid id, CancellationToken ct)
    {
        var account = await VisibleTo(user, Query().AsNoTracking()).FirstOrDefaultAsync(a => a.Id == id, ct)
                      ?? throw new NotFoundException($"Nie znaleziono konta społecznościowego o id {id}.");
        return (await ToDtosAsync(user, new List<SocialAccount> { account }, ct)).Single();
    }

    public async Task<SocialAccountDto> CreateAsync(Guid clientId, SocialAccountWriteDto dto, CancellationToken ct)
    {
        var company = await FindCompanyByEmail(dto.CompanyEmail, ct);

        var account = SocialAccount.Create(
            clientId,
            company.CompanyId,
            dto.AccountId,
            dto.Platform!.Value,
            dto.ExternalPlatformId,
            dto.AccountName,
            dto.AccessToken ?? "",
            dto.RefreshToken,
            dto.TokenExpiresAt!.Value,
            dto.IsActive,
            Array.Empty<ManagerUser>());

        if (await db.SocialAccounts.AnyAsync(
                a => a.OwnerClientId == clientId && a.Platform == account.Platform && a.AccountId == account.AccountId, ct))
            throw new ConflictException($"Konto {account.Platform} o identyfikatorze '{account.AccountId}' już istnieje.");

        db.SocialAccounts.Add(account);
        await SaveAsync(account, ct);
        return await ReloadDtoAsync(clientId, UserRole.ClientUser, account.Id, ct);
    }

    public async Task<SocialAccountDto> UpdateAsync(Guid clientId, Guid id, SocialAccountWriteDto dto, CancellationToken ct)
    {
        var account = await FindOwned(clientId, id, ct);

        // Posty kierowane na konto muszą zostać na tej samej platformie (reguła z Post.Apply).
        if (dto.Platform!.Value != account.Platform && account.Posts.Count > 0)
            throw new DomainValidationException("Nie można zmienić platformy konta, do którego są przypisane posty.");

        // Zmiana danych nie rusza firmy ani managerów (null = bez zmian tokenów).
        account.Update(
            dto.AccountId,
            dto.Platform!.Value,
            dto.ExternalPlatformId,
            dto.AccountName,
            dto.AccessToken,
            dto.RefreshToken,
            dto.TokenExpiresAt!.Value,
            dto.IsActive,
            account.ManagerUsers.ToList());

        var duplicate = await db.SocialAccounts.AnyAsync(
            a => a.Id != id && a.OwnerClientId == clientId && a.Platform == account.Platform && a.AccountId == account.AccountId, ct);
        if (duplicate)
            throw new ConflictException($"Konto {account.Platform} o identyfikatorze '{account.AccountId}' już istnieje.");

        await SaveAsync(account, ct);
        return await ReloadDtoAsync(clientId, UserRole.ClientUser, id, ct);
    }

    public async Task<SocialAccountDto> InviteAsync(Guid clientId, Guid id, InviteCompanyDto dto, CancellationToken ct)
    {
        var account = await FindOwned(clientId, id, ct);
        var company = await FindCompanyByEmail(dto.CompanyEmail, ct);
        account.Invite(company.CompanyId); // Conflict, gdy konto jest już zarządzane przez firmę
        await db.SaveChangesAsync(ct);
        return await ReloadDtoAsync(clientId, UserRole.ClientUser, id, ct);
    }

    public async Task<SocialAccountDto> AcceptInvitationAsync(Guid companyId, Guid id, CancellationToken ct)
    {
        var account = await FindInvitedBy(companyId, id, ct);
        account.AcceptInvitation();

        // Przyjęcie zaproszenia tworzy relację firma - klient (jeśli jej jeszcze nie ma) z domyślnym trybem akceptacji.
        if (!await db.CompanyClients.AnyAsync(l => l.CompanyId == companyId && l.ClientId == account.OwnerClientId, ct))
            db.CompanyClients.Add(CompanyClient.Create(companyId, account.OwnerClientId));

        await db.SaveChangesAsync(ct);
        return await ReloadDtoAsync(companyId, UserRole.CompanyUser, id, ct);
    }

    public async Task<SocialAccountDto> DeclineInvitationAsync(Guid companyId, Guid id, CancellationToken ct)
    {
        var account = await FindInvitedBy(companyId, id, ct);
        account.DeclineInvitation();
        await db.SaveChangesAsync(ct);
        // Po odrzuceniu konto znika z widoku firmy, więc odpowiedź budujemy z już załadowanej encji.
        return mapper.Map<SocialAccountDto>(account);
    }

    public async Task<SocialAccountDto> AssignManagersAsync(Guid companyId, Guid id, AssignManagersDto dto, CancellationToken ct)
    {
        var account = await FindInvitedBy(companyId, id, ct, includeAccepted: true);
        var managers = await ManagerLookup.LoadAsync(db, companyId, dto.ManagerUserIds, ct);

        // Przypisanie zmienia tylko listę managerów; dane i tokeny konta zostają (null = bez zmian).
        account.Update(account.AccountId, account.Platform, account.ExternalPlatformId, account.AccountName,
            null, null, account.TokenExpiresAt, account.IsActive, managers);

        await db.SaveChangesAsync(ct);
        return await ReloadDtoAsync(companyId, UserRole.CompanyUser, id, ct);
    }

    public async Task DeleteAsync(Guid clientId, Guid id, CancellationToken ct)
    {
        var account = await db.SocialAccounts.FirstOrDefaultAsync(a => a.Id == id && a.OwnerClientId == clientId, ct)
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

    /// <summary>
    /// Klient: własne konta (każdy status). CompanyUser: konta, do których został zaproszony lub którymi zarządza
    /// (odrzucone znikają). ManagerUser: tylko konta, do których został przypisany.
    /// </summary>
    private static IQueryable<SocialAccount> VisibleTo(CurrentUser user, IQueryable<SocialAccount> query) =>
        user.Role switch
        {
            UserRole.ClientUser => query.Where(a => a.OwnerClientId == user.Id),
            UserRole.CompanyUser => query.Where(a => a.CompanyId == user.Id && a.ManagementStatus != ManagementStatus.Declined),
            _ => query.Where(a => a.ManagementStatus == ManagementStatus.Accepted && a.ManagerUsers.Any(m => m.Id == user.Id))
        };

    /// <summary>
    /// Uzupełnia DTO o dane zależne od widza: pseudonim klienta nadany przez firmę (widoki firmy i managera)
    /// oraz tryb akceptacji postów wybrany przez klienta.
    /// </summary>
    private async Task<List<SocialAccountDto>> ToDtosAsync(CurrentUser user, List<SocialAccount> accounts, CancellationToken ct)
    {
        var companyIds = accounts.Select(a => a.CompanyId).Distinct().ToList();
        var links = await db.CompanyClients.AsNoTracking()
            .Where(l => companyIds.Contains(l.CompanyId)).ToListAsync(ct);
        var byPair = links.ToDictionary(l => (l.CompanyId, l.ClientId));

        return accounts.Select(a =>
        {
            var link = byPair.GetValueOrDefault((a.CompanyId, a.OwnerClientId));
            var dto = mapper.Map<SocialAccountDto>(a);
            var ownerName = user.Role == UserRole.ClientUser || string.IsNullOrWhiteSpace(link?.Alias)
                ? dto.OwnerName
                : link!.Alias!;
            return dto with { OwnerName = ownerName, ApprovalMode = link?.ApprovalMode ?? PostApprovalMode.Manager };
        }).ToList();
    }

    private async Task<SocialAccountDto> ReloadDtoAsync(Guid userId, UserRole role, Guid id, CancellationToken ct)
    {
        var user = new CurrentUser(userId, role);
        var account = await VisibleTo(user, Query().AsNoTracking()).FirstAsync(a => a.Id == id, ct);
        return (await ToDtosAsync(user, new List<SocialAccount> { account }, ct)).Single();
    }

    private async Task<CompanyUser> FindCompanyByEmail(string? email, CancellationToken ct)
    {
        var normalized = Validate.NormalizeEmail(email);
        Validate.Email(normalized);
        return await db.CompanyUsers.FirstOrDefaultAsync(c => c.Email == normalized, ct)
               ?? throw new NotFoundException("Nie znaleziono firmy o tym adresie e-mail. Firma musi najpierw założyć konto w aplikacji.");
    }

    private async Task SaveAsync(SocialAccount account, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException($"Konto {account.Platform} o identyfikatorze '{account.AccountId}' już istnieje.");
        }
    }

    private async Task<SocialAccount> FindOwned(Guid clientId, Guid id, CancellationToken ct) =>
        await Query().FirstOrDefaultAsync(a => a.Id == id && a.OwnerClientId == clientId, ct)
        ?? throw new NotFoundException($"Nie znaleziono konta społecznościowego o id {id}.");

    /// <summary>Konto, do którego firma została zaproszona (Invited) albo którym zarządza (Accepted, gdy includeAccepted).</summary>
    private async Task<SocialAccount> FindInvitedBy(Guid companyId, Guid id, CancellationToken ct, bool includeAccepted = false) =>
        await Query().FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId
                && (a.ManagementStatus == ManagementStatus.Invited
                    || (includeAccepted && a.ManagementStatus == ManagementStatus.Accepted)), ct)
        ?? throw new NotFoundException($"Nie znaleziono konta społecznościowego o id {id}.");
}
