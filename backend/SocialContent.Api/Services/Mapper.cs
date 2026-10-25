using SocialContent.Api.Domain;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Services;

public static class Mapper
{
    public static ManagerUserDto ToDto(this ManagerUser m) =>
        new(m.Id, m.FirstName.Value, m.LastName.Value, m.Age.Value, m.Description.Value, m.SocialAccountId);

    /// <summary>Wersja z informacją o koncie logowania - używana tylko przez ManagerUserService.</summary>
    public static ManagerUserDto ToDto(this ManagerUser m, bool hasLoginAccount, bool isAccessBlocked) =>
        new(m.Id, m.FirstName.Value, m.LastName.Value, m.Age.Value, m.Description.Value, m.SocialAccountId,
            hasLoginAccount, isAccessBlocked);

    public static ManagerProfileDto ToProfileDto(this ManagerUser m, string email) =>
        new(m.Id, email, m.FirstName.Value, m.LastName.Value, m.Age.Value, m.Description.Value, m.SocialAccountId);

    public static PictureDto ToDto(this Picture x) => new(x.Id, x.FileName.Value, x.Format, x.SizeKb);

    public static VideoDto ToDto(this Video x) => new(x.Id, x.FileName.Value, x.Format, x.Length);

    public static PostDto ToDto(this Post p)
    {
        FacebookPostDetailsDto? facebook = null;
        InstagramPostDetailsDto? instagram = null;
        TikTokPostDetailsDto? tikTok = null;

        switch (p)
        {
            case FacebookPost f:
                facebook = new FacebookPostDetailsDto
                {
                    TargetPageId = f.TargetPageId.Value,
                    LinkUrl = f.LinkUrl?.Url,
                    PrivacySetting = f.PrivacySetting,
                    FeedTargeting = f.FeedTargeting,
                    PostType = f.PostType
                };
                break;
            case InstagramPost i:
                instagram = new InstagramPostDetailsDto
                {
                    InstagramAccountId = i.InstagramAccountId.Value,
                    MediaType = i.MediaType,
                    UserTags = i.UserTags.ToList(),
                    LocationId = i.LocationId?.Value,
                    ShareToFeed = i.ShareToFeed
                };
                break;
            case TikTokPost t:
                tikTok = new TikTokPostDetailsDto
                {
                    TikTokAccountId = t.TikTokAccountId.Value,
                    PrivacyLevel = t.PrivacyLevel,
                    DisableDuet = t.DisableDuet,
                    DisableStitch = t.DisableStitch,
                    DisableComment = t.DisableComment,
                    BrandContentToggle = t.BrandContentToggle,
                    AutoAddMusic = t.AutoAddMusic
                };
                break;
        }

        return new PostDto(
            p.Id,
            p.Title.Value,
            DateTime.SpecifyKind(p.CreatedAt.Date, DateTimeKind.Utc),
            p.Description.Value,
            p.Visibility,
            p.Pictures.Select(x => x.ToDto()).ToList(),
            p.Videos.Select(x => x.ToDto()).ToList(),
            p.ManagerUsers.Select(x => x.ToDto()).ToList(),
            p.Status,
            p.Platform,
            AsUtc(p.ScheduledAt),
            AsUtc(p.PublishedAt),
            p.ExternalPostId?.Value,
            p.ErrorMessage,
            p.TargetAccountId,
            facebook,
            instagram,
            tikTok);
    }

    public static SocialAccountDto ToDto(this SocialAccount a) => new(
        a.Id,
        a.AccountId,
        a.Platform,
        a.ExternalPlatformId,
        a.AccountName,
        DateTime.SpecifyKind(a.TokenExpiresAt, DateTimeKind.Utc),
        a.IsActive,
        a.ErrorMessage,
        !string.IsNullOrEmpty(a.RefreshToken),
        a.Pictures.Select(x => x.ToDto()).ToList(),
        a.Videos.Select(x => x.ToDto()).ToList(),
        a.ManagerUsers.Select(x => x.ToDto()).ToList());

    private static DateTime? AsUtc(ScheduledTime? time) =>
        time is null ? null : DateTime.SpecifyKind(time.Date, DateTimeKind.Utc);
}
