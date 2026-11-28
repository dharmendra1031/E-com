using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Text;

namespace EcommerceWebApplication.Helper;

public static class CustomerPassword
{
    public const string Prefix = "customer-v1:";
    public const string VersionClaim = "CustomerCredentialVersion";
    private static readonly PasswordHasher<string> Hasher = new();

    public static bool IsValidNew(string? password) =>
        password is { Length: >= 8 and <= 50 } && !string.IsNullOrWhiteSpace(password);

    public static string Hash(string password) => Prefix + Hasher.HashPassword("customer", password);

    public static bool Verify(string? stored, string? password, out bool upgrade)
    {
        upgrade = false;
        if (string.IsNullOrEmpty(stored) || string.IsNullOrEmpty(password) || password.Length > 1024 || stored == "0")
            return false;
        if (stored.StartsWith(Prefix, StringComparison.Ordinal))
        {
            try
            {
                var result = Hasher.VerifyHashedPassword("customer", stored[Prefix.Length..], password);
                upgrade = result == PasswordVerificationResult.SuccessRehashNeeded;
                return result != PasswordVerificationResult.Failed;
            }
            catch (FormatException) { return false; }
        }
        // Existing customer plaintext values are upgraded only after successful authentication.
        if (stored.StartsWith(StaffPassword.Prefix, StringComparison.Ordinal)) return false;
        upgrade = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(password));
        return upgrade;
    }

    public static string ApiOtpKey(string email, bool reset) =>
        "api:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            (reset ? "reset:" : "register:") + email.Trim().ToUpperInvariant())));
}
