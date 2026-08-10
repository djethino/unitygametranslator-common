using System;
using System.Security.Cryptography;
using System.Text;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// At-rest obfuscation of secrets, bound to the machine that wrote them.
    /// AES-256-CBC, key derived from machine identity through PBKDF2.
    ///
    /// ⚠ THE THREAT MODEL, which must travel with this code and must not be oversold:
    ///  · it DOES protect a config file copied off the machine — shared for support, picked up by
    ///    a cloud sync, committed by accident — because the key cannot be rebuilt elsewhere;
    ///  · it protects NOTHING against a local process running as the same user, which can rebuild
    ///    the key from the same public identity values and read everything.
    /// This is obfuscation bound to a machine, not a security boundary. The real defence for a
    /// token is revoking it server-side (DELETE /api/v1/auth/token); for a provider key, revoking
    /// it at the provider. Anyone who reads this file and concludes "the secrets are encrypted, so
    /// they are safe" has been misled, and that is why the paragraph comes before the code.
    ///
    /// ⚠ EVERY CONSTANT BELOW IS PART OF THE KEY. The "ENCRYPTED:" prefix, the 100,000 iterations,
    /// the "_UGT_v3" suffix, the salt source string, the order the identity values are appended in.
    /// Change one and this code no longer reads what the previous version wrote: the user is not
    /// told their secret is unreadable, they are told they are signed out. That is why this lives
    /// in one place now — it was written twice, and the second copy carried a comment saying so.
    ///
    /// Works on Windows, Linux and macOS, under Mono and IL2CPP.
    /// </summary>
    public static class Secrets
    {
        /// <summary>Marks a value this code wrote. Part of the format, not decoration.</summary>
        public const string Prefix = "ENCRYPTED:";

        /// <summary>Wraps a secret for storage. Null in, null out; empty in, empty out.</summary>
        public static string? Protect(string? plain)
        {
            if (string.IsNullOrEmpty(plain)) return plain;

            return Prefix + Encrypt(plain!);
        }

        /// <summary>
        /// Reads a stored secret back.
        ///
        /// A value we did not write is handed back untouched — that is how a secret saved by an
        /// older version, in the clear, keeps working until it is rewritten. Null means we could
        /// not decrypt something that claims to be ours, which happens legitimately when the file
        /// comes from another machine: callers should read it as "no secret", never as corruption.
        /// </summary>
        public static string? Unprotect(string? stored)
        {
            TryUnprotect(stored, out string? secret, out _);
            return secret;
        }

        /// <summary>
        /// Same as <see cref="Unprotect"/>, and says why when it fails.
        ///
        /// The reason exists because the two failures are not the same event: a key that no longer
        /// matches is a file that travelled, while unreadable base64 is a file that was damaged.
        /// A program that logs one sentence for both leaves whoever reads the log guessing.
        /// </summary>
        /// <returns>False only when the value was ours and could not be read.</returns>
        public static bool TryUnprotect(string? stored, out string? secret, out string? failure)
        {
            secret = stored;
            failure = null;

            if (string.IsNullOrEmpty(stored)) return true;
            if (!IsProtected(stored)) return true;

            try
            {
                secret = Decrypt(stored!.Substring(Prefix.Length));
                return true;
            }
            catch (CryptographicException error)
            {
                // The machine identity moved, or the value was tampered with.
                secret = null;
                failure = error.Message;
                return false;
            }
            catch (FormatException error)
            {
                // Not valid base64 at all.
                secret = null;
                failure = error.Message;
                return false;
            }
        }

        /// <summary>True when the value carries our marker, whatever it decrypts to.</summary>
        public static bool IsProtected(string? value) =>
            value != null && value.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>
        /// True when a stored value is a real secret sitting there unprotected.
        ///
        /// Covers a value written by a version that predates this code as well as anything else
        /// that arrived in the clear; the caller rewrites it on the next save.
        /// </summary>
        public static bool NeedsProtecting(string? stored) =>
            !string.IsNullOrEmpty(stored) && !IsProtected(stored);

        /// <summary>
        /// Encrypts a probe and reads it straight back.
        ///
        /// Worth the milliseconds before announcing that a secret was saved: on a machine where
        /// key derivation cannot run, writing an unreadable value and reporting success is the one
        /// outcome that costs the user their session without telling them anything.
        /// </summary>
        public static bool RoundTripWorks()
        {
            try
            {
                const string probe = "UGT round-trip probe";
                return Unprotect(Protect(probe)) == probe;
            }
            catch
            {
                return false;
            }
        }

        private static string Encrypt(string plainText)
        {
            byte[] key = DeriveKey(MachineSecret(), MachineSalt());

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.GenerateIV();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                {
                    byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                    byte[] encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                    // IV first, then the ciphertext. This layout is the format on disk.
                    byte[] result = new byte[aes.IV.Length + encrypted.Length];
                    Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
                    Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);

                    return Convert.ToBase64String(result);
                }
            }
        }

        private static string Decrypt(string encryptedText)
        {
            byte[] key = DeriveKey(MachineSecret(), MachineSalt());
            byte[] combined = Convert.FromBase64String(encryptedText);

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                byte[] iv = new byte[16];
                Buffer.BlockCopy(combined, 0, iv, 0, 16);
                aes.IV = iv;

                byte[] encrypted = new byte[combined.Length - 16];
                Buffer.BlockCopy(combined, 16, encrypted, 0, encrypted.Length);

                using (var decryptor = aes.CreateDecryptor())
                {
                    byte[] plainBytes = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
                    return Encoding.UTF8.GetString(plainBytes);
                }
            }
        }

        /// <summary>
        /// PBKDF2, 100,000 iterations, SHA-1 as the HMAC.
        ///
        /// ⚠ SHA-1 is not a choice, it is a constraint inherited from the runtime floor: on
        /// netstandard2.0 this overload of Rfc2898DeriveBytes takes no hash algorithm. SHA-256
        /// would be stronger AND would derive a different key, so switching it silently turns
        /// every stored secret unreadable. If it ever moves, it moves with a new suffix below and
        /// a path that rewrites what it finds.
        /// </summary>
        private static byte[] DeriveKey(string secret, byte[] salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(secret, salt, 100000))
            {
                return pbkdf2.GetBytes(32);
            }
        }

        /// <summary>
        /// The machine identity the key comes from.
        ///
        /// ⚠ Every value here has to be identical from one run to the next, which rules out
        /// String.GetHashCode() — it is randomised per process, and using it would produce a key
        /// that works until the program restarts.
        /// </summary>
        private static string MachineSecret()
        {
            var builder = new StringBuilder();
            builder.Append(Environment.MachineName);
            builder.Append("_");
            builder.Append(Environment.UserName);
            builder.Append("_");
            builder.Append(Environment.OSVersion.Platform);
            builder.Append("_UGT_v3");

            try
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(home))
                {
                    builder.Append("_");
                    builder.Append(home);
                }
            }
            catch
            {
                // A home path we cannot read drops out of the secret. Both programs have always
                // failed this way, and they must keep failing identically or they derive two keys.
            }

            return builder.ToString();
        }

        private static byte[] MachineSalt()
        {
            string source = Environment.MachineName + "_" + Environment.UserName + "_UGT_SALT";
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(source));
            }
        }
    }
}
