using System.Security.Cryptography;
using GoDental.Application.Abstractions.Security;

namespace GoDental.Infrastructure.Security;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const int Iterations = 600_000;
    public const string DummyHash = "PBKDF2-SHA256$600000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        try { return $"PBKDF2-SHA256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}"; }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
    public bool Verify(string password, string encodedHash)
    {
        try
        {
            var parts = encodedHash.Split('$');
            if (parts.Length != 4 || parts[0] != "PBKDF2-SHA256" || !int.TryParse(parts[1], out var rounds) || rounds is < 100_000 or > 2_000_000) return false;
            var salt = Convert.FromBase64String(parts[2]); var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length != 16 || expected.Length != 32) return false;
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, rounds, HashAlgorithmName.SHA256, 32);
            try { return CryptographicOperations.FixedTimeEquals(actual, expected); }
            finally { CryptographicOperations.ZeroMemory(actual); }
        }
        catch (FormatException) { return false; }
    }
}
