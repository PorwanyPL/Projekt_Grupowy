using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Services;

/// <summary>Relacja firma - klient (CompanyUser n - m ClientUser). Klient rejestruje się sam, a firma dodaje go po adresie e-mail.</summary>
public interface IClientUserService
{
    Task<List<ClientUserDto>> GetForCompanyAsync(Guid companyId, CancellationToken ct);
    Task<ClientUserDto> LinkAsync(Guid companyId, LinkClientDto dto, CancellationToken ct);
    Task UnlinkAsync(Guid companyId, Guid clientId, CancellationToken ct);
    Task<List<CompanyDto>> GetCompaniesAsync(Guid clientId, CancellationToken ct);
}

public class ClientUserService(AppDbContext db, IMapper mapper) : IClientUserService
{
    public async Task<List<ClientUserDto>> GetForCompanyAsync(Guid companyId, CancellationToken ct)
    {
        var company = await LoadCompany(companyId, ct);
        return company.ClientUsers
            .OrderBy(c => c.LastName.Value).ThenBy(c => c.FirstName.Value)
            .Select(c => mapper.Map<ClientUserDto>(c))
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
        return mapper.Map<ClientUserDto>(client);
    }

    public async Task UnlinkAsync(Guid companyId, Guid clientId, CancellationToken ct)
    {
        var company = await LoadCompany(companyId, ct);
        var client = company.ClientUsers.FirstOrDefault(c => c.Id == clientId)
                     ?? throw new NotFoundException("Ten klient nie jest powiązany z firmą.");
        company.RemoveClient(client);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<CompanyDto>> GetCompaniesAsync(Guid clientId, CancellationToken ct)
    {
        var client = await db.ClientUsers.AsNoTracking().Include(c => c.CompanyUsers)
                         .FirstOrDefaultAsync(c => c.Id == clientId, ct)
                     ?? throw new NotFoundException("Nie znaleziono klienta.");
        return client.CompanyUsers.OrderBy(c => c.Name).Select(c => mapper.Map<CompanyDto>(c)).ToList();
    }

    private async Task<CompanyUser> LoadCompany(Guid companyId, CancellationToken ct) =>
        await db.CompanyUsers.Include(c => c.ClientUsers).FirstOrDefaultAsync(c => c.CompanyId == companyId, ct)
        ?? throw new NotFoundException("Nie znaleziono firmy.");
}
