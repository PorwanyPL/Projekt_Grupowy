namespace SocialContent.Api.Domain.Entities;

/// <summary>
/// Dane logowania użytkownika ManagerUser (relacja 1:1). Trzymane osobno, aby encja ManagerUser
/// pozostała zgodna z diagramem, a hasło nigdy nie trafiało do DTO przez przypadek.
/// </summary>
public class ManagerCredential
{
    public Guid ManagerUserId { get; private set; }
    public ManagerUser ManagerUser { get; private set; } = null!;

    /// <summary>Identyfikator logowania: adres e-mail w postaci znormalizowanej (małe litery, bez spacji).</summary>
    public string Email { get; private set; } = null!;

    /// <summary>Hasz PBKDF2 w formacie "v1.iteracje.sól.hasz". Nigdy nie opuszcza warstwy serwisów.</summary>
    public string PasswordHash { get; private set; } = null!;

    /// <summary>
    /// Blokada dostępu: manager i jego powiązania z postami zostają, ale nie może się zalogować,
    /// a wydane wcześniej tokeny przestają być akceptowane.
    /// </summary>
    public bool IsAccessBlocked { get; private set; }

    private ManagerCredential() { }

    public void SetAccessBlocked(bool blocked) => IsAccessBlocked = blocked;

    /// <summary>Zmiana adresu logowania; wywołujący przekazuje e-mail już znormalizowany i zwalidowany (Validate).</summary>
    public void ChangeEmail(string normalizedEmail) => Email = normalizedEmail;

    /// <summary>Zmiana hasła; wywołujący przekazuje już hasz hasła zwalidowanego przez Validate.Password.</summary>
    public void ChangePasswordHash(string passwordHash) => PasswordHash = passwordHash;

    public static ManagerCredential Create(ManagerUser manager, string normalizedEmail, string passwordHash) =>
        new()
        {
            ManagerUserId = manager.Id,
            ManagerUser = manager,
            Email = normalizedEmail,
            PasswordHash = passwordHash
        };
}
