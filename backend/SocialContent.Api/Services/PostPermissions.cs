using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Services;

/// <summary>
/// Reguły dostępu do postów w jednym miejscu (używają ich PostService i serwis blokad).
/// Wymaga załadowanych: Post.ManagerUsers oraz Post.TargetAccount z ManagerUsers.
/// </summary>
internal static class PostPermissions
{
    /// <summary>Podgląd: firma widzi posty swoich kont, manager - posty, których jest autorem (także po odebraniu praw do konta).</summary>
    public static bool CanView(CurrentUser user, Post post) =>
        user.IsCompany
            ? post.TargetAccount is not null && post.TargetAccount.CompanyId == user.Id
            : post.ManagerUsers.Any(m => m.Id == user.Id);

    /// <summary>
    /// Zmiana i usuwanie: firma - każdy post swoich kont; manager - tylko własny post na koncie, do którego jest
    /// (nadal) przypisany. Manager, któremu odebrano prawa do konta, może tylko przeglądać swoje dawne posty.
    /// </summary>
    public static bool CanModify(CurrentUser user, Post post) =>
        user.IsCompany
            ? post.TargetAccount is not null && post.TargetAccount.CompanyId == user.Id
            : user.IsManager
              && post.ManagerUsers.Any(m => m.Id == user.Id)
              && post.TargetAccount is not null
              && post.TargetAccount.ManagerUsers.Any(m => m.Id == user.Id);

    /// <summary>Domena nie pozwala edytować posta w trakcie publikacji ani opublikowanego.</summary>
    public static bool IsEditableStatus(Post post) => post.Status is not (PostStatus.Publishing or PostStatus.Published);
}
