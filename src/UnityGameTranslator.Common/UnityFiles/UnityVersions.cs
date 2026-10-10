using System;

namespace UnityGameTranslator.Common.UnityFiles
{
    /// <summary>
    /// A Unity version as a number that sorts like the versions do: major, minor and patch on 16 bits
    /// each, then the release kind (a, b, c, f, p, x) and its number — the encoding of the type tree
    /// dumps <see cref="FontLayouts"/> is generated from, so the two compare directly.
    /// </summary>
    public static class UnityVersions
    {
        private const string Kinds = "abcfpx";

        /// <summary>
        /// The key of a version as Unity writes it ("2021.3.27f1", "6000.0.84f1", "5.6.5p2"); 0 when the
        /// text is not one (empty, "0.0.0" in a bundle built without it).
        /// </summary>
        public static ulong Key(string? text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var parts = text!.Trim().Split('.');
            if (parts.Length < 3) return 0;
            if (!ushort.TryParse(parts[0], out var major) || !ushort.TryParse(parts[1], out var minor)) return 0;

            // "27f1" (or "27f1c1" on Unity's China builds): the patch, the kind letter, its number.
            string tail = parts[2];
            int i = 0;
            while (i < tail.Length && char.IsDigit(tail[i])) i++;
            if (i == 0 || !ushort.TryParse(tail.Substring(0, i), out var patch)) return 0;
            ulong kind = 3, number = 0;   // no letter: a final release
            if (i < tail.Length)
            {
                int k = Kinds.IndexOf(tail[i]);
                if (k < 0) return 0;
                kind = (ulong)k;
                int j = i + 1;
                while (j < tail.Length && char.IsDigit(tail[j])) j++;
                if (j > i + 1 && byte.TryParse(tail.Substring(i + 1, j - i - 1), out var n)) number = n;
            }
            if (major == 0 && minor == 0 && patch == 0) return 0;
            return ((ulong)major << 48) | ((ulong)minor << 32) | ((ulong)patch << 16) | (kind << 8) | number;
        }

        /// <summary>The text of a key: "2021.3.27f1".</summary>
        public static string Text(ulong key)
        {
            ulong kind = (key >> 8) & 0xFF;
            char letter = kind < (ulong)Kinds.Length ? Kinds[(int)kind] : '?';
            return $"{(key >> 48) & 0xFFFF}.{(key >> 32) & 0xFFFF}.{(key >> 16) & 0xFFFF}{letter}{key & 0xFF}";
        }

        /// <summary>The year-or-major number of a key (2018, 6000…).</summary>
        public static int Major(ulong key) => (int)((key >> 48) & 0xFFFF);
        public static int Minor(ulong key) => (int)((key >> 32) & 0xFFFF);
    }
}
