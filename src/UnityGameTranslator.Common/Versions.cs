using System;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// Comparing two version strings, the same way on both sides.
    ///
    /// The mod and the installer read the same tags from the same publisher and have to reach the
    /// same verdict. One of them deciding that 0.9.66 comes before 0.9.9 while the other says the
    /// opposite shows up as "an update it keeps offering and never applies", with nothing on screen
    /// to say which of the two is wrong.
    ///
    /// This existed twice — once in the mod's update checker, once in the installer, the second
    /// written from the first and marked as a mirror. Two copies of one rule is exactly what this
    /// library is for.
    ///
    /// The rules, spelled out because they are not a string comparison:
    /// · the version is the part before the first dash; each dot-separated field is compared as a
    ///   NUMBER, so 0.9.10 comes after 0.9.9;
    /// · a missing field counts as zero, so 1.2 and 1.2.0 are the same version;
    /// · at equal numbers, a version carrying a suffix (1.2.0-beta.1) ranks BELOW the plain one,
    ///   and two suffixes are compared field by field — beta.2 after beta.1, beta after alpha,
    ///   and a numeric field before a word;
    /// · build metadata (+something) is ignored, as the standard asks.
    ///
    /// ⚠ The dash has to be cut out BEFORE splitting on dots, and that is the whole reason this
    /// method is not three lines. Both earlier copies split on dots first, so "0.11.0-beta.1"
    /// became four fields — 0, 11, 0, 1 — and outranked "0.11.0" on the strength of that trailing
    /// 1. The suffix rule below never even ran, because it only fires once the numeric fields tie.
    /// A plain "-beta" behaved correctly and a "-beta.1" did the opposite, which is why nobody saw
    /// it: the release process uses the dotted form. What it cost, concretely, is that whoever
    /// installed a beta was never offered the final release that replaced it — their program kept
    /// telling them they were up to date.
    /// </summary>
    public static class Versions
    {
        /// <summary>Negative when a is older, zero when equal, positive when a is newer.</summary>
        public static int Compare(string? a, string? b)
        {
            string left = Clean(a);
            string right = Clean(b);

            if (left.Length == 0 && right.Length == 0) return 0;
            if (left.Length == 0) return -1;
            if (right.Length == 0) return 1;

            Split(left, out string leftNumbers, out string leftSuffix);
            Split(right, out string rightNumbers, out string rightSuffix);

            int numbers = CompareNumbers(leftNumbers, rightNumbers);
            if (numbers != 0) return numbers;

            return CompareSuffixes(leftSuffix, rightSuffix);
        }

        /// <summary>True when <paramref name="candidate"/> is strictly newer than <paramref name="current"/>.</summary>
        public static bool IsNewer(string? current, string? candidate) => Compare(current, candidate) < 0;

        /// <summary>Surrounding blanks, a leading v, and build metadata are all noise here.</summary>
        private static string Clean(string? version)
        {
            string cleaned = (version ?? string.Empty).Trim().TrimStart('v', 'V');
            int metadata = cleaned.IndexOf('+');
            return metadata >= 0 ? cleaned.Substring(0, metadata) : cleaned;
        }

        private static void Split(string version, out string numbers, out string suffix)
        {
            int dash = version.IndexOf('-');
            if (dash < 0)
            {
                numbers = version;
                suffix = string.Empty;
                return;
            }

            numbers = version.Substring(0, dash);
            suffix = version.Substring(dash + 1);
        }

        private static int CompareNumbers(string left, string right)
        {
            string[] leftFields = left.Split('.');
            string[] rightFields = right.Split('.');
            int count = Math.Max(leftFields.Length, rightFields.Length);

            for (int i = 0; i < count; i++)
            {
                int leftNumber = NumberAt(leftFields, i);
                int rightNumber = NumberAt(rightFields, i);

                if (leftNumber != rightNumber) return leftNumber < rightNumber ? -1 : 1;
            }

            return 0;
        }

        /// <summary>An absent suffix wins; otherwise the two are read field by field.</summary>
        private static int CompareSuffixes(string left, string right)
        {
            if (left.Length == 0 && right.Length == 0) return 0;
            if (left.Length == 0) return 1;
            if (right.Length == 0) return -1;

            string[] leftFields = left.Split('.');
            string[] rightFields = right.Split('.');
            int count = Math.Min(leftFields.Length, rightFields.Length);

            for (int i = 0; i < count; i++)
            {
                bool leftIsNumber = IsNumber(leftFields[i], out int leftNumber);
                bool rightIsNumber = IsNumber(rightFields[i], out int rightNumber);

                if (leftIsNumber && rightIsNumber)
                {
                    if (leftNumber != rightNumber) return leftNumber < rightNumber ? -1 : 1;
                    continue;
                }

                // A number is worth less than a word, so beta.1 comes before beta.final.
                if (leftIsNumber != rightIsNumber) return leftIsNumber ? -1 : 1;

                int words = string.CompareOrdinal(leftFields[i], rightFields[i]);
                if (words != 0) return words < 0 ? -1 : 1;
            }

            // Everything shared matched, so the one that says more is further along: beta.1 after beta.
            if (leftFields.Length != rightFields.Length)
                return leftFields.Length < rightFields.Length ? -1 : 1;

            return 0;
        }

        private static int NumberAt(string[] fields, int index)
        {
            if (index >= fields.Length) return 0;

            // A field that is not a number at all counts as zero rather than throwing: a tag we do
            // not recognise must never stop a program from starting.
            return IsNumber(fields[index], out int value) ? value : 0;
        }

        private static bool IsNumber(string field, out int value)
        {
            value = 0;
            if (field.Length == 0) return false;

            for (int i = 0; i < field.Length; i++)
            {
                if (field[i] < '0' || field[i] > '9') return false;
            }

            // Long enough to overflow is not a number we can rank; treating it as a word is safer
            // than wrapping around into a small one.
            return int.TryParse(field, out value);
        }
    }
}
