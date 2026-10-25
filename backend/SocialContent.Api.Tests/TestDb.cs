using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain;

namespace SocialContent.Api.Tests;

/// <summary>Baza SQLite w pamięci (jedno otwarte połączenie na test) z prawdziwym schematem aplikacji.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

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

    public async Task<ManagerUser> AddManagerAsync(string first = "Anna", string last = "Nowak")
    {
        await using var db = NewContext();
        var manager = ManagerUser.Create(
            FirstName.Create(first), LastName.Create(last), Age.Create(30), Description.Create("Opis managera"));
        db.ManagerUsers.Add(manager);
        await db.SaveChangesAsync();
        return manager;
    }

    public async Task AddCredentialAsync(Guid managerId, string email, string passwordHash = "x")
    {
        await using var db = NewContext();
        var manager = await db.ManagerUsers.FirstAsync(m => m.Id == managerId);
        db.ManagerCredentials.Add(ManagerCredential.Create(manager, email, passwordHash));
        await db.SaveChangesAsync();
    }

    public async Task<Guid> AddPostWithManagerAsync(Guid managerId)
    {
        await using var db = NewContext();
        var manager = await db.ManagerUsers.FirstAsync(m => m.Id == managerId);
        var post = Post.Create(Title.Create("Tytuł"), Description.Create("Treść posta"), FacebookPostType.Text,
            Array.Empty<Picture>(), Array.Empty<Video>(), new[] { manager }, null, null);
        db.Posts.Add(post);
        await db.SaveChangesAsync();
        return post.Id;
    }

    public async Task<Guid> AddSocialAccountWithManagerAsync(Guid managerId)
    {
        await using var db = NewContext();
        var manager = await db.ManagerUsers.FirstAsync(m => m.Id == managerId);
        var account = SocialAccount.Create("acc-1", SocialPlatform.Facebook, "fb-1", "Konto testowe", "token",
            null, DateTime.UtcNow.AddDays(30), true, new[] { manager });
        db.SocialAccounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    public void Dispose() => _connection.Dispose();
}
