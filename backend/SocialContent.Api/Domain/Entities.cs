namespace SocialContent.Api.Domain;

public class ManagerUser
{
    public Guid Id { get; private set; }
    public FirstName FirstName { get; private set; } = null!;
    public LastName LastName { get; private set; } = null!;
    public Age Age { get; private set; } = null!;
    public Description Description { get; private set; } = null!;

    /// <summary>Konto społecznościowe, do którego manager jest przypisany (SocialAccount 1 - n ManagerUser; opcjonalne).</summary>
    public Guid? SocialAccountId { get; private set; }
    public SocialAccount? SocialAccount { get; private set; }

    public List<Post> Posts { get; private set; } = new();

    private ManagerUser() { }

    public static ManagerUser Create(FirstName firstName, LastName lastName, Age age, Description description) =>
        new()
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Age = age,
            Description = description
        };

    public void Update<T>(T value) where T : notnull
    {
        switch (value)
        {
            case FirstName f: FirstName = f; break;
            case LastName l: LastName = l; break;
            case Age a: Age = a; break;
            case Description d: Description = d; break;
            default: throw new ArgumentException($"Nieobsługiwany typ aktualizacji: {typeof(T).Name}");
        }
    }

    /// <summary>
    /// Usuwa dane osobowe (prośba o skasowanie danych): imię i nazwisko zamienia na "Usunięty użytkownik",
    /// a opis na informację o usunięciu (Description nie może być pusty). Wiek, powiązania z postami
    /// i z kontem społecznościowym zostają nienaruszone - dzięki temu historia publikacji jest zachowana.
    /// Konto logowania usuwa serwis.
    /// </summary>
    public void Anonymize()
    {
        FirstName = FirstName.Create("Usunięty");
        LastName = LastName.Create("użytkownik");
        Description = Description.Create("Dane usunięte na prośbę użytkownika.");
    }
}

public abstract class FileType
{
    public Guid Id { get; protected set; }
    public FileName FileName { get; protected set; } = null!;
    public Format Format { get; protected set; }
    public Guid PostId { get; private set; }
}

public sealed class Picture : FileType
{
    public int SizeKb { get; private set; }

    private Picture() { }

    public static Picture Create(FileName fileName, Format format, int sizeKb)
    {
        if (sizeKb <= 0) throw new DomainValidationException("Rozmiar zdjęcia musi być większy od 0.");
        var picture = new Picture { Id = Guid.NewGuid(), FileName = fileName, Format = format, SizeKb = sizeKb };
        if (!Validators.ValidFileFormat(picture))
            throw new DomainValidationException($"Format {format} nie jest dozwolony dla zdjęcia.");
        return picture;
    }
}

public sealed class Video : FileType
{
    /// <summary>Czas trwania w sekundach.</summary>
    public int Length { get; private set; }

    private Video() { }

    public static Video Create(FileName fileName, Format format, int length)
    {
        if (length <= 0) throw new DomainValidationException("Długość wideo musi być większa od 0.");
        var video = new Video { Id = Guid.NewGuid(), FileName = fileName, Format = format, Length = length };
        if (!Validators.ValidFileFormat(video))
            throw new DomainValidationException($"Format {format} nie jest dozwolony dla wideo.");
        return video;
    }
}

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
