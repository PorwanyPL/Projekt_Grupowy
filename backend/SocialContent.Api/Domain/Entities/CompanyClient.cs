using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Domain.Entities;

/// <summary>
/// Relacja firma - klient (CompanyUser n - m ClientUser) jako osobna encja, bo niesie dane relacji:
/// pseudonim klienta nadany przez firmę oraz wybór klienta, kto akceptuje posty przed publikacją.
/// </summary>
public class CompanyClient
{
    public Guid CompanyId { get; private set; }
    public CompanyUser CompanyUser { get; private set; } = null!;

    public Guid ClientId { get; private set; }
    public ClientUser ClientUser { get; private set; } = null!;

    /// <summary>Nazwa lub pseudonim klienta widoczny w widokach firmy i jej managerów; null = imię i nazwisko.</summary>
    public string? Alias { get; private set; }

    public PostApprovalMode ApprovalMode { get; private set; } = PostApprovalMode.Manager;

    public DateTime CreatedAt { get; private set; }

    private CompanyClient() { }

    public static CompanyClient Create(Guid companyId, Guid clientId) =>
        new() { CompanyId = companyId, ClientId = clientId, CreatedAt = DateTime.UtcNow };

    public void SetAlias(string? alias) => Alias = Validate.OptionalText(alias, "Pseudonim", 100);

    public void SetApprovalMode(PostApprovalMode mode) => ApprovalMode = mode;
}
