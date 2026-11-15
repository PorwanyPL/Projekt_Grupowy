using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.ValueObjects;
using SocialContent.Api.Services;

namespace SocialContent.Api.Data;

/// <summary>
/// Dane demonstracyjne do ręcznych testów (firma, managerowie, klienci, konta i kilka postów, w tym post na dwie platformy z tagami).
/// Osobna klasa od <see cref="DatabaseInitializer"/>: initializer odpowiada za schemat bazy, seeder za jej zawartość.
/// Jest idempotentny (nic nie robi, gdy w bazie jest już jakakolwiek firma) i wywoływany tylko w środowisku Development.
/// Wszystkie konta demo mają hasło <see cref="DemoPassword"/>.
/// </summary>
public static class DatabaseSeeder
{
    public const string DemoPassword = "ToJestHaslo1!";

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

        // Klienci tworzą konta i zapraszają firmę. Pierwszy powierza akceptację postów menedżerowi (domyślnie),
        // drugi sam akceptuje posty - można więc przećwiczyć oba przepływy.
        var karol = ClientUser.Create(FirstName.Create("Karol"), LastName.Create("Klient"), Age.Create(41),
            Description.Create("Klient demonstracyjny"), "klient@demo.pl", hash);
        var karolina = ClientUser.Create(FirstName.Create("Karolina"), LastName.Create("Zielińska"), Age.Create(36),
            Description.Create("Klientka, która sama akceptuje posty"), "klient2@demo.pl", hash);
        // Klient bez współpracy z firmą: z nim może czatować tylko manager ds. czatu (Anna), nie Bartek.
        var marta = ClientUser.Create(FirstName.Create("Marta"), LastName.Create("Obca"), Age.Create(28),
            Description.Create("Klientka niewspółpracująca z firmą (test reguł czatu)"), "klient3@demo.pl", hash);
        company.AddClient(karol).SetAlias("Kawiarnia Pod Dębem");
        company.AddClient(karolina).SetAlias("Sklep Karoliny");
        company.Clients.Single(c => c.ClientId == karolina.Id).SetApprovalMode(PostApprovalMode.Client);

        var expires = DateTime.UtcNow.AddDays(60);
        var facebook = ManagedAccount(karol, company, "demo-fb", SocialPlatform.Facebook, "fb-1001", "Demo Facebook", expires, anna);
        var instagram = ManagedAccount(karol, company, "demo-ig", SocialPlatform.Instagram, "ig-2002", "Demo Instagram", expires, anna, bartek);
        var tiktok = ManagedAccount(karolina, company, "demo-tt", SocialPlatform.TikTok, "tt-3003", "Demo TikTok", expires, bartek);
        // Zaproszenie czekające na decyzję firmy (zobaczysz je w widoku firmy).
        var pending = SocialAccount.Create(karol.Id, company.CompanyId, "demo-ig-2", SocialPlatform.Instagram, "ig-4004",
            "Nowy Instagram (zaproszenie)", "demo-token-ig2", null, expires, true, Array.Empty<ManagerUser>());

        var draft = Post.Create(Title.Create("Szkic na start"), Description.Create("Pierwszy post demonstracyjny."),
            FacebookPostType.Text, Array.Empty<Picture>(), Array.Empty<Video>(), new[] { anna }, null, facebook);
        var scheduled = Post.Create(Title.Create("Zaplanowana premiera"),
            Description.Create("Post zaplanowany na za dwa dni."),
            FacebookPostType.Text, Array.Empty<Picture>(), Array.Empty<Video>(), new[] { anna, bartek },
            ScheduledTime.Create(DateTime.UtcNow.AddDays(2)), instagram);
        scheduled.SetTags(new[] { "premiera", "kawa" });

        // Jeden post na dwie platformy (Facebook + Instagram): dwa warianty ze wspólnym GroupId, widoczne jako jeden wpis.
        var groupFacebook = Post.Create(Title.Create("Weekend w kawiarni"),
            Description.Create("Wspólny post na Facebooka i Instagram - zaplanowany za trzy dni."),
            FacebookPostType.Text, Array.Empty<Picture>(), Array.Empty<Video>(), new[] { anna },
            ScheduledTime.Create(DateTime.UtcNow.AddDays(3)), facebook);
        var groupInstagram = Post.Create(Title.Create("Weekend w kawiarni"),
            Description.Create("Wspólny post na Facebooka i Instagram - zaplanowany za trzy dni."),
            FacebookPostType.Text, Array.Empty<Picture>(), Array.Empty<Video>(), new[] { anna, bartek },
            ScheduledTime.Create(DateTime.UtcNow.AddDays(3)), instagram);
        groupInstagram.AssignGroup(groupFacebook.GroupId);
        groupFacebook.SetTags(new[] { "weekend", "kawa" });
        groupInstagram.SetTags(new[] { "weekend", "kawa" });

        // Opublikowany post (widoczny w kalendarzu i filtrze "Opublikowany").
        var published = Post.Create(Title.Create("Dziękujemy za wizytę"),
            Description.Create("Post opublikowany wczoraj."),
            FacebookPostType.Text, Array.Empty<Picture>(), Array.Empty<Video>(), new[] { anna }, null, facebook);
        published.MarkPublishing();
        published.MarkPublished(ExternalId.Create("sim-demo-1"), ScheduledTime.Create(DateTime.UtcNow.AddDays(-1)));
        published.SetTags(new[] { "podziękowania" });

        var awaiting = Post.Create(Title.Create("Nowa kolekcja - do akceptacji"),
            Description.Create("Post czeka na akceptację klientki."),
            FacebookPostType.Text, Array.Empty<Picture>(), Array.Empty<Video>(), new[] { bartek }, null, tiktok);
        awaiting.SubmitForApproval();

        // Czat (v8): Anna jest managerem ds. czatu, w firmie jest grupa zespołu, a z Karolem rozmawiają firma i Anna.
        company.SetChatManager(anna.Id);
        var now = DateTime.UtcNow;
        var team = Conversation.CreateGroup(company.CompanyId, "Zespół Demo Firma", new (Guid, UserRole)[]
        {
            (company.CompanyId, UserRole.CompanyUser), (anna.Id, UserRole.ManagerUser), (bartek.Id, UserRole.ManagerUser)
        });
        var withFirm = Conversation.CreateDirect((karol.Id, UserRole.ClientUser), (company.CompanyId, UserRole.CompanyUser));
        var withAnna = Conversation.CreateDirect((karol.Id, UserRole.ClientUser), (anna.Id, UserRole.ManagerUser));
        var chat = new[]
        {
            ChatMessage.Create(team.Id, company.CompanyId, UserRole.CompanyUser, company.Name,
                "Cześć zespół, od dziś Anna odpowiada na czacie za wszystkich klientów.", now.AddHours(-3)),
            ChatMessage.Create(team.Id, anna.Id, UserRole.ManagerUser, "Anna Nowak", "Jasne, biorę to na siebie.", now.AddHours(-2)),
            ChatMessage.Create(team.Id, bartek.Id, UserRole.ManagerUser, "Bartek Kowalski",
                "Ja zajmuję się TikTokiem Karoliny.", now.AddHours(-1)),
            ChatMessage.Create(withFirm.Id, karol.Id, UserRole.ClientUser, "Karol Klient",
                "Dzień dobry, kiedy ruszy kampania weekendowa?", now.AddMinutes(-50)),
            ChatMessage.Create(withFirm.Id, company.CompanyId, UserRole.CompanyUser, company.Name,
                "Dzień dobry, post jest zaplanowany na weekend.", now.AddMinutes(-40)),
            ChatMessage.Create(withAnna.Id, anna.Id, UserRole.ManagerUser, "Anna Nowak",
                "Cześć Karol, mam pytanie o zdjęcia do postu.", now.AddMinutes(-20))
        };

        db.CompanyUsers.Add(company);
        db.Conversations.AddRange(team, withFirm, withAnna);
        db.ChatMessages.AddRange(chat);
        // Powiadomienia demo (bez nich dzwonek jest pusty, bo powstają tylko przy zdarzeniach).
        db.Notifications.AddRange(
            Notification.Create(company.CompanyId, UserRole.CompanyUser, published.GroupId,
                "Post \"Dziękujemy za wizytę\" został opublikowany."),
            Notification.Create(anna.Id, UserRole.ManagerUser, groupFacebook.GroupId,
                "Post \"Weekend w kawiarni\" jest zaplanowany."),
            Notification.Create(karolina.Id, UserRole.ClientUser, awaiting.GroupId,
                "Post \"Nowa kolekcja - do akceptacji\" czeka na Twoją akceptację."));
        db.ManagerUsers.AddRange(anna, bartek);
        db.ManagerCredentials.AddRange(
            ManagerCredential.Create(anna, "anna@demo.pl", hash),
            ManagerCredential.Create(bartek, "bartek@demo.pl", hash));
        db.ClientUsers.AddRange(karol, karolina, marta);
        db.SocialAccounts.AddRange(facebook, instagram, tiktok, pending);
        db.Posts.AddRange(draft, scheduled, groupFacebook, groupInstagram, published, awaiting);
        db.SaveChanges();

        logger.LogInformation(
            "Seeder: dodano dane demo. Logowanie (hasło {Password}): firma@demo.pl, anna@demo.pl (manager ds. czatu), bartek@demo.pl, klient@demo.pl, klient2@demo.pl, klient3@demo.pl (bez współpracy z firmą).",
            DemoPassword);
    }

    /// <summary>Konto klienta z przyjętym zaproszeniem firmy i przypisanymi managerami.</summary>
    private static SocialAccount ManagedAccount(ClientUser owner, CompanyUser company, string accountId,
        SocialPlatform platform, string externalId, string name, DateTime expires, params ManagerUser[] managers)
    {
        var account = SocialAccount.Create(owner.Id, company.CompanyId, accountId, platform, externalId, name,
            "demo-token-" + accountId, null, expires, true, Array.Empty<ManagerUser>());
        account.AcceptInvitation();
        account.Update(account.AccountId, account.Platform, account.ExternalPlatformId, account.AccountName,
            null, null, account.TokenExpiresAt, account.IsActive, managers);
        return account;
    }
}
