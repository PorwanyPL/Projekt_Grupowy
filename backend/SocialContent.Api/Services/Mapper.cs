using SocialContent.Api.Domain;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Services;

public static class Mapper
{
    public static ManagerUserDto ToDto(this ManagerUser m) =>
        new(m.Id, m.FirstName.Value, m.LastName.Value, m.Age.Value, m.Description.Value);

    public static PostDto ToDto(this Post p) => new(
        p.Id,
        p.Title.Value,
        DateTime.SpecifyKind(p.CreatedAt.Date, DateTimeKind.Utc),
        p.Description.Value,
        p.Visibility,
        p.Pictures.Select(x => new PictureDto(x.Id, x.FileName.Value, x.Format, x.SizeKb)).ToList(),
        p.Videos.Select(x => new VideoDto(x.Id, x.FileName.Value, x.Format, x.Length)).ToList(),
        p.ManagerUsers.Select(x => x.ToDto()).ToList());
}
