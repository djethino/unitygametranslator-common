using System;
using System.Collections.Generic;
using System.Text;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// How a font name used by a translation ("Candara", "comicbd", "Adobe Devanagari Italic") is
    /// matched to a font file installed on the computer — the rules, not the folders.
    ///
    /// 🔴 **One rule for the mod that USES the font and the Manager that EXPORTS it.** The mod finds a
    /// system font by these names when a translation asks for it; the Manager's export, asked to carry
    /// the system fonts a translation uses, must pick the very same file — or the pack would carry a
    /// font the game never showed. Moved here from the mod's font loader (2026-09-27), unchanged.
    ///
    /// ⚠ The folders to look in and the system's own name table stay with each product: they depend
    /// on the machine, and the socle knows no machine.
    /// </summary>
    public static class SystemFontNames
    {
        private static readonly string[] StyleSuffixes =
        {
            "Bold Italic", "Bold", "Italic", "Regular", "Light", "Medium",
            "SemiBold", "ExtraBold", "Thin", "Black", "Condensed",
        };

        /// <summary>
        /// The file names (without extension) worth trying for a display name, most exact first.
        /// "Adobe Devanagari Italic" → itself, "AdobeDevanagari-Italic", "AdobeDevanagariItalic", …
        /// </summary>
        public static string[] Candidates(string displayName)
        {
            var candidates = new List<string> { displayName };

            // Split into name + style: "Adobe Devanagari Bold Italic" → "Adobe Devanagari", "Bold Italic".
            var family = displayName;
            var style = "";
            foreach (var suffix in StyleSuffixes)
            {
                if (displayName.EndsWith(" " + suffix, StringComparison.OrdinalIgnoreCase))
                {
                    family = displayName.Substring(0, displayName.Length - suffix.Length - 1);
                    style = suffix;
                    break;
                }
            }

            var familyNoSpaces = family.Replace(" ", "");
            var styleNoSpaces = style.Replace(" ", "");

            if (style.Length > 0)
            {
                candidates.Add(familyNoSpaces + "-" + styleNoSpaces);
                candidates.Add(familyNoSpaces + style);
                candidates.Add(familyNoSpaces + "-" + style);
            }

            candidates.Add(familyNoSpaces + "-Regular");
            candidates.Add(familyNoSpaces);
            candidates.Add(displayName.Replace(" ", ""));

            return candidates.ToArray();
        }

        /// <summary>
        /// A font name made comparable: lower case, no spaces, hyphens or underscores.
        /// "Adobe Devanagari Italic" and "AdobeDevanagari-Italic" both become "adobedevanagariitalic".
        /// </summary>
        public static string Normalize(string? name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            var sb = new StringBuilder(name!.Length);
            foreach (var c in name)
            {
                if (c != ' ' && c != '-' && c != '_') sb.Append(char.ToLowerInvariant(c));
            }

            return sb.ToString();
        }

        /// <summary>
        /// The name a system font table gives a file, without the format Windows appends:
        /// "Candara Bold (TrueType)" → "Candara Bold". Several names joined by " &amp; " (a collection)
        /// come back as they are — such a file is a .ttc, which no product here loads.
        /// </summary>
        public static string RegisteredName(string valueName)
        {
            var name = valueName.Trim();
            var open = name.LastIndexOf(" (", StringComparison.Ordinal);
            if (open > 0 && name.EndsWith(")", StringComparison.Ordinal)) name = name.Substring(0, open);
            return name;
        }
    }
}
