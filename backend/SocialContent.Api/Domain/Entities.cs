namespace SocialContent.Api.Domain;

public class ManagerUser
{
    public Guid Id { get; private set; }
    public FirstName FirstName { get; private set; } = null!;
    public LastName LastName { get; private set; } = null!;
    public Age Age { get; private set; } = null!;
    public Description Description { get; private set; } = null!;
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

public class Post
{
    public Guid Id { get; private set; }
    public Title Title { get; private set; } = null!;
    public CreatedAt CreatedAt { get; private set; } = null!;
    public Description Description { get; private set; } = null!;
    public FacebookPostType Visibility { get; private set; }
    public List<FileType> Files { get; private set; } = new();
    public List<ManagerUser> ManagerUsers { get; private set; } = new();

    public IReadOnlyList<Picture> Pictures => Files.OfType<Picture>().ToList();
    public IReadOnlyList<Video> Videos => Files.OfType<Video>().ToList();

    private Post() { }

    public static Post Create(Title title, Description description, FacebookPostType type,
        IEnumerable<Picture> pictures, IEnumerable<Video> videos, IEnumerable<ManagerUser> managers)
    {
        var post = new Post { Id = Guid.NewGuid(), CreatedAt = CreatedAt.Create(DateTime.UtcNow) };
        post.Apply(title, description, type, pictures, videos, managers);
        return post;
    }

    public void Update(Title title, Description description, FacebookPostType type,
        IEnumerable<Picture> pictures, IEnumerable<Video> videos, IEnumerable<ManagerUser> managers) =>
        Apply(title, description, type, pictures, videos, managers);

    private void Apply(Title title, Description description, FacebookPostType type,
        IEnumerable<Picture> pictures, IEnumerable<Video> videos, IEnumerable<ManagerUser> managers)
    {
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

        Title = title;
        Description = description;
        Visibility = type;

        Files.Clear(); // osierocone pliki są usuwane przez EF (relacja wymagana)
        Files.AddRange(pics);
        Files.AddRange(vids);

        ManagerUsers.Clear();
        ManagerUsers.AddRange(managers.DistinctBy(m => m.Id));
    }
}
