using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// The ways a Unity game can put text on screen, as UGT Mod handles them — one road each in the
    /// mod, so which of them a game uses is which of the mod's roads it exercises.
    /// </summary>
    public enum TextSystem
    {
        /// <summary>TextMesh Pro (TMPro).</summary>
        Tmp,
        /// <summary>The TextMesh Pro that shipped as an asset before Unity took it in (TMProOld).</summary>
        TmpLegacy,
        /// <summary>uGUI's UnityEngine.UI.Text.</summary>
        UiText,
        /// <summary>The legacy 3D TextMesh.</summary>
        TextMesh,
        /// <summary>2D Toolkit's tk2dTextMesh.</summary>
        Tk2d,
        /// <summary>NGUI's UILabel.</summary>
        Ngui,
        /// <summary>UI Toolkit (UnityEngine.UIElements).</summary>
        UiToolkit,
        /// <summary>A TextMesh Pro input field (TMP_InputField).</summary>
        InputTmp,
        /// <summary>A uGUI input field (UnityEngine.UI.InputField).</summary>
        InputUiText,
    }

    /// <summary>
    /// 🔴 **Two different questions, and they must never be written as one.**
    ///
    /// | | who knows | how sure |
    /// |---|---|---|
    /// | what a game CONTAINS | the Manager, from the game's files, before anything runs | a library is shipped whole: TextMesh Pro always carries its input field, used or not |
    /// | what a game SHOWS | UGT Mod, from the texts it actually met while the game ran | exact, but only for what has been on screen with the mod |
    ///
    /// Measured on 2026-09-25: the script list of a game (globalgamemanagers.assets) names every
    /// class of every shipped assembly, TMP_Dropdown and TMP_SpriteAnimator included. So a file
    /// search says "contains" and nothing more; only the mod can say "shows" — which is why the mod
    /// writes <see cref="FileName"/> and the Manager reads it.
    ///
    /// The file (spec/texts-seen): <c>{ "format": 1, "systems": ["tmp", "input-tmp"],
    /// "other": ["SuperTextMesh"], "mod_version": "0.13.7" }</c> — a set that only grows while the
    /// game runs, rewritten when it does.
    /// </summary>
    public static class TextSystems
    {
        /// <summary>The file UGT Mod writes in its data folder, beside config.json.</summary>
        public const string FileName = "texts-seen.json";

        /// <summary>The format this socle writes and reads.</summary>
        public const int Format = 1;

        // The written words, in the order a list is shown: text first, input fields after.
        private static readonly (TextSystem System, string Word, string Label)[] Table =
        {
            (TextSystem.Tmp, "tmp", "TextMesh Pro"),
            (TextSystem.TmpLegacy, "tmp-legacy", "TextMesh Pro (legacy)"),
            (TextSystem.UiText, "ui-text", "UI Text"),
            (TextSystem.TextMesh, "textmesh", "TextMesh"),
            (TextSystem.Tk2d, "tk2d", "2D Toolkit"),
            (TextSystem.Ngui, "ngui", "NGUI"),
            (TextSystem.UiToolkit, "ui-toolkit", "UI Toolkit"),
            (TextSystem.InputTmp, "input-tmp", "TextMesh Pro"),
            (TextSystem.InputUiText, "input-ui-text", "UI Text"),
        };

        /// <summary>The word written in the file for a system.</summary>
        public static string Word(TextSystem system)
        {
            foreach (var row in Table)
                if (row.System == system) return row.Word;
            throw new ArgumentOutOfRangeException(nameof(system));
        }

        /// <summary>A written word back to its system. False for a word this socle does not know —
        /// a newer mod's, which the reader keeps as it is rather than dropping.</summary>
        public static bool TryParse(string word, out TextSystem system)
        {
            foreach (var row in Table)
            {
                if (string.Equals(row.Word, word, StringComparison.Ordinal))
                {
                    system = row.System;
                    return true;
                }
            }
            system = default;
            return false;
        }

        /// <summary>What a person reads for a system — the product's name, nothing more.</summary>
        public static string Label(TextSystem system)
        {
            foreach (var row in Table)
                if (row.System == system) return row.Label;
            throw new ArgumentOutOfRangeException(nameof(system));
        }

        /// <summary>An input field rather than a text: listed apart, after the texts.</summary>
        public static bool IsInputField(TextSystem system) =>
            system == TextSystem.InputTmp || system == TextSystem.InputUiText;

        /// <summary>
        /// A generic text component the mod found by its type name — the systems it knows by a
        /// name rather than by a type it links against. Null when the name is none of them: that
        /// one is written under "other", by its name.
        /// </summary>
        public static TextSystem? FromTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            if (typeName == "UILabel") return TextSystem.Ngui;
            if (typeName == "tk2dTextMesh") return TextSystem.Tk2d;
            return null;
        }

        /// <summary>
        /// One line for a set of systems: the texts, then the input fields —
        /// "TextMesh Pro, UI Text · input fields: TextMesh Pro". Words this socle does not know
        /// (<paramref name="unknown"/>) and other components (<paramref name="other"/>) are named
        /// as they are, after the texts. Empty for an empty set.
        /// </summary>
        public static string Describe(IEnumerable<TextSystem> systems, IEnumerable<string> other = null,
                                      IEnumerable<string> unknown = null)
        {
            var set = new HashSet<TextSystem>(systems ?? Enumerable.Empty<TextSystem>());

            var texts = Table.Where(r => set.Contains(r.System) && !IsInputField(r.System)).Select(r => r.Label).ToList();
            texts.AddRange((other ?? Enumerable.Empty<string>()).Where(s => !string.IsNullOrEmpty(s)).Distinct());
            texts.AddRange((unknown ?? Enumerable.Empty<string>()).Where(s => !string.IsNullOrEmpty(s)).Distinct());

            var inputs = Table.Where(r => set.Contains(r.System) && IsInputField(r.System)).Select(r => r.Label).ToList();

            var line = string.Join(", ", texts);
            if (inputs.Count == 0) return line;
            var fields = "input fields: " + string.Join(", ", inputs);
            return line.Length == 0 ? fields : line + " · " + fields;
        }
    }
}
