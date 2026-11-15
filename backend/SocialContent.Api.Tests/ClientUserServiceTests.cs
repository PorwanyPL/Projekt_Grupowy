using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Services;

namespace SocialContent.Api.Tests;

public class ClientUserServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Client_WithAcceptedAccountsInManyCompanies_IsManyToMany()
    {
        var first = await _db.AddCompanyAsync("Pierwsza", "p@example.com");
        var second = await _db.AddCompanyAsync("Druga", "d@example.com");
        var client = await _db.AddClientAsync("klient@example.com");
        // Relacja firma - klient powstaje wyłącznie ze strony klienta (konto + przyjęte zaproszenie).
        await _db.AddAccountForClientAsync(client.Id, first.CompanyId, "a-1", SocialPlatform.Facebook);
        await _db.AddAccountForClientAsync(client.Id, second.CompanyId, "a-2", SocialPlatform.Instagram);

        await using var read = _db.NewContext();
        var service = new ClientUserService(read, _db.Mapper);
        Assert.Equal(2, (await service.GetCompaniesAsync(client.Id, CancellationToken.None)).Count);
        Assert.Equal(client.Id, Assert.Single(await service.GetForCompanyAsync(first.CompanyId, CancellationToken.None)).Id);
    }

    [Fact]
    public async Task Unlink_RemovesRelationOnly_KeepsClientAndOtherCompany()
    {
        var first = await _db.AddCompanyAsync("Pierwsza", "p@example.com");
        var second = await _db.AddCompanyAsync("Druga", "d@example.com");
        var client = await _db.AddClientAsync();
        await _db.AddAccountForClientAsync(client.Id, first.CompanyId, "a-1", SocialPlatform.Facebook);
        await _db.AddAccountForClientAsync(client.Id, second.CompanyId, "a-2", SocialPlatform.Instagram);

        // Firma nie może odpiąć klienta, którego konta nadal obsługuje - zwalniamy je (odrzucenie zaproszenia) i dopiero odpinamy.
        await using (var db = _db.NewContext())
        {
            db.SocialAccounts.RemoveRange(db.SocialAccounts.Where(a => a.CompanyId == first.CompanyId));
            await db.SaveChangesAsync();
        }
        await using (var db = _db.NewContext())
            await new ClientUserService(db, _db.Mapper).UnlinkAsync(first.CompanyId, client.Id, CancellationToken.None);

        await using var check = _db.NewContext();
        Assert.True(await check.ClientUsers.AnyAsync(c => c.Id == client.Id));
        var companies = await new ClientUserService(check, _db.Mapper).GetCompaniesAsync(client.Id, CancellationToken.None);
        Assert.Equal(second.CompanyId, Assert.Single(companies).CompanyId);
    }
}
