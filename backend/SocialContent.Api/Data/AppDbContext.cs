using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Domain;

namespace SocialContent.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ManagerUser> ManagerUsers => Set<ManagerUser>();
    public DbSet<ManagerCredential> ManagerCredentials => Set<ManagerCredential>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<FileType> FileTypes => Set<FileType>();
    public DbSet<SocialAccount> SocialAccounts => Set<SocialAccount>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ManagerUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.FirstName).HasConversion(v => v.Value, s => FirstName.Create(s)).HasMaxLength(50).IsRequired();
            e.Property(x => x.LastName).HasConversion(v => v.Value, s => LastName.Create(s)).HasMaxLength(50).IsRequired();
            e.Property(x => x.Age).HasConversion(v => v.Value, i => Age.Create(i)).IsRequired();
            e.Property(x => x.Description).HasConversion(v => v.Value, s => Description.Create(s)).HasMaxLength(2000).IsRequired();
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

            e.HasIndex(x => new { x.Platform, x.AccountId }).IsUnique();

            // SocialAccount 1 - n ManagerUser: usunięcie konta odpina managerów, nie usuwa ich.
            e.HasMany(x => x.ManagerUsers).WithOne(m => m.SocialAccount).HasForeignKey(m => m.SocialAccountId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);

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

        b.Entity<FileType>(e =>
        {
            e.ToTable("FileTypes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.FileName).HasConversion(v => v.Value, s => FileName.Create(s)).HasMaxLength(150).IsRequired();
            e.Property(x => x.Format).HasConversion<string>().HasMaxLength(10).IsRequired();
            e.HasDiscriminator<string>("Kind")
                .HasValue<Picture>("Picture")
                .HasValue<Video>("Video");
        });
    }
}
