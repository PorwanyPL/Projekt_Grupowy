using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.ValueObjects;
using SocialContent.Api.Mapping;

namespace SocialContent.Api.Tests;

/// <summary>Baza SQLite w pamięci (jedno otwarte połączenie na test) z prawdziwym schematem aplikacji.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    /// <summary>Konfiguracja AutoMappera z wszystkimi profilami aplikacji.</summary>
    public static readonly MapperConfiguration MapperConfig =
        new(cfg => cfg.AddMaps(typeof(PostProfile).Assembly));

    public IMapper Mapper { get; } = MapperConfig.CreateMapper();

    public TestDb()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    /// <summary>Nowy kontekst na tym samym połączeniu - pozwala sprawdzić, co faktycznie trafiło do bazy.</summary>
    public AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    public async Task<CompanyUser> AddCompanyAsync(string name = "Firma Testowa", string email = "firma@example.com")
    {
        await using var db = NewContext();
        var company = CompanyUser.Create(name, email, "x");
        db.CompanyUsers.Add(company);
        await db.SaveChangesAsync();
        return company;
    }

    public async Task<ManagerUser> AddManagerAsync(Guid companyId, string first = "Anna", string last = "Nowak")
    {
        await using var db = NewContext();
        var manager = ManagerUser.Create(
            FirstName.Create(first), LastName.Create(last), Age.Create(30), Description.Create("Opis managera"), companyId);
        db.ManagerUsers.Add(manager);
        await db.SaveChangesAsync();
        return manager;
    }

    public async Task<ClientUser> AddClientAsync(string email = "klient@example.com")
    {
        await using var db = NewContext();
        var client = ClientUser.Create(
            FirstName.Create("Karol"), LastName.Create("Klient"), Age.Create(40), Description.Create("Opis klienta"), email, "x");
        db.ClientUsers.Add(client);
        await db.SaveChangesAsync();
        return client;
    }

    public async Task AddCredentialAsync(Guid managerId, string email, string passwordHash = "x")
    {
        await using var db = NewContext();
        var manager = await db.ManagerUsers.FirstAsync(m => m.Id == managerId);
        db.ManagerCredentials.Add(ManagerCredential.Create(manager, email, passwordHash));
        await db.SaveChangesAsync();
    }

    public async Task<Guid> AddSocialAccountAsync(Guid companyId, string accountId = "acc-1",
        SocialPlatform platform = SocialPlatform.Facebook, params Guid[] managerIds)
    {
        await using var db = NewContext();
        var managers = await db.ManagerUsers.Where(m => managerIds.Contains(m.Id)).ToListAsync();
        var account = SocialAccount.Create(companyId, accountId, platform, "ext-1", "Konto testowe", "token",
            null, DateTime.UtcNow.AddDays(30), true, managers);
        db.SocialAccounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    public async Task<Guid> AddPostWithManagerAsync(Guid managerId, Guid? accountId = null)
    {
        await using var db = NewContext();
        var manager = await db.ManagerUsers.FirstAsync(m => m.Id == managerId);
        var account = accountId is null ? null : await db.SocialAccounts.FirstAsync(a => a.Id == accountId);
        var post = Post.Create(Title.Create("Tytuł"), Description.Create("Treść posta"), FacebookPostType.Text,
            Array.Empty<Picture>(), Array.Empty<Video>(), new[] { manager }, null, account);
        db.Posts.Add(post);
        await db.SaveChangesAsync();
        return post.Id;
    }

    public void Dispose() => _connection.Dispose();
}
