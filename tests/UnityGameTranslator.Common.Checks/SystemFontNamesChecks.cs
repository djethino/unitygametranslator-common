using System;
using System.Linq;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// How a translation's font name finds a font installed on the computer — the rule the mod uses
    /// to show it and the Manager to export it. Two products disagreeing here export another font.
    /// </summary>
    internal static class SystemFontNamesChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            var styled = SystemFontNames.Candidates("Adobe Devanagari Italic");
            check(styled[0] == "Adobe Devanagari Italic"
                  && styled.Contains("AdobeDevanagari-Italic") && styled.Contains("AdobeDevanagariItalic"),
                "a display name with a style tries the file names fonts are usually saved under",
                "\"Adobe Devanagari Italic\" is AdobeDevanagari-Italic.otf on disk");

            var plain = SystemFontNames.Candidates("comicbd");
            check(plain[0] == "comicbd" && plain.Contains("comicbd-Regular"),
                "a name that already is a file name is tried as it is, first",
                "most translations name Windows fonts by their file, as the picker lists them");

            check(SystemFontNames.Normalize("Adobe Devanagari Italic") == SystemFontNames.Normalize("AdobeDevanagari-Italic")
                  && SystemFontNames.Normalize("DejaVu_Sans") == "dejavusans" && SystemFontNames.Normalize(null) == "",
                "names compare without case, spaces, hyphens or underscores",
                "the loose match the mod falls back on when no exact file name is there");

            check(SystemFontNames.RegisteredName("Candara Bold (TrueType)") == "Candara Bold"
                  && SystemFontNames.RegisteredName("Segoe UI Variable (TrueType)") == "Segoe UI Variable"
                  && SystemFontNames.RegisteredName("Candara") == "Candara",
                "a name from the system's font table loses the format it is filed under",
                "Windows files \"Candara Bold\" as \"Candara Bold (TrueType)\"");

            check(!AssetPacks.ShareNotice.Contains("copy", StringComparison.OrdinalIgnoreCase)
                  && AssetPacks.ShareNotice.Contains("licences") && AssetPacks.ShareNotice.Contains("not responsible"),
                "sharing fonts and images says the licences are the sharer's to check",
                "the user's condition for offering to export system fonts (2026-09-27)");
        }
    }
}
