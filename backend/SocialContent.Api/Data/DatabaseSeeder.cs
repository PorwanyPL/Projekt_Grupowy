using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.ValueObjects;
using SocialContent.Api.Services;

namespace SocialContent.Api.Data;

/// <summary>
/// Dane demonstracyjne do ręcznych testów (firma, managerowie, klient, konta i kilka postów).
/// Osobna klasa od <see cref="DatabaseInitializer"/>: initializer odpowiada za schemat bazy, seeder za jej zawartość.
/// Jest idempotentny (nic nie robi, gdy w bazie jest już jakakolwiek firma) i wywoływany tylko w środowisku Development.
/// Wszystkie konta demo mają hasło <see cref="DemoPassword"/>.
/// </summary>
public static class DatabaseSeeder
{
    public const string DemoPassword = "Haslo1234";

    public static void Seed(AppDbContext db, IPasswordHasher hasher, ILogger logger)
    {
        if (db.CompanyUsers.Any())
        {
            logger.LogInformation("Seeder: baza zawiera już dane, pomijam.");
            return;
        }

        var hash = hasher.Hash(DemoPassword);

        var company = CompanyUser.Create("Demo Firma", "firma@demo.pl", hash);

        var anna = ManagerUser.Create(FirstName.Create("Anna"), LastName.Create("Nowak"), Age.Create(29),
            Description.Create("Content manager - Facebook i Instagram"), company.CompanyId);
        var bartek = ManagerUser.Create(FirstName.Create("Bartek"), LastName.Create("Kowalski"), Age.Create(34),
            Description.Create("Content manager - TikTok i Instagram"), company.CompanyId);

        var client = ClientUser.Create(FirstName.Create("Karol"), LastName.Create("Klient"), Age.Create(41),
            Description.Create("Klient demonstracyjny"), "klient@demo.pl", hash);
        company.AddClient(client);

        var expires = DateTime.UtcNow.AddDays(60);
        var facebook = SocialAccount.Create(company.CompanyId, "demo-fb", SocialPlatform.Facebook, "fb-1001",
            "Demo Facebook", "demo-token-fb", null, expires, true, new[] { anna });
        var instagram = SocialAccount.Create(company.CompanyId, "demo-ig", SocialPlatform.Instagram, "ig-2002",
            "Demo Instagram", "demo-token-ig", null, expires, true, new[] { anna, bartek });
        var tiktok = SocialAccount.Create(company.CompanyId, "demo-tt", SocialPlatform.TikTok, "tt-3003",
            "Demo TikTok", "demo-token-tt", null, expires, true, new[] { bartek });

        var draft = Post.Create(Title.Create("Szkic na start"), Description.Create("Pierwszy post demonstracyjny."),
            FacebookPostType.Text, Array.Empty<Picture>(), Array.Empty<Video>(), new[] { anna }, null, facebook);
        var scheduled = Post.Create(Title.Create("Zaplanowana premiera"),
            Description.Create("Post zaplanowany na za dwa dni."),
            FacebookPostType.Text, Array.Empty<Picture>(), Array.Empty<Video>(), new[] { anna, bartek },
            ScheduledTime.Create(DateTime.UtcNow.AddDays(2)), instagram);

        db.CompanyUsers.Add(company);
        db.ManagerUsers.AddRange(anna, bartek);
        db.ManagerCredentials.AddRange(
            ManagerCredential.Create(anna, "anna@demo.pl", hash),
            ManagerCredential.Create(bartek, "bartek@demo.pl", hash));
        db.ClientUsers.Add(client);
        db.SocialAccounts.AddRange(facebook, instagram, tiktok);
        db.Posts.AddRange(draft, scheduled);
        db.SaveChanges();

        logger.LogInformation(
            "Seeder: dodano dane demo. Logowanie (hasło {Password}): firma@demo.pl, anna@demo.pl, bartek@demo.pl, klient@demo.pl.",
            DemoPassword);
    }
}