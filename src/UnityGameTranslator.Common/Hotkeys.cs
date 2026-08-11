using System;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// How a keyboard shortcut is written down: "Ctrl+Alt+F10".
    ///
    /// One string, written by three different places and read by one. The mod's capture widget
    /// builds it, the manager's settings screen builds it, and the mod's input loop takes it apart
    /// again to decide whether to open a panel.
    ///
    /// ⚠ Getting this wrong is SILENT. A shortcut the input loop cannot take apart does not raise
    /// anything and does not log anything — the panel simply never opens, and someone who also
    /// skipped the first-run wizard is left with a mod they cannot open and no screen on which to
    /// fix it.
    ///
    /// ⚠ It used to be written three times, and the copies did not agree on CASE. The input loop
    /// tested `Contains("Ctrl+")` and stripped with a case-sensitive Replace, while the manager
    /// stripped case-insensitively — so "ctrl+F10" typed into the manager was reported as valid,
    /// written into a game, and never fired. Everything here is case-insensitive, which is the
    /// permissive direction: every shortcut that worked before still works.
    ///
    /// What is NOT here: which key names are acceptable. The mod's answer is UnityEngine.KeyCode,
    /// an enum this library cannot see and must not second-guess; the manager keeps a deliberately
    /// narrower list of keys it is willing to write. Those are two different questions from "how is
    /// it spelled", and merging them would have the manager refuse shortcuts the mod handles.
    /// </summary>
    public static class Hotkeys
    {
        /// <summary>What the mod falls back to when nothing is configured.</summary>
        public const string Default = "Ctrl+F10";

        private const string Ctrl = "Ctrl+";
        private const string Alt = "Alt+";
        private const string Shift = "Shift+";

        /// <summary>
        /// Assemble a shortcut. Modifiers come first and always in this order — Ctrl, Alt, Shift —
        /// so that one shortcut has one spelling and two configs can be compared as strings.
        /// </summary>
        public static string Compose(string? key, bool ctrl, bool alt, bool shift)
        {
            if (key == null || key.Length == 0) return string.Empty;

            return (ctrl ? Ctrl : "")
                 + (alt ? Alt : "")
                 + (shift ? Shift : "")
                 + key.Trim();
        }

        /// <summary>
        /// Take a shortcut apart.
        ///
        /// Returns false for nothing at all — which is a legitimate setting, meaning the shortcut
        /// is disabled, and not an error to report.
        /// </summary>
        public static bool TryParse(string? hotkey, out string key, out bool ctrl, out bool alt, out bool shift)
        {
            key = string.Empty;
            ctrl = alt = shift = false;

            if (hotkey == null) return false;

            string rest = hotkey.Trim();
            if (rest.Length == 0) return false;

            // Repeated rather than one pass: the three modifiers are conventionally written in one
            // order, but a config edited by hand can carry them in any, and refusing to read
            // "Alt+Ctrl+F10" would be refusing something the mod has always accepted.
            bool found = true;
            while (found)
            {
                found = false;
                if (StripPrefix(ref rest, Ctrl)) { ctrl = true; found = true; }
                if (StripPrefix(ref rest, Alt)) { alt = true; found = true; }
                if (StripPrefix(ref rest, Shift)) { shift = true; found = true; }
            }

            key = rest.Trim();
            return key.Length > 0;
        }

        /// <summary>The key on its own, with the modifiers taken off. Empty when there is none.</summary>
        public static string BaseKeyOf(string? hotkey)
        {
            TryParse(hotkey, out string key, out _, out _, out _);
            return key;
        }

        private static bool StripPrefix(ref string text, string prefix)
        {
            if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

            text = text.Substring(prefix.Length).TrimStart();
            return true;
        }
    }
}
