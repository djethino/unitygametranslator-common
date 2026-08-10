using System;
using System.Security.Cryptography;
using System.Text;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// Checks the stored-secret format against the constants its own documentation names.
    ///
    /// ⚠ The derivation below is written FROM THE SPEC, deliberately not copied from Secrets.cs.
    /// A check that reuses the code it checks only proves the code equals itself; this one fails
    /// the moment the library stops matching what it says it does — a changed iteration count, a
    /// bumped suffix, a reordered identity value, a different IV layout.
    ///
    /// Why it is worth a whole file: a drift here is silent and expensive. Nothing crashes. The
    /// secret simply stops decrypting, the program reads that as "no secret", and the user is told
    /// they are signed out with no way to know their token is still sitting in the file. The mod
    /// (Mono, IL2CPP) and the installer (.NET 8) also have to derive the SAME key on one machine,
    /// so this pins a format two runtimes share.
    ///
    /// What it cannot check: that today's key matches a key derived on another machine. That is
    /// the whole point of the scheme, and it is why compatibility with an older build has to be
    /// proven by running both, which was done once at migration.
    /// </summary>
    internal static class SecretsChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            // Round-trip, the ordinary case.
            foreach (var secret in new[] { "ugt_abc123", "sk-proj-0123456789", "accents éàü", "x" })
            {
                string? stored = Secrets.Protect(secret);
                check(Secrets.Unprotect(stored) == secret, $"round-trip {secret}", "what we write, we read back");
            }

            // The format itself, since callers and files depend on it.
            check(Secrets.Protect("x")!.StartsWith(Secrets.Prefix, StringComparison.Ordinal),
                "a protected value carries the prefix", "that marker is how anything recognises ours");
            check(Secrets.Protect("x") != Secrets.Protect("x"),
                "two writes differ", "the IV is random, so identical secrets must not look identical");

            // A value nobody protected travels through untouched: that is how a secret written in
            // the clear by an older build keeps working until it is rewritten.
            check(Secrets.Unprotect("ugt_written_in_the_clear") == "ugt_written_in_the_clear",
                "a value we did not write comes back as it was", "older files must keep working");
            check(Secrets.NeedsProtecting("ugt_written_in_the_clear"),
                "and it is reported as needing protection", "so the next save rewrites it");
            check(!Secrets.NeedsProtecting(Secrets.Protect("x")),
                "an already-protected value does not", "or every save would re-wrap it");
            check(!Secrets.NeedsProtecting("") && !Secrets.NeedsProtecting(null),
                "and nothing is not a secret", "empty is not something to protect");

            // Failure has to be a value, never an exception: this runs while a game is starting.
            check(Secrets.Unprotect("ENCRYPTED:not base64 at all !!") == null,
                "damaged content reads as null", "unreadable is not a reason to crash a game");
            check(Secrets.Unprotect(Secrets.Prefix + Convert.ToBase64String(new byte[64])) == null,
                "a key that does not match reads as null", "a file from another machine is normal, not corruption");

            check(Secrets.TryUnprotect("ENCRYPTED:not base64 at all !!", out _, out string? why) == false && why != null,
                "and the failure says why", "a key that moved and a damaged file are not one event");

            check(Secrets.RoundTripWorks(), "the self-check passes", "callers gate on this before reporting success");

            // The oracle: decrypt what the library produced, using a key rebuilt from the spec.
            const string probe = "spec oracle probe";
            string ciphertext = Secrets.Protect(probe)!.Substring(Secrets.Prefix.Length);
            check(DecryptWithSpecKey(ciphertext) == probe,
                "the stored format matches its specification",
                "AES-256-CBC, PBKDF2-SHA1 100000 iterations, IV then ciphertext, salt and secret as documented");

            // And the reverse, so the library cannot merely be self-consistent.
            check(Secrets.Unprotect(Secrets.Prefix + EncryptWithSpecKey(probe)) == probe,
                "and it reads back what the specification produces", "the format is a contract, not a habit");
        }

        /// <summary>Key rebuilt from the documented constants alone.</summary>
        private static byte[] SpecKey()
        {
            string secret = Environment.MachineName
                            + "_" + Environment.UserName
                            + "_" + Environment.OSVersion.Platform
                            + "_UGT_v3";

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home)) secret += "_" + home;

            byte[] salt;
            using (var sha256 = SHA256.Create())
            {
                salt = sha256.ComputeHash(Encoding.UTF8.GetBytes(
                    Environment.MachineName + "_" + Environment.UserName + "_UGT_SALT"));
            }

#pragma warning disable SYSLIB0041 // SHA-1 is pinned by the netstandard2.0 floor, see Secrets.cs
            using var pbkdf2 = new Rfc2898DeriveBytes(secret, salt, 100000);
#pragma warning restore SYSLIB0041
            return pbkdf2.GetBytes(32);
        }

        private static string DecryptWithSpecKey(string base64)
        {
            byte[] combined = Convert.FromBase64String(base64);

            using var aes = Aes.Create();
            aes.Key = SpecKey();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.IV = combined[..16];

            using var decryptor = aes.CreateDecryptor();
            return Encoding.UTF8.GetString(decryptor.TransformFinalBlock(combined, 16, combined.Length - 16));
        }

        private static string EncryptWithSpecKey(string plain)
        {
            using var aes = Aes.Create();
            aes.Key = SpecKey();
            aes.GenerateIV();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            byte[] plainBytes = Encoding.UTF8.GetBytes(plain);
            byte[] encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            byte[] result = new byte[aes.IV.Length + encrypted.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);
            return Convert.ToBase64String(result);
        }
    }
}
