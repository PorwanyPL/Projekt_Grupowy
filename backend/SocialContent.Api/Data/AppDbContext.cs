using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Domain;

namespace SocialContent.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ManagerUser> ManagerUsers => Set<ManagerUser>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<FileType> FileTypes => Set<FileType>();

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

        b.Entity<Post>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Title).HasConversion(v => v.Value, s => Title.Create(s)).HasMaxLength(120).IsRequired();
            e.Property(x => x.CreatedAt).HasConversion(v => v.Date, d => CreatedAt.Create(d)).IsRequired();
            e.Property(x => x.Description).HasConversion(v => v.Value, s => Description.Create(s)).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(20).IsRequired();

            e.Ignore(x => x.Pictures);
            e.Ignore(x => x.Videos);

            e.HasMany(x => x.Files).WithOne().HasForeignKey(f => f.PostId).IsRequired().OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.ManagerUsers).WithMany(m => m.Posts);
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
