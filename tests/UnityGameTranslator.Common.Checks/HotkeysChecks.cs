using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// How a shortcut is spelled, and the case bug that made this worth sharing.
    ///
    /// The failure mode is what justifies every case below: a shortcut the mod cannot take apart
    /// raises nothing and logs nothing. The panel just never opens. There is no error to search
    /// for, so the only defence is that all three writers and the one reader agree here.
    /// </summary>
    internal static class HotkeysChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            // Round-trip: what one screen writes, the input loop reads back.
            check(Compose("F10", ctrl: true) == "Ctrl+F10", "Ctrl+F10 composes", "the default, and the ordinary case");
            check(Compose("F10", ctrl: true, alt: true, shift: true) == "Ctrl+Alt+Shift+F10",
                "modifiers keep one order", "so one shortcut has one spelling and configs compare as strings");

            check(Parsed("Ctrl+F10") == "F10:C--", "Ctrl+F10 parses back", "round-trip");
            check(Parsed("Ctrl+Alt+Shift+F10") == "F10:CAS", "and so does the full set", "round-trip");
            check(Parsed("F10") == "F10:---", "a bare key needs no modifier", "plenty of people bind just F10");

            // ⚠ The bug this file exists for. The mod tested Contains("Ctrl+") case-sensitively
            // while the manager stripped case-insensitively, so this spelling was reported valid,
            // written into a game, and never fired.
            check(Parsed("ctrl+f10") == "f10:C--", "lower case is understood",
                "it used to validate on one side and never fire on the other");
            check(Parsed("CTRL+ALT+F10") == "F10:CA-", "upper case too", "same reason");

            // A config edited by hand does not follow our conventions.
            check(Parsed("Alt+Ctrl+F10") == "F10:CA-", "modifiers in any order",
                "refusing this would refuse something that always worked");
            check(Parsed("  Ctrl+F10  ") == "F10:C--", "surrounding blanks are ignored", "hand-edited files have them");

            // Nothing is a setting, not a fault: an empty shortcut means "disabled".
            check(!Hotkeys.TryParse("", out _, out _, out _, out _)
                  && !Hotkeys.TryParse(null, out _, out _, out _, out _),
                "nothing parses to nothing", "an empty shortcut means disabled, and that is allowed");
            check(!Hotkeys.TryParse("Ctrl+", out _, out _, out _, out _),
                "and a modifier with no key is not a shortcut", "there would be nothing to press");
            check(Hotkeys.Compose("", true, false, false) == "",
                "composing nothing gives nothing", "never a dangling 'Ctrl+' written into a config");

            check(Hotkeys.BaseKeyOf("Ctrl+Alt+F10") == "F10", "the key alone can be asked for",
                "callers check it against what their own runtime can bind");
            check(Hotkeys.Default == "Ctrl+F10", "the default is stated once", "three places used to carry it");
        }

        private static string Compose(string key, bool ctrl = false, bool alt = false, bool shift = false) =>
            Hotkeys.Compose(key, ctrl, alt, shift);

        /// <summary>"F10:CA-" — the key, then which modifiers were recognised. Compact on purpose.</summary>
        private static string Parsed(string hotkey)
        {
            if (!Hotkeys.TryParse(hotkey, out string key, out bool ctrl, out bool alt, out bool shift))
                return "(none)";

            return $"{key}:{(ctrl ? 'C' : '-')}{(alt ? 'A' : '-')}{(shift ? 'S' : '-')}";
        }
    }
}
