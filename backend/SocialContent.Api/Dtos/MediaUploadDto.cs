using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Dtos;

/// <summary>Wynik przesłania pliku. StorageKey i FileName przekazuje się w pictures/videos przy tworzeniu posta.</summary>
public record MediaUploadDto(string StorageKey, string FileName, MediaKind Kind, Format Format, int SizeKb);
