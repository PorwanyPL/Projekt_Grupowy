namespace SocialContent.Api.Domain;

public sealed class FacebookPost : Post
{
    public ExternalId TargetPageId { get; private set; } = null!;
    public WebUrl? LinkUrl { get; private set; }
    public string? PrivacySetting { get; private set; }
    public string? FeedTargeting { get; private set; }
    public FacebookPostType PostType { get; private set; }

    public override SocialPlatform? Platform => SocialPlatform.Facebook;

    private FacebookPost() { }

    /// <param name="postType">Gdy null, przyjmowany jest ten sam typ co Visibility.</param>
    public static FacebookPost Create(Title title, Description description, FacebookPostType type,
        IEnumerable<Picture> pictures, IEnumerable<Video> videos, IEnumerable<ManagerUser> managers,
        ScheduledTime? scheduledAt, SocialAccount? targetAccount,
        ExternalId targetPageId, WebUrl? linkUrl, string? privacySetting, string? feedTargeting,
        FacebookPostType? postType)
    {
        var post = new FacebookPost();
        post.InitNew();
        post.Apply(title, description, type, pictures, videos, managers, scheduledAt, targetAccount);
        post.SetDetails(targetPageId, linkUrl, privacySetting, feedTargeting, postType ?? type);
        return post;
    }

    public void Update(Title title, Description description, FacebookPostType type,
        IEnumerable<Picture> pictures, IEnumerable<Video> videos, IEnumerable<ManagerUser> managers,
        ScheduledTime? scheduledAt, SocialAccount? targetAccount,
        ExternalId targetPageId, WebUrl? linkUrl, string? privacySetting, string? feedTargeting,
        FacebookPostType? postType)
    {
        var privacy = Rules.OptionalText(privacySetting, "PrivacySetting", 100);
        var targeting = Rules.OptionalText(feedTargeting, "FeedTargeting", 500);
        Apply(title, description, type, pictures, videos, managers, scheduledAt, targetAccount);
        TargetPageId = targetPageId;
        LinkUrl = linkUrl;
        PrivacySetting = privacy;
        FeedTargeting = targeting;
        PostType = postType ?? type;
    }

    private void SetDetails(ExternalId targetPageId, WebUrl? linkUrl, string? privacySetting,
        string? feedTargeting, FacebookPostType postType)
    {
        TargetPageId = targetPageId;
        LinkUrl = linkUrl;
        PrivacySetting = Rules.OptionalText(privacySetting, "PrivacySetting", 100);
        FeedTargeting = Rules.OptionalText(feedTargeting, "FeedTargeting", 500);
        PostType = postType;
    }
}

public sealed class InstagramPost : Post
{
    private const int MaxUserTags = 30;

    public ExternalId InstagramAccountId { get; private set; } = null!;
    public InstagramMediaType MediaType { get; private set; }
    public List<string> UserTags { get; private set; } = new();
    public ExternalId? LocationId { get; private set; }
    public bool ShareToFeed { get; private set; }

    public override SocialPlatform? Platform => SocialPlatform.Instagram;

    private InstagramPost() { }

    public static InstagramPost Create(Title title, Description description, FacebookPostType type,
        IEnumerable<Picture> pictures, IEnumerable<Video> videos, IEnumerable<ManagerUser> managers,
        ScheduledTime? scheduledAt, SocialAccount? targetAccount,
        ExternalId instagramAccountId, InstagramMediaType mediaType, IEnumerable<string>? userTags,
        ExternalId? locationId, bool shareToFeed)
    {
        var tags = NormalizeTags(userTags);
        var post = new InstagramPost();
        post.InitNew();
        post.Apply(title, description, type, pictures, videos, managers, scheduledAt, targetAccount);
        post.SetDetails(instagramAccountId, mediaType, tags, locationId, shareToFeed);
        return post;
    }

    public void Update(Title title, Description description, FacebookPostType type,
        IEnumerable<Picture> pictures, IEnumerable<Video> videos, IEnumerable<ManagerUser> managers,
        ScheduledTime? scheduledAt, SocialAccount? targetAccount,
        ExternalId instagramAccountId, InstagramMediaType mediaType, IEnumerable<string>? userTags,
        ExternalId? locationId, bool shareToFeed)
    {
        var tags = NormalizeTags(userTags);
        Apply(title, description, type, pictures, videos, managers, scheduledAt, targetAccount);
        SetDetails(instagramAccountId, mediaType, tags, locationId, shareToFeed);
    }

    private void SetDetails(ExternalId instagramAccountId, InstagramMediaType mediaType, List<string> userTags,
        ExternalId? locationId, bool shareToFeed)
    {
        InstagramAccountId = instagramAccountId;
        MediaType = mediaType;
        UserTags = userTags;
        LocationId = locationId;
        ShareToFeed = shareToFeed;
    }

    private static List<string> NormalizeTags(IEnumerable<string>? tags)
    {
        var list = (tags ?? Array.Empty<string>())
            .Select(t => (t ?? "").Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (list.Count > MaxUserTags)
            throw new DomainValidationException($"Post na Instagramie może mieć maksymalnie {MaxUserTags} oznaczeń użytkowników.");
        if (list.Any(t => t.Length > 100))
            throw new DomainValidationException("Pojedyncze oznaczenie użytkownika może mieć maksymalnie 100 znaków.");
        return list;
    }
}

public sealed class TikTokPost : Post
{
    public ExternalId TikTokAccountId { get; private set; } = null!;
    public TikTokPrivacyLevel PrivacyLevel { get; private set; }
    public bool DisableDuet { get; private set; }
    public bool DisableStitch { get; private set; }
    public bool DisableComment { get; private set; }
    public bool BrandContentToggle { get; private set; }
    public bool AutoAddMusic { get; private set; }

    public override SocialPlatform? Platform => SocialPlatform.TikTok;

    private TikTokPost() { }

    public static TikTokPost Create(Title title, Description description, FacebookPostType type,
        IEnumerable<Picture> pictures, IEnumerable<Video> videos, IEnumerable<ManagerUser> managers,
        ScheduledTime? scheduledAt, SocialAccount? targetAccount,
        ExternalId tikTokAccountId, TikTokPrivacyLevel privacyLevel, bool disableDuet, bool disableStitch,
        bool disableComment, bool brandContentToggle, bool autoAddMusic)
    {
        var post = new TikTokPost();
        post.InitNew();
        post.Apply(title, description, type, pictures, videos, managers, scheduledAt, targetAccount);
        post.SetDetails(tikTokAccountId, privacyLevel, disableDuet, disableStitch, disableComment,
            brandContentToggle, autoAddMusic);
        return post;
    }

    public void Update(Title title, Description description, FacebookPostType type,
        IEnumerable<Picture> pictures, IEnumerable<Video> videos, IEnumerable<ManagerUser> managers,
        ScheduledTime? scheduledAt, SocialAccount? targetAccount,
        ExternalId tikTokAccountId, TikTokPrivacyLevel privacyLevel, bool disableDuet, bool disableStitch,
        bool disableComment, bool brandContentToggle, bool autoAddMusic)
    {
        Apply(title, description, type, pictures, videos, managers, scheduledAt, targetAccount);
        SetDetails(tikTokAccountId, privacyLevel, disableDuet, disableStitch, disableComment,
            brandContentToggle, autoAddMusic);
    }

    private void SetDetails(ExternalId tikTokAccountId, TikTokPrivacyLevel privacyLevel, bool disableDuet,
        bool disableStitch, bool disableComment, bool brandContentToggle, bool autoAddMusic)
    {
        TikTokAccountId = tikTokAccountId;
        PrivacyLevel = privacyLevel;
        DisableDuet = disableDuet;
        DisableStitch = disableStitch;
        DisableComment = disableComment;
        BrandContentToggle = brandContentToggle;
        AutoAddMusic = autoAddMusic;
    }
}
