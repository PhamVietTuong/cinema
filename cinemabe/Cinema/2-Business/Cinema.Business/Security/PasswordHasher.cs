using System.Security.Cryptography;
using System.Text;

namespace Cinema.Business.Security;

/// <summary>
/// Salted PBKDF2 (SHA-256) secret hashing shared by account passwords and manager-override PINs.
/// Also verifies the legacy single-round HMAC-SHA512 scheme (128-byte key salts) so old accounts still log in.
/// </summary>
public static class PasswordHasher
{
    private const int _saltSize = 16;
    private const int _keySize = 32;
    private const int _iterations = 100_000;
    private static readonly HashAlgorithmName _algorithm = HashAlgorithmName.SHA256;

    public static void CreateHash(string secret, out byte[] hash, out byte[] salt)
    {
        salt = RandomNumberGenerator.GetBytes(_saltSize);
        hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(secret), salt, _iterations, _algorithm, _keySize);
    }

    public static bool Verify(string secret, byte[] hash, byte[] salt)
    {
        if (IsLegacy(salt))
        {
            // Legacy scheme: single-round HMAC-SHA512 keyed by the stored salt.
            using var hmac = new HMACSHA512(salt);
            var legacy = hmac.ComputeHash(Encoding.UTF8.GetBytes(secret));
            return CryptographicOperations.FixedTimeEquals(legacy, hash);
        }

        var computed = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(secret), salt, _iterations, _algorithm, _keySize);
        return CryptographicOperations.FixedTimeEquals(computed, hash);
    }

    // New PBKDF2 salts are exactly _saltSize bytes; the old HMAC-SHA512 key salts are 128 bytes.
    public static bool IsLegacy(byte[] salt)
    {
        return salt.Length != _saltSize;
    }
}
