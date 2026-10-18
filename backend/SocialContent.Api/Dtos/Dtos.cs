using System.ComponentModel.DataAnnotations;
using SocialContent.Api.Domain;

namespace SocialContent.Api.Dtos;

// ---------- ManagerUser ----------
public class ManagerUserWriteDto
{
    [Required, StringLength(50, MinimumLength = 1)] public string FirstName { get; set; } = "";
    [Required, StringLength(50, MinimumLength = 1)] public string LastName { get; set; } = "";
    [Range(18, 100)] public int Age { get; set; }
    [Required, StringLength(2000, MinimumLength = 1)] public string Description { get; set; } = "";
}

public record ManagerUserDto(Guid Id, string FirstName, string LastName, int Age, string Description);

// ---------- Media ----------
public class PictureWriteDto
{
    [Required, StringLength(150, MinimumLength = 1)] public string FileName { get; set; } = "";
    public Format Format { get; set; }
    [Range(1, int.MaxValue)] public int SizeKb { get; set; }
}

public class VideoWriteDto
{
    [Required, StringLength(150, MinimumLength = 1)] public string FileName { get; set; } = "";
    public Format Format { get; set; }
    [Range(1, int.MaxValue)] public int Length { get; set; }
}

public record PictureDto(Guid Id, string FileName, Format Format, int SizeKb);
public record VideoDto(Guid Id, string FileName, Format Format, int Length);

// ---------- Post ----------
public class PostWriteDto
{
    [Required, StringLength(120, MinimumLength = 1)] public string Title { get; set; } = "";
    [Required, StringLength(2000, MinimumLength = 1)] public string Description { get; set; } = "";
    public FacebookPostType Visibility { get; set; }
    public List<PictureWriteDto> Pictures { get; set; } = new();
    public List<VideoWriteDto> Videos { get; set; } = new();
    public List<Guid> ManagerUserIds { get; set; } = new();
}

public record PostDto(
    Guid Id,
    string Title,
    DateTime CreatedAt,
    string Description,
    FacebookPostType Visibility,
    List<PictureDto> Pictures,
    List<VideoDto> Videos,
    List<ManagerUserDto> ManagerUsers);
