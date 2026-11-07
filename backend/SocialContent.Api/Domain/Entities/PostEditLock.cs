using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Domain.Entities;

/// <summary>
/// Blokada edycji posta: w danej chwili post może edytować tylko jedna osoba. Blokada wygasa sama
/// (ExpiresAt), więc porzucona sesja edycji nie blokuje posta na zawsze; aktywny edytor ją odnawia.
/// </summary>
public class PostEditLock
{
    public Guid PostId { get; private set; }
    public Guid HolderId { get; private set; }
    public UserRole HolderRole { get; private set; }

    /// <summary>Imię i nazwisko albo nazwa firmy w chwili przejęcia blokady (pokazywane drugiej osobie).</summary>
    public string HolderName { get; private set; } = null!;
    public DateTime AcquiredAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private PostEditLock() { }

    public static PostEditLock Create(Guid postId, Guid holderId, UserRole role, string holderName, DateTime now, TimeSpan ttl) =>
        new()
        {
            PostId = postId, HolderId = holderId, HolderRole = role, HolderName = holderName,
            AcquiredAt = now, ExpiresAt = now + ttl
        };

    public bool IsExpired(DateTime now) => ExpiresAt <= now;

    public bool IsHeldBy(Guid userId, UserRole role) => HolderId == userId && HolderRole == role;

    /// <summary>Odnowienie przez obecnego właściciela.</summary>
    public void Renew(DateTime now, TimeSpan ttl) => ExpiresAt = now + ttl;

    /// <summary>Przejęcie wygasłej blokady przez inną osobę.</summary>
    public void TakeOver(Guid holderId, UserRole role, string holderName, DateTime now, TimeSpan ttl)
    {
        HolderId = holderId;
        HolderRole = role;
        HolderName = holderName;
        AcquiredAt = now;
        ExpiresAt = now + ttl;
    }
}
