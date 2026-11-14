using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The white space of the library is Unicode's table, not the runtime's. On the runtime these checks run on (.NET 8) the
    /// two were MEASURED equal over the whole plane, so replacing Trim() and IsNullOrWhiteSpace by the table changes no
    /// answer here — this holds it for every character, which stands for the ~90 calls replaced.
    /// </summary>
    internal static class SpacesChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            int trimDiffers = 0, startDiffers = 0, endDiffers = 0, blankDiffers = 0;
            string firstDifferent = "";
            for (int cp = 0; cp <= 0xFFFF; cp++)
            {
                if (cp >= 0xD800 && cp <= 0xDFFF) continue;
                string c = ((char)cp).ToString();
                string both = c + "a" + c, only = c;
                if (Spaces.TrimSpaces(both) != both.Trim()) { trimDiffers++; firstDifferent = firstDifferent.Length == 0 ? cp.ToString("X4") : firstDifferent; }
                if (Spaces.TrimSpacesStart(both) != both.TrimStart()) startDiffers++;
                if (Spaces.TrimSpacesEnd(both) != both.TrimEnd()) endDiffers++;
                if (Spaces.IsBlank(only) != string.IsNullOrWhiteSpace(only)) blankDiffers++;
            }
            check(trimDiffers == 0 && startDiffers == 0 && endDiffers == 0, "trimming by the table gives what Trim gave, for every character of the plane",
                trimDiffers + " / " + startDiffers + " / " + endDiffers + " differ (first U+" + firstDifferent + ")");
            check(blankDiffers == 0, "a text of one character is blank exactly when IsNullOrWhiteSpace said so", blankDiffers + " differ");

            check(Spaces.IsBlank(null) && Spaces.IsBlank("") && Spaces.IsBlank(" \t\u00A0\u3000\u2028") && !Spaces.IsBlank(" a "), "null, empty and only-spaces are blank", "IsNullOrWhiteSpace's three");
            string clean = new string('x', 3);
            check(ReferenceEquals(Spaces.TrimSpaces(clean), clean) && ReferenceEquals(Spaces.TrimSpacesStart(clean), clean) && ReferenceEquals(Spaces.TrimSpacesEnd(clean), clean),
                "a text with nothing to take off comes back as the very same string", "this runs on every text of every frame");
            check(Spaces.TrimSpaces("\u200Bx\u200B") == "\u200Bx\u200B", "the zero-width space is not white space", "Unicode 18 says Cf, not White_Space");
        }
    }
}
