using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Services;

/// <summary>
/// Reguły dostępu do postów w jednym miejscu (używają ich PostService i serwis blokad).
/// Wymaga załadowanych: Post.ManagerUsers oraz Post.TargetAccount z ManagerUsers.
/// </summary>
internal static class PostPermissions
{
    /// <summary>
    /// Podgląd: firma widzi posty swoich kont, manager - posty, których jest autorem (także po odebraniu praw do konta),
    /// klient - wszystkie posty swoich kont.
    /// </summary>
    public static bool CanView(CurrentUser user, Post post) =>
        user.Role switch
        {
            UserRole.CompanyUser => post.TargetAccount is not null && post.TargetAccount.CompanyId == user.Id,
            UserRole.ClientUser => post.TargetAccount is not null && post.TargetAccount.OwnerClientId == user.Id,
            _ => post.ManagerUsers.Any(m => m.Id == user.Id)
        };

    /// <summary>
    /// Zmiana i usuwanie: firma - każdy post swoich (przyjętych) kont; manager - tylko własny post na koncie, do którego jest
    /// (nadal) przypisany. Manager, któremu odebrano prawa do konta, może tylko przeglądać swoje dawne posty.
    /// Klient nigdy nie edytuje postów (może je tylko akceptować lub odrzucać).
    /// </summary>
    public static bool CanModify(CurrentUser user, Post post) =>
        user.Role switch
        {
            UserRole.CompanyUser => post.TargetAccount is not null && post.TargetAccount.CompanyId == user.Id,
            UserRole.ManagerUser => post.ManagerUsers.Any(m => m.Id == user.Id)
                                    && post.TargetAccount is not null
                                    && post.TargetAccount.ManagerUsers.Any(m => m.Id == user.Id),
            _ => false
        };

    /// <summary>Opublikowany post można edytować; nie można tylko tego, który jest właśnie publikowany.</summary>
    public static bool IsEditableStatus(Post post) => post.Status != PostStatus.Publishing;

    /// <summary>Klient akceptuje lub odrzuca post swojego konta, który czeka na jego decyzję.</summary>
    public static bool CanDecide(CurrentUser user, Post post) =>
        user.IsClient && post.Status == PostStatus.AwaitingApproval && CanView(user, post);
}
