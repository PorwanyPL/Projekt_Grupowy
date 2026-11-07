namespace SocialContent.Api.Domain.Enums;

/// <summary>Rola użytkownika zapisywana w tokenie JWT (claim "role"); wartości odpowiadają nazwom encji z diagramu.</summary>
public enum UserRole { CompanyUser, ManagerUser, ClientUser }
