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

    /// <summary>
    /// Identyfikator logicznego posta. Post przeznaczony na kilka platform to kilka wierszy Post (po jednym na konto),
    /// które mają wspólny GroupId; dla zwykłego posta GroupId == Id. W API i w blokadach edycji "id posta" to GroupId.
    /// </summary>
    public Guid GroupId { get; private set; }

    public Title Title { get; private set; } = null!;
    public CreatedAt CreatedAt { get; private set; } = null!;
    public Description Description { get; private set; } = null!;
    public PostStatus Status { get; private set; } = PostStatus.Draft;
    public FacebookPostType Visibility { get; private set; }
    public ScheduledTime? ScheduledAt { get; private set; }
    public ScheduledTime? PublishedAt { get; private set; }
    public ExternalId? ExternalPostId { get; private set; }
    public string? ErrorMessage { get; private set; }

    /// <summary>Uwagi klienta z poprawkami, wpisane przy odrzuceniu posta (v9). Znikają po edycji posta lub akceptacji.</summary>
    public string? RejectionComment { get; private set; }

    public const int MaxRejectionCommentLength = 1000;

    /// <summary>Konto docelowe publikacji (SocialAccount 1 - n Post; opcjonalne, bo szkic może go jeszcze nie mieć).</summary>
    public Guid? TargetAccountId { get; private set; }
    public SocialAccount? TargetAccount { get; private set; }

    /// <summary>Tagi treści zapisane w jednej kolumnie (jeden tag w wierszu); używaj <see cref="Tags"/> i <see cref="SetTags"/>.</summary>
    public string TagsText { get; private set; } = "";

    public IReadOnlyList<string> Tags =>
        TagsText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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

    /// <summary>Wysyła post do akceptacji klienta: Draft/Scheduled -> AwaitingApproval (termin, jeśli był, zostaje).</summary>
    public void SubmitForApproval()
    {
        if (Status is not (PostStatus.Draft or PostStatus.Scheduled))
            throw new DomainValidationException($"Nie można wysłać do akceptacji posta w statusie {Status}.");
        Status = PostStatus.AwaitingApproval;
    }

    /// <summary>Akceptacja klienta: AwaitingApproval -> Scheduled (termin w przyszłości) albo Draft (do natychmiastowej publikacji).</summary>
    public void Approve(DateTime utcNow)
    {
        if (Status != PostStatus.AwaitingApproval)
            throw new DomainValidationException("Ten post nie oczekuje na akceptację.");
        Status = ScheduledAt is not null && ScheduledAt.Date > utcNow ? PostStatus.Scheduled : PostStatus.Draft;
        if (Status == PostStatus.Draft) ScheduledAt = null;
        RejectionComment = null;
    }

    /// <summary>
    /// Odrzucenie przez klienta: AwaitingApproval -> Rejected (autorzy mogą poprawić post i wysłać go ponownie).
    /// Opcjonalne uwagi z poprawkami (do 1000 znaków) trafiają do autorów razem z powiadomieniem.
    /// </summary>
    public void Reject(string? comment = null)
    {
        if (Status != PostStatus.AwaitingApproval)
            throw new DomainValidationException("Ten post nie oczekuje na akceptację.");
        var text = comment?.Trim();
        if (text is { Length: > MaxRejectionCommentLength })
            throw new DomainValidationException($"Uwagi mogą mieć maksymalnie {MaxRejectionCommentLength} znaków.");
        Status = PostStatus.Rejected;
        RejectionComment = string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>Dołącza ten wariant do grupy innego posta (post na wiele platform).</summary>
    public void AssignGroup(Guid groupId)
    {
        if (groupId == Guid.Empty) throw new DomainValidationException("Identyfikator grupy posta jest nieprawidłowy.");
        GroupId = groupId;
    }

    public const int MaxTags = 10;
    public const int MaxTagLength = 30;

    /// <summary>Ustawia tagi treści: przycina spacje, usuwa puste i powtórzone (bez względu na wielkość liter).</summary>
    public void SetTags(IEnumerable<string>? tags)
    {
        var list = (tags ?? Enumerable.Empty<string>())
            .Select(t => (t ?? "").Trim().TrimStart('#').Trim())
            .Where(t => t.Length > 0)
            .DistinctBy(t => t.ToLowerInvariant())
            .ToList();
        if (list.Count > MaxTags)
            throw new DomainValidationException($"Post może mieć maksymalnie {MaxTags} tagów.");
        foreach (var t in list)
        {
            if (t.Length > MaxTagLength)
                throw new DomainValidationException($"Tag może mieć maksymalnie {MaxTagLength} znaków.");
            if (t.Any(char.IsControl))
                throw new DomainValidationException("Tag nie może zawierać znaków sterujących ani nowych linii.");
        }
        TagsText = string.Join('\n', list);
    }

    protected void InitNew()
    {
        Id = Guid.NewGuid();
        GroupId = Id;
        CreatedAt = CreatedAt.Create(DateTime.UtcNow);
    }

    protected void Apply(Title title, Description description, FacebookPostType type,
        IEnumerable<Picture> pictures, IEnumerable<Video> videos, IEnumerable<ManagerUser> managers,
        ScheduledTime? scheduledAt, SocialAccount? targetAccount)
    {
        // Opublikowany post można edytować (zmiana treści i mediów), ale nie jego terminu - zostaje Published.
        if (Status == PostStatus.Publishing)
            throw new DomainValidationException("Post jest w trakcie publikacji i nie może być teraz edytowany.");
        if (Status == PostStatus.Published && scheduledAt is not null)
            throw new DomainValidationException("Opublikowany post można edytować, ale nie można dla niego ustawić terminu publikacji.");

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

        if (Status != PostStatus.Published)
        {
            ScheduledAt = scheduledAt;
            Status = scheduledAt is null ? PostStatus.Draft : PostStatus.Scheduled;
            ErrorMessage = null; // edycja posta po błędzie, odrzuceniu lub w oczekiwaniu na akceptację przywraca Draft/Scheduled
            RejectionComment = null;
        }

        TargetAccount = targetAccount;
        TargetAccountId = targetAccount?.Id;

        Files.Clear(); // osierocone pliki są usuwane przez EF (relacja wymagana)
        Files.AddRange(pics);
        Files.AddRange(vids);

        ManagerUsers.Clear();
        ManagerUsers.AddRange(managers.DistinctBy(m => m.Id));
    }
}
