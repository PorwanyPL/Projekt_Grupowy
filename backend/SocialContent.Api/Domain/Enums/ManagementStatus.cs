namespace SocialContent.Api.Domain.Enums;

/// <summary>Stan zaproszenia firmy do zarządzania kontem społecznościowym klienta.</summary>
public enum ManagementStatus
{
    /// <summary>Klient zaprosił firmę; czeka na jej decyzję.</summary>
    Invited,

    /// <summary>Firma przyjęła zaproszenie i zarządza kontem.</summary>
    Accepted,

    /// <summary>Firma odrzuciła zaproszenie; klient może zaprosić ponownie (tę samą lub inną firmę).</summary>
    Declined
}
