using System.Security.Cryptography;

namespace Cinema.Data.Services;

/// <summary>
/// A short fingerprint of a user's current credentials, embedded in the JWT as the <see cref="ClaimType"/> claim.
/// The API recomputes it from the database on every authenticated request, so changing or resetting the password
/// (which rewrites hash + salt) immediately invalidates every token issued before it.
/// </summary>
public static class TokenStamp
{
    public const string ClaimType = "stamp";

    public static string Compute(byte[] passwordHash, byte[] passwordSalt)
    {
        var buffer = new byte[passwordSalt.Length + passwordHash.Length];
        Buffer.BlockCopy(passwordSalt, 0, buffer, 0, passwordSalt.Length);
        Buffer.BlockCopy(passwordHash, 0, buffer, passwordSalt.Length, passwordHash.Length);
        return Convert.ToHexString(SHA256.HashData(buffer))[..32];
    }
}
