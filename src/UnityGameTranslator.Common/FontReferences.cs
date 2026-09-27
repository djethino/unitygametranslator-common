using System;

namespace UnityGameTranslator.Common
{
    /// <summary>Where a replacement font comes from.</summary>
    public enum FontSource
    {
        /// <summary>A font of the game itself.</summary>
        Game,

        /// <summary>A font file in the mod's fonts/ folder — dropped there, or brought by an asset pack.</summary>
        Custom,

        /// <summary>A font installed on this computer.</summary>
        System,
    }

    /// <summary>
    /// Which font a translation's font reference means — the ORIGIN it names, and the order it is
    /// looked for in.
    ///
    /// 🔴 **The origin chosen is the origin used** (user, 2026-09-27). The Fonts tab writes
    /// "[Game] Arial", "[Custom] Arial" or a bare "Arial" (installed on the computer), and the mod
    /// used to strip that mark before looking: a font in fonts/ answered first for all three, so
    /// "[Game] Arial" could be served a file somebody dropped in fonts/. Now:
    ///
    /// <code>
    /// [Game] X    the game's X, and nothing else
    /// [Custom] X  fonts/X, and nothing else
    /// X           the installed X; failing that a copy of it in fonts/ (an asset pack carries
    ///             installed fonts under their own name); failing that a game font of that name
    ///             (translations written before the marks existed)
    /// </code>
    ///
    /// ⚠ The one leniency, the second line of a bare name, is what lets an exported installed font
    /// reach a player who does not have it — and on the author's machine changes nothing: the
    /// installed font still answers first.
    /// </summary>
    public static class FontReferences
    {
        private static readonly FontSource[] GameOnly = { FontSource.Game };
        private static readonly FontSource[] CustomOnly = { FontSource.Custom };
        private static readonly FontSource[] Installed = { FontSource.System, FontSource.Custom, FontSource.Game };

        /// <summary>The sources to try for a reference, in order.</summary>
        public static FontSource[] Order(string? reference)
        {
            if (reference != null && reference.StartsWith(AssetPacks.GameFontPrefix, StringComparison.Ordinal)) return GameOnly;
            if (reference != null && reference.StartsWith(AssetPacks.CustomFontPrefix, StringComparison.Ordinal)) return CustomOnly;
            return Installed;
        }

        /// <summary>The font's name, without the origin mark.</summary>
        public static string Name(string? reference)
        {
            if (string.IsNullOrEmpty(reference)) return reference ?? "";
            if (reference!.StartsWith(AssetPacks.GameFontPrefix, StringComparison.Ordinal)) return reference.Substring(AssetPacks.GameFontPrefix.Length);
            if (reference.StartsWith(AssetPacks.CustomFontPrefix, StringComparison.Ordinal)) return reference.Substring(AssetPacks.CustomFontPrefix.Length);
            return reference;
        }

        /// <summary>
        /// Whether text of this kind can be drawn with a font FILE — a font in fonts/, brought by hand
        /// or by an asset pack. The kind is `_fonts[…].type` as the mod writes it.
        ///
        /// 🔴 **Only TextMeshPro can.** Legacy text (UI.Text, written "Unity") is drawn by the engine
        /// from a font the game holds or one INSTALLED on the computer — the operating system finds
        /// the file, and a file lying in fonts/ is not installed. So an installed font a pack carries
        /// helps TextMeshPro text only; for legacy text it would arrive and never be used.
        /// </summary>
        public static bool ReadsFontFiles(string? textType) =>
            textType == "TMP" || textType == "TextMeshPro" || textType == "TMP (alt)";

        /// <summary>
        /// Which source serves a reference, given what exists here — null when none can.
        /// What the Fonts tab shows, so it says the font the game actually uses.
        /// </summary>
        public static FontSource? Serving(string? reference, bool gameHas, bool customHas, bool systemHas)
        {
            foreach (var source in Order(reference))
            {
                if (source == FontSource.Game && gameHas) return source;
                if (source == FontSource.Custom && customHas) return source;
                if (source == FontSource.System && systemHas) return source;
            }

            return null;
        }
    }
}
