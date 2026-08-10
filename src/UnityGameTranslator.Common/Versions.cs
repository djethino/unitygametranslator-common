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
    /// · each dot-separated part is compared as a NUMBER, so 0.9.10 comes after 0.9.9;
    /// · a missing part counts as zero, so 1.2 and 1.2.0 are the same version;
    /// · anything after a dash in a part is dropped for the numeric comparison;
    /// · at equal numbers, a version carrying a suffix (1.2.0-beta.1) ranks BELOW the plain one.
    /// </summary>
    public static class Versions
    {
        /// <summary>Negative when a is older, zero when equal, positive when a is newer.</summary>
        public static int Compare(string? a, string? b)
        {
            string left = (a ?? string.Empty).Trim().TrimStart('v', 'V');
            string right = (b ?? string.Empty).Trim().TrimStart('v', 'V');

            if (left.Length == 0 && right.Length == 0) return 0;
            if (left.Length == 0) return -1;
            if (right.Length == 0) return 1;

            string[] leftParts = left.Split('.');
            string[] rightParts = right.Split('.');
            int count = Math.Max(leftParts.Length, rightParts.Length);

            for (int i = 0; i < count; i++)
            {
                int leftNumber = NumberAt(leftParts, i);
                int rightNumber = NumberAt(rightParts, i);

                if (leftNumber != rightNumber) return leftNumber.CompareTo(rightNumber);
            }

            bool leftPrerelease = left.IndexOf('-') >= 0;
            bool rightPrerelease = right.IndexOf('-') >= 0;

            if (leftPrerelease && !rightPrerelease) return -1;
            if (!leftPrerelease && rightPrerelease) return 1;

            return 0;
        }

        /// <summary>True when <paramref name="candidate"/> is strictly newer than <paramref name="current"/>.</summary>
        public static bool IsNewer(string? current, string? candidate) => Compare(current, candidate) < 0;

        private static int NumberAt(string[] parts, int index)
        {
            if (index >= parts.Length) return 0;

            // "3-beta.1" -> "3". A part that is not a number at all counts as zero rather than
            // throwing: a tag we do not recognise must never stop a program from starting.
            string head = parts[index].Split('-')[0];
            int value;
            return int.TryParse(head, out value) ? value : 0;
        }
    }
}
