using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Dtos;

/// <summary>Firma (CompanyUser) widziana przez powiązanego z nią klienta. Bez danych logowania.</summary>
/// <param name="ApprovalMode">Wybór klienta dla tej firmy: kto akceptuje posty przed publikacją (Manager albo Client).</param>
public record CompanyDto(Guid CompanyId, string Name, PostApprovalMode ApprovalMode = PostApprovalMode.Manager);
