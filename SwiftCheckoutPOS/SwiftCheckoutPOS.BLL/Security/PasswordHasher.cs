using System;
using System.Linq;
using System.Security.Cryptography;

namespace SwiftCheckoutPOS.BLL.Security;

/// <summary>
/// Provides a versioned PBKDF2 password format while retaining verification support
/// for the original unversioned hashes already stored in deployed databases.
/// </summary>
public static class PasswordHasher
{
    private const int LegacyIterations = 100_000;
    private const int CurrentIterations = 210_000;
    private const int SaltLength = 32;
    private const int KeyLength = 32;
    private const string CurrentPrefix = "v2$";

    public static string GenerateSalt() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(SaltLength));

    public static string Hash(string password, string salt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var saltBytes = Convert.FromBase64String(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, CurrentIterations, HashAlgorithmName.SHA256, KeyLength);
        return $"{CurrentPrefix}{CurrentIterations}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string salt, string storedHash, out bool needsRehash)
    {
        needsRehash = false;

        try
        {
            var saltBytes = Convert.FromBase64String(salt);
            var iterations = LegacyIterations;
            var encodedHash = storedHash;

            if (storedHash.StartsWith(CurrentPrefix, StringComparison.Ordinal))
            {
                var parts = storedHash.Split('$', StringSplitOptions.None);
                if (parts.Length != 3 || !int.TryParse(parts[1], out iterations) || iterations < LegacyIterations)
                {
                    return false;
                }

                encodedHash = parts[2];
            }

            var expectedHash = Convert.FromBase64String(encodedHash);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
            var isValid = CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            needsRehash = isValid && iterations < CurrentIterations;
            return isValid;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static void EnsureMeetsPolicy(string password)
    {
        if (password.Length is < 12 or > 128)
        {
            throw new ArgumentException("Passwords must be between 12 and 128 characters long.");
        }

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            throw new ArgumentException("Passwords must include at least one letter and one number.");
        }
    }
}
