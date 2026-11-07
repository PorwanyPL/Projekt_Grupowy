using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.ValueObjects;

namespace SocialContent.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<CompanyUser> CompanyUsers => Set<CompanyUser>();
    public DbSet<ClientUser> ClientUsers => Set<ClientUser>();
    public DbSet<ManagerUser> ManagerUsers => Set<ManagerUser>();
    public DbSet<ManagerCredential> ManagerCredentials => Set<ManagerCredential>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<FileType> FileTypes => Set<FileType>();
    public DbSet<SocialAccount> SocialAccounts => Set<SocialAccount>();
    public DbSet<PostEditLock> PostEditLocks => Set<PostEditLock>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<CompanyUser>(e =>
        {
            e.HasKey(x => x.CompanyId);
            e.Property(x => x.CompanyId).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Email).HasMaxLength(254).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
            e.Property(x => x.IsAccessBlocked).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();

            // CompanyUser n - m ClientUser: usunięcie powiązania nie usuwa klienta ani firmy.
            e.HasMany(x => x.ClientUsers).WithMany(c => c.CompanyUsers);
        });

        b.Entity<ClientUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.FirstName).HasConversion(v => v.Value, s => FirstName.Create(s)).HasMaxLength(50).IsRequired();
            e.Property(x => x.LastName).HasConversion(v => v.Value, s => LastName.Create(s)).HasMaxLength(50).IsRequired();
            e.Property(x => x.Age).HasConversion(v => v.Value, i => Age.Create(i)).IsRequired();
            e.Property(x => x.Description).HasConversion(v => v.Value, s => Description.Create(s)).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Email).HasMaxLength(254).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
        });

        b.Entity<ManagerUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.FirstName).HasConversion(v => v.Value, s => FirstName.Create(s)).HasMaxLength(50).IsRequired();
            e.Property(x => x.LastName).HasConversion(v => v.Value, s => LastName.Create(s)).HasMaxLength(50).IsRequired();
            e.Property(x => x.Age).HasConversion(v => v.Value, i => Age.Create(i)).IsRequired();
            e.Property(x => x.Description).HasConversion(v => v.Value, s => Description.Create(s)).HasMaxLength(2000).IsRequired();

            // CompanyUser 1 - n ManagerUser: firma z managerami nie może zniknąć po cichu.
            e.HasOne(x => x.CompanyUser).WithMany(c => c.ManagerUsers).HasForeignKey(x => x.CompanyId)
                .IsRequired().OnDelete(DeleteBehavior.Restrict);

            // ManagerUser n - m SocialAccount (tabela łącząca tworzona przez EF).
            e.HasMany(x => x.SocialAccounts).WithMany(a => a.ManagerUsers);
        });

        // Dane logowania: 1:1 z ManagerUser, usuwane razem z nim.
        b.Entity<ManagerCredential>(e =>
        {
            e.HasKey(x => x.ManagerUserId);
            e.Property(x => x.ManagerUserId).ValueGeneratedNever();
            e.Property(x => x.Email).HasMaxLength(254).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
            e.Property(x => x.IsAccessBlocked).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.HasOne(x => x.ManagerUser).WithOne().HasForeignKey<ManagerCredential>(x => x.ManagerUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SocialAccount>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.AccountId).HasMaxLength(100).IsRequired();
            e.Property(x => x.Platform).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.ExternalPlatformId).HasMaxLength(100).IsRequired();
            e.Property(x => x.AccountName).HasMaxLength(150).IsRequired();
            e.Property(x => x.AccessToken).HasMaxLength(2000).IsRequired();
            e.Property(x => x.RefreshToken).HasMaxLength(2000);
            e.Property(x => x.TokenExpiresAt).IsRequired();
            e.Property(x => x.IsActive).IsRequired();
            e.Property(x => x.ErrorMessage).HasMaxLength(1000);

            e.Ignore(x => x.Pictures);
            e.Ignore(x => x.Videos);

            // Identyfikator konta jest unikalny w obrębie platformy i firmy (różne firmy mogą mieć to samo id).
            e.HasIndex(x => new { x.CompanyId, x.Platform, x.AccountId }).IsUnique();

            // CompanyUser 1 - n SocialAccount.
            e.HasOne(x => x.CompanyUser).WithMany(c => c.SocialAccounts).HasForeignKey(x => x.CompanyId)
                .IsRequired().OnDelete(DeleteBehavior.Restrict);

            // SocialAccount 1 - n Post: konta z postami nie można usunąć (sprawdza to serwis, baza jest ostatnią zaporą).
            e.HasMany(x => x.Posts).WithOne(p => p.TargetAccount).HasForeignKey(p => p.TargetAccountId)
                .IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Post>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Title).HasConversion(v => v.Value, s => Title.Create(s)).HasMaxLength(120).IsRequired();
            e.Property(x => x.CreatedAt).HasConversion(v => v.Date, d => CreatedAt.Create(d)).IsRequired();
            e.Property(x => x.Description).HasConversion(v => v.Value, s => Description.Create(s)).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.ScheduledAt).HasConversion(v => v!.Date, d => ScheduledTime.Create(d));
            e.Property(x => x.PublishedAt).HasConversion(v => v!.Date, d => ScheduledTime.Create(d));
            e.Property(x => x.ExternalPostId).HasConversion(v => v!.Value, s => ExternalId.Create(s)).HasMaxLength(100);
            e.Property(x => x.ErrorMessage).HasMaxLength(1000);

            e.Ignore(x => x.Pictures);
            e.Ignore(x => x.Videos);
            e.Ignore(x => x.Platform);

            e.HasMany(x => x.Files).WithOne().HasForeignKey(f => f.PostId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.ManagerUsers).WithMany(m => m.Posts);

            // Dziedziczenie Post -> FacebookPost / InstagramPost / TikTokPost: jedna tabela (TPH).
            e.HasDiscriminator<string>("PostKind")
                .HasValue<Post>("Post")
                .HasValue<FacebookPost>("Facebook")
                .HasValue<InstagramPost>("Instagram")
                .HasValue<TikTokPost>("TikTok");
        });

        b.Entity<FacebookPost>(e =>
        {
            e.Property(x => x.TargetPageId).HasConversion(v => v.Value, s => ExternalId.Create(s)).HasMaxLength(100);
            e.Property(x => x.LinkUrl).HasConversion(v => v!.Url, s => WebUrl.Create(s)).HasMaxLength(2000);
            e.Property(x => x.PrivacySetting).HasMaxLength(100);
            e.Property(x => x.FeedTargeting).HasMaxLength(500);
            e.Property(x => x.PostType).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<InstagramPost>(e =>
        {
            e.Property(x => x.InstagramAccountId).HasConversion(v => v.Value, s => ExternalId.Create(s)).HasMaxLength(100);
            e.Property(x => x.MediaType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.LocationId).HasConversion(v => v!.Value, s => ExternalId.Create(s)).HasMaxLength(100);
        });

        b.Entity<TikTokPost>(e =>
        {
            e.Property(x => x.TikTokAccountId).HasConversion(v => v.Value, s => ExternalId.Create(s)).HasMaxLength(100);
            e.Property(x => x.PrivacyLevel).HasConversion<string>().HasMaxLength(30);
        });

        // Blokada edycji posta: jeden wiersz na post (klucz = PostId), więc dwie równoczesne blokady są niemożliwe.
        b.Entity<PostEditLock>(e =>
        {
            e.HasKey(x => x.PostId);
            e.Property(x => x.PostId).ValueGeneratedNever();
            e.Property(x => x.HolderRole).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.HolderName).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.ExpiresAt);
        });

        b.Entity<Notification>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.RecipientRole).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.Message).HasMaxLength(500).IsRequired();
            e.HasIndex(x => new { x.RecipientId, x.RecipientRole, x.IsRead });
        });

        b.Entity<FileType>(e =>
        {
            e.ToTable("FileTypes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.FileName).HasConversion(v => v.Value, s => FileName.Create(s)).HasMaxLength(150).IsRequired();
            e.Property(x => x.Format).HasConversion<string>().HasMaxLength(10).IsRequired();
            e.Property(x => x.SizeKb).IsRequired();
            e.Property(x => x.StorageKey).HasMaxLength(60).IsRequired();
            e.HasDiscriminator<string>("Kind")
                .HasValue<Picture>("Picture")
                .HasValue<Video>("Video");
        });
    }
}
