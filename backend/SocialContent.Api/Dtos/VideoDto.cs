using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Dtos;

/// <param name="Length">Czas trwania w sekundach; null, gdy nieznany.</param>
/// <param name="StorageKey">Klucz pliku; treść pobiera się z GET /api/Media/{storageKey}.</param>
public record VideoDto(Guid Id, string FileName, Format Format, int SizeKb, int? Length, string StorageKey);
