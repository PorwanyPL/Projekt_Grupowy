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
/// Relacja firma - klient (CompanyUser n - m ClientUser). Klient rejestruje się sam; relacja powstaje, gdy firma przyjmie
/// zaproszenie do zarządzania kontem klienta (albo gdy firma doda klienta po adresie e-mail). Relacja niesie pseudonim klienta
/// nadany przez firmę oraz wybór klienta, kto akceptuje posty (PostApprovalMode).
/// </summary>
public interface IClientUserService
{
    /// <summary>Klienci firmy: powiązani oraz ci, którzy czekają z zaproszeniem (z pseudonimem nadanym przez firmę).</summary>
    Task<List<ClientUserDto>> GetForCompanyAsync(Guid companyId, CancellationToken ct);

    /// <summary>Klienci, do których kont przypisano managera (bez adresów e-mail).</summary>
    Task<List<ClientUserDto>> GetForManagerAsync(Guid managerId, CancellationToken ct);

    Task<ClientUserDto> LinkAsync(Guid companyId, LinkClientDto dto, CancellationToken ct);
    Task UnlinkAsync(Guid companyId, Guid clientId, CancellationToken ct);
    Task<ClientUserDto> SetAliasAsync(Guid companyId, Guid clientId, ClientAliasDto dto, CancellationToken ct);

    Task<List<CompanyDto>> GetCompaniesAsync(Guid clientId, CancellationToken ct);
    Task<CompanyDto> SetApprovalModeAsync(Guid clientId, Guid companyId, PostApprovalMode mode, CancellationToken ct);
}

public class ClientUserService(AppDbContext db, IMapper mapper) : IClientUserService
{
    public async Task<List<ClientUserDto>> GetForCompanyAsync(Guid companyId, CancellationToken ct)
    {
        var links = await db.CompanyClients.AsNoTracking().Include(l => l.ClientUser)
            .Where(l => l.CompanyId == companyId).ToListAsync(ct);
        var invited = await db.SocialAccounts.AsNoTracking().Include(a => a.OwnerClient)
            .Where(a => a.CompanyId == companyId && a.ManagementStatus == ManagementStatus.Invited).ToListAsync(ct);

        var clients = links.Select(l => l.ClientUser)
            .Concat(invited.Select(a => a.OwnerClient))
            .DistinctBy(c => c.Id)
            .ToList();
        var aliases = links.ToDictionary(l => l.ClientId, l => l.Alias);

        return clients
            .Select(c => ToDto(c, aliases.GetValueOrDefault(c.Id), invited.Count(a => a.OwnerClientId == c.Id)))
            .OrderBy(c => c.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<List<ClientUserDto>> GetForManagerAsync(Guid managerId, CancellationToken ct)
    {
        var manager = await db.ManagerUsers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == managerId, ct)
                      ?? throw new NotFoundException("Nie znaleziono managera.");
        var owners = await db.SocialAccounts.AsNoTracking().Include(a => a.OwnerClient)
            .Where(a => a.ManagementStatus == ManagementStatus.Accepted && a.ManagerUsers.Any(m => m.Id == managerId))
            .Select(a => a.OwnerClient).Distinct().ToListAsync(ct);
        var aliases = await db.CompanyClients.AsNoTracking().Where(l => l.CompanyId == manager.CompanyId)
            .ToDictionaryAsync(l => l.ClientId, l => l.Alias, ct);

        // Manager nie dostaje adresów e-mail klientów, tylko to, czego potrzebuje do wyboru klienta.
        return owners
            .Select(c => ToDto(c, aliases.GetValueOrDefault(c.Id), 0) with { Email = "" })
            .OrderBy(c => c.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<ClientUserDto> LinkAsync(Guid companyId, LinkClientDto dto, CancellationToken ct)
    {
        var email = Validate.NormalizeEmail(dto.Email);
        Validate.Email(email);

        var client = await db.ClientUsers.FirstOrDefaultAsync(c => c.Email == email, ct)
                     ?? throw new NotFoundException(
                         "Nie znaleziono klienta o tym adresie e-mail. Klient musi najpierw sam się zarejestrować.");

        var company = await LoadCompany(companyId, ct);
        company.AddClient(client); // ConflictException, gdy klient jest już powiązany z firmą
        await db.SaveChangesAsync(ct);
        return ToDto(client, null, 0);
    }

    public async Task UnlinkAsync(Guid companyId, Guid clientId, CancellationToken ct)
    {
        var company = await LoadCompany(companyId, ct);
        if (company.Clients.All(c => c.ClientId != clientId))
            throw new NotFoundException("Ten klient nie jest powiązany z firmą.");

        // Odpięcie klienta, którego konta firma nadal obsługuje, zgubiłoby jego ustawienia (pseudonim, tryb akceptacji).
        if (await db.SocialAccounts.AnyAsync(a => a.CompanyId == companyId && a.OwnerClientId == clientId
                                                  && a.ManagementStatus == ManagementStatus.Accepted, ct))
            throw new ConflictException("Firma zarządza kontami tego klienta - nie można go odpiąć.");

        company.RemoveClient(clientId);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ClientUserDto> SetAliasAsync(Guid companyId, Guid clientId, ClientAliasDto dto, CancellationToken ct)
    {
        var link = await db.CompanyClients.Include(l => l.ClientUser)
                       .FirstOrDefaultAsync(l => l.CompanyId == companyId && l.ClientId == clientId, ct)
                   ?? throw new NotFoundException("Ten klient nie jest powiązany z firmą.");
        link.SetAlias(string.IsNullOrWhiteSpace(dto.Alias) ? null : dto.Alias.Trim());
        await db.SaveChangesAsync(ct);
        return ToDto(link.ClientUser, link.Alias, 0);
    }

    public async Task<List<CompanyDto>> GetCompaniesAsync(Guid clientId, CancellationToken ct)
    {
        var links = await db.CompanyClients.AsNoTracking().Include(l => l.CompanyUser)
            .Where(l => l.ClientId == clientId).ToListAsync(ct);
        return links.OrderBy(l => l.CompanyUser.Name)
            .Select(l => mapper.Map<CompanyDto>(l.CompanyUser) with { ApprovalMode = l.ApprovalMode })
            .ToList();
    }

    public async Task<CompanyDto> SetApprovalModeAsync(Guid clientId, Guid companyId, PostApprovalMode mode, CancellationToken ct)
    {
        var link = await db.CompanyClients.Include(l => l.CompanyUser)
                       .FirstOrDefaultAsync(l => l.ClientId == clientId && l.CompanyId == companyId, ct)
                   ?? throw new NotFoundException("Nie współpracujesz z tą firmą.");
        link.SetApprovalMode(mode);
        await db.SaveChangesAsync(ct);
        return mapper.Map<CompanyDto>(link.CompanyUser) with { ApprovalMode = link.ApprovalMode };
    }

    private ClientUserDto ToDto(ClientUser client, string? alias, int pendingInvitations)
    {
        var dto = mapper.Map<ClientUserDto>(client);
        return dto with
        {
            Alias = alias,
            DisplayName = string.IsNullOrWhiteSpace(alias) ? dto.DisplayName : alias,
            PendingInvitations = pendingInvitations
        };
    }

    private async Task<CompanyUser> LoadCompany(Guid companyId, CancellationToken ct) =>
        await db.CompanyUsers.Include(c => c.Clients).FirstOrDefaultAsync(c => c.CompanyId == companyId, ct)
        ?? throw new NotFoundException("Nie znaleziono firmy.");
}
