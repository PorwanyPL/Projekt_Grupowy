namespace SocialContent.Api.Domain.Enums;

/// <summary>
/// Status posta. AwaitingApproval i Rejected dotyczą klientów, którzy sami akceptują posty (PostApprovalMode.Client).
/// Nowe wartości są dopisane na końcu; w bazie status jest zapisany jako tekst.
/// </summary>
public enum PostStatus { Draft, Scheduled, Publishing, Published, Failed, AwaitingApproval, Rejected }
