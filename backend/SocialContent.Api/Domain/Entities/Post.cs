using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Domain.ValueObjects;

namespace SocialContent.Api.Domain.Entities;

/// <summary>
/// Post ogólny (bez platformy). Klasy FacebookPost, InstagramPost i TikTokPost dziedziczą po nim (TPH).
/// </summary>
public class Post
{
    public Guid Id { get; private set; }
    public Title Title { get; private set; } = null!;
    public CreatedAt CreatedAt { get; private set; } = null!;
    public Description Description { get; private set; } = null!;
    public PostStatus Status { get; private set; } = PostStatus.Draft;
    public FacebookPostType Visibility { get; private set; }
    public ScheduledTime? ScheduledAt { get; private set; }
    public ScheduledTime? PublishedAt { get; private set; }
    public ExternalId? ExternalPostId { get; private set; }
    public string? ErrorMessage { get; private set; }

    /// <summary>Konto docelowe publikacji (SocialAccount 1 - n Post; opcjonalne, bo szkic może go jeszcze nie mieć).</summary>
    public Guid? TargetAccountId { get; private set; }
    public SocialAccount? TargetAccount { get; private set; }

    public List<FileType> Files { get; private set; } = new();
    public List<ManagerUser> ManagerUsers { get; private set; } = new();

    public IReadOnlyList<Picture> Pictures => Files.OfType<Picture>().ToList();
    public IReadOnlyList<Video> Videos => Files.OfType<Video>().ToList();

    /// <summary>Platforma posta; null dla posta ogólnego. Nadpisywane w podklasach.</summary>
    public virtual SocialPlatform? Platform => null;

    protected Post() { }

    public static Post Create(Title title, Description description, FacebookPostType type,
        IEnumerable<Picture> pictures, IEnumerable<Video> videos, IEnumerable<ManagerUser> managers,
        ScheduledTime? scheduledAt, SocialAccount? targetAccount)
    {
        var post = new Post();
        post.InitNew();
        post.Apply(title, description, type, pictures, videos, managers, scheduledAt, targetAccount);
        return post;
    }

    public void Update(Title title, Description description, FacebookPostType type,
        IEnumerable<Picture> pictures, IEnumerable<Video> videos, IEnumerable<ManagerUser> managers,
        ScheduledTime? scheduledAt, SocialAccount? targetAccount) =>
        Apply(title, description, type, pictures, videos, managers, scheduledAt, targetAccount);

    /// <summary>Rozpoczyna publikację: Draft/Scheduled -> Publishing (wywoływane przez warstwę integracji z platformą).</summary>
    public void MarkPublishing()
    {
        if (Status is not (PostStatus.Draft or PostStatus.Scheduled))
            throw new DomainValidationException($"Nie można rozpocząć publikacji posta w statusie {Status}.");
        Status = PostStatus.Publishing;
        ErrorMessage = null;
    }

    /// <summary>Kończy publikację sukcesem: Publishing -> Published.</summary>
    public void MarkPublished(ExternalId externalPostId, ScheduledTime publishedAt)
    {
        if (Status != PostStatus.Publishing)
            throw new DomainValidationException($"Nie można oznaczyć jako opublikowany posta w statusie {Status}.");
        Status = PostStatus.Published;
        ExternalPostId = externalPostId;
        PublishedAt = publishedAt;
        ErrorMessage = null;
    }

    /// <summary>Kończy publikację błędem: Publishing -> Failed (komunikat jest przycinany do 1000 znaków).</summary>
    public void MarkFailed(string error)
    {
        if (Status != PostStatus.Publishing)
            throw new DomainValidationException($"Nie można oznaczyć jako nieudany posta w statusie {Status}.");
        var message = string.IsNullOrWhiteSpace(error) ? "Nieznany błąd publikacji." : error.Trim();
        Status = PostStatus.Failed;
        ErrorMessage = message.Length > 1000 ? message[..1000] : message;
    }

    protected void InitNew()
    {
        Id = Guid.NewGuid();
        CreatedAt = CreatedAt.Create(DateTime.UtcNow);
    }

    protected void Apply(Title title, Description description, FacebookPostType type,
        IEnumerable<Picture> pictures, IEnumerable<Video> videos, IEnumerable<ManagerUser> managers,
        ScheduledTime? scheduledAt, SocialAccount? targetAccount)
    {
        if (Status is PostStatus.Publishing or PostStatus.Published)
            throw new DomainValidationException($"Post w statusie {Status} nie może być edytowany.");

        var pics = pictures.ToList();
        var vids = videos.ToList();

        switch (type)
        {
            case FacebookPostType.Text when pics.Count > 0 || vids.Count > 0:
                throw new DomainValidationException("Post tekstowy nie może zawierać zdjęć ani wideo.");
            case FacebookPostType.Photo when pics.Count == 0 || vids.Count > 0:
                throw new DomainValidationException("Post typu Photo wymaga co najmniej jednego zdjęcia i nie może zawierać wideo.");
            case FacebookPostType.Video when vids.Count == 0:
                throw new DomainValidationException("Post typu Video wymaga co najmniej jednego wideo.");
        }

        if (targetAccount is not null && Platform is { } expected && targetAccount.Platform != expected)
            throw new DomainValidationException(
                $"Konto docelowe jest na platformie {targetAccount.Platform}, a post jest przeznaczony dla platformy {expected}.");

        // Termin sprawdzamy tylko wtedy, gdy się zmienia - edycja tytułu starego, zaplanowanego posta nie jest blokowana.
        if (scheduledAt is not null && !Equals(scheduledAt, ScheduledAt) && scheduledAt.Date <= DateTime.UtcNow)
            throw new DomainValidationException("Termin publikacji musi być w przyszłości.");

        Title = title;
        Description = description;
        Visibility = type;

        ScheduledAt = scheduledAt;
        Status = scheduledAt is null ? PostStatus.Draft : PostStatus.Scheduled;
        ErrorMessage = null; // edycja posta po nieudanej publikacji przywraca go do Draft/Scheduled

        TargetAccount = targetAccount;
        TargetAccountId = targetAccount?.Id;

        Files.Clear(); // osierocone pliki są usuwane przez EF (relacja wymagana)
        Files.AddRange(pics);
        Files.AddRange(vids);

        ManagerUsers.Clear();
        ManagerUsers.AddRange(managers.DistinctBy(m => m.Id));
    }
}
