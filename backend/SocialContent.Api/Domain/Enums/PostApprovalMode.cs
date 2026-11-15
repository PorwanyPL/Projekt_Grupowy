namespace SocialContent.Api.Domain.Enums;

/// <summary>Kto akceptuje posty przed publikacją na kontach danego klienta (ustawienie klienta dla firmy).</summary>
public enum PostApprovalMode
{
    /// <summary>Klient powierza akceptację menedżerowi/firmie: posty publikują się bez zgody klienta.</summary>
    Manager,

    /// <summary>Klient sam akceptuje każdy post przed publikacją.</summary>
    Client
}
