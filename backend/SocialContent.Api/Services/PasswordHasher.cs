using System.Security.Cryptography;

namespace SocialContent.Api.Services;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>
/// PBKDF2-HMAC-SHA256 z losową solą (16 B) i 600 000 iteracji (zalecenie OWASP).
/// Format: "v1.iteracje.sól(base64).hasz(base64)" - parametry są w haszu, więc można je później podnieść.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const int Iterations = 600_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const string Version = "v1";

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Version}.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split('.');
        if (parts.Length != 4 || parts[0] != Version || !int.TryParse(parts[1], out var iterations) || iterations <= 0)
            return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
