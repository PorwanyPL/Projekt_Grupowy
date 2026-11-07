using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Dtos;

/// <param name="StorageKey">Klucz pliku; treść pobiera się z GET /api/Media/{storageKey}.</param>
public record PictureDto(Guid Id, string FileName, Format Format, int SizeKb, string StorageKey);
