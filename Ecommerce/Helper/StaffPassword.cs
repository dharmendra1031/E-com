using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Text;

namespace EcommerceWebApplication.Helper;

public static class StaffPassword
{
    public const string Prefix = "staff-v1:";
    private static readonly PasswordHasher<string> Hasher = new();

    public static bool IsValidNew(string? password) =>
        password is { Length: >= 12 and <= 128 } && !string.IsNullOrWhiteSpace(password);

    public static string Hash(string password) => Prefix + Hasher.HashPassword("staff", password);

    public static bool Verify(string? stored, string? password, bool allowLegacy, out bool upgrade)
    {
        upgrade = false;
        if (string.IsNullOrEmpty(stored) || string.IsNullOrEmpty(password) || password.Length > 1024)
            return false;
        if (stored.StartsWith(Prefix, StringComparison.Ordinal))
        {
            try
            {
                var result = Hasher.VerifyHashedPassword("staff", stored[Prefix.Length..], password);
                upgrade = result == PasswordVerificationResult.SuccessRehashNeeded;
                return result != PasswordVerificationResult.Failed;
            }
            catch (FormatException) { return false; }
        }
        if (!allowLegacy) return false;
        upgrade = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(password));
        return upgrade;
    }
}
