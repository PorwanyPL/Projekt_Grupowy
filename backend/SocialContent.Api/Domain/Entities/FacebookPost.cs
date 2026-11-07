using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.ValueObjects;

namespace SocialContent.Api.Domain.Entities;

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
        var privacy = Validate.OptionalText(privacySetting, "PrivacySetting", 100);
        var targeting = Validate.OptionalText(feedTargeting, "FeedTargeting", 500);
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
        PrivacySetting = Validate.OptionalText(privacySetting, "PrivacySetting", 100);
        FeedTargeting = Validate.OptionalText(feedTargeting, "FeedTargeting", 500);
        PostType = postType;
    }
}
