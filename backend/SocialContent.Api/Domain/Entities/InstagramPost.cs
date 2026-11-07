using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Domain.ValueObjects;

namespace SocialContent.Api.Domain.Entities;

public sealed class InstagramPost : Post
{
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
        var tags = Validate.NormalizeTags(userTags);
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
        var tags = Validate.NormalizeTags(userTags);
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
}
