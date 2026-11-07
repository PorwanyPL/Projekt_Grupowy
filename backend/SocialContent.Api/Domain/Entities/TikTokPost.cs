using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.ValueObjects;

namespace SocialContent.Api.Domain.Entities;

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
