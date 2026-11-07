namespace SocialContent.Api.Dtos;

/// <summary>Firma (CompanyUser) widziana przez powiązanego z nią klienta. Bez danych logowania.</summary>
public record CompanyDto(Guid CompanyId, string Name);
