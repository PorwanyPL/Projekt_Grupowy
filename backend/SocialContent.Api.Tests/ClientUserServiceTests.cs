using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Tests;

public class ClientUserServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Link_ClientToManyCompanies_IsManyToMany()
    {
        var first = await _db.AddCompanyAsync("Pierwsza", "p@example.com");
        var second = await _db.AddCompanyAsync("Druga", "d@example.com");
        var client = await _db.AddClientAsync("klient@example.com");

        foreach (var company in new[] { first, second })
        {
            await using var db = _db.NewContext();
            await new ClientUserService(db, _db.Mapper).LinkAsync(company.CompanyId,
                new LinkClientDto { Email = "KLIENT@example.com" }, CancellationToken.None);
        }

        await using var read = _db.NewContext();
        var service = new ClientUserService(read, _db.Mapper);
        Assert.Equal(2, (await service.GetCompaniesAsync(client.Id, CancellationToken.None)).Count);
        Assert.Equal(client.Id, Assert.Single(await service.GetForCompanyAsync(first.CompanyId, CancellationToken.None)).Id);
    }

    [Fact]
    public async Task Link_Twice_ThrowsConflict_UnknownEmail_ThrowsNotFound()
    {
        var company = await _db.AddCompanyAsync();
        await _db.AddClientAsync("klient@example.com");

        await using (var db = _db.NewContext())
            await new ClientUserService(db, _db.Mapper).LinkAsync(company.CompanyId,
                new LinkClientDto { Email = "klient@example.com" }, CancellationToken.None);

        await using var again = _db.NewContext();
        var service = new ClientUserService(again, _db.Mapper);
        await Assert.ThrowsAsync<ConflictException>(() => service.LinkAsync(company.CompanyId,
            new LinkClientDto { Email = "klient@example.com" }, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => service.LinkAsync(company.CompanyId,
            new LinkClientDto { Email = "brak@example.com" }, CancellationToken.None));
    }

    [Fact]
    public async Task Unlink_RemovesRelationOnly_KeepsClientAndOtherCompany()
    {
        var first = await _db.AddCompanyAsync("Pierwsza", "p@example.com");
        var second = await _db.AddCompanyAsync("Druga", "d@example.com");
        var client = await _db.AddClientAsync();
        foreach (var company in new[] { first, second })
        {
            await using var db = _db.NewContext();
            await new ClientUserService(db, _db.Mapper).LinkAsync(company.CompanyId,
                new LinkClientDto { Email = client.Email }, CancellationToken.None);
        }

        await using (var db = _db.NewContext())
            await new ClientUserService(db, _db.Mapper).UnlinkAsync(first.CompanyId, client.Id, CancellationToken.None);

        await using var check = _db.NewContext();
        Assert.True(await check.ClientUsers.AnyAsync(c => c.Id == client.Id));
        var companies = await new ClientUserService(check, _db.Mapper).GetCompaniesAsync(client.Id, CancellationToken.None);
        Assert.Equal(second.CompanyId, Assert.Single(companies).CompanyId);
    }
}
