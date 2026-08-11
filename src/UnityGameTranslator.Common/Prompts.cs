using System;
using System.Linq;
using System.Text;

namespace UnityGameTranslator.Common
{
    /// <summary>What a piece of text looks like, which decides how it is asked for.</summary>
    public enum TextType
    {
        /// <summary>One word: no space in a space-writing script, four characters or fewer otherwise.</summary>
        SingleWord,

        /// <summary>A short phrase or sentence.</summary>
        Phrase,

        /// <summary>Several lines.</summary>
        Paragraph,
    }

    /// <summary>
    /// The instructions sent to a model, and the classification that shapes them.
    ///
    /// ⚠ Written for the mod, which is what actually translates games — and reproduced in the
    /// manager so it could score models against those instructions. Reproducing them is what makes
    /// a bench measure itself: what is scored has to be what a game sends, word for word.
    ///
    /// Both prompts live here, the one for a game's text and the one for the mod's own interface,
    /// even though only the first is scored today. They are the same kind of thing, and a rule
    /// left behind in one program is a rule that drifts the day the other one needs it.
    ///
    /// ⚠ Nothing here reads a configuration. Everything the prompt depends on arrives as an
    /// argument, because the mod's configuration object cannot leave the mod — it carries
    /// Newtonsoft-encrypted properties — and because a builder that reaches for globals cannot be
    /// asked "what would you send for this?".
    /// </summary>
    public static class Prompts
    {
        /// <summary>
        /// What a model is told to answer when the text is not in the source language at all.
        ///
        /// Deliberately not a word: it must never collide with something a game could legitimately
        /// contain, and it must survive being echoed back verbatim.
        /// </summary>
        public const string SkipMarker = "AxNoTranslateXa";

        /// <summary>
        /// Sort a text before asking for it.
        ///
        /// ⚠ Counting spaces only works for scripts that use them. Chinese, Japanese, Korean, Thai,
        /// Lao, Khmer, Burmese and Tibetan write without them, so a whole sentence has none — and
        /// judging it by spaces would call it a single word every time. Those scripts are measured
        /// in characters instead.
        ///
        /// ⚠ What this changes in practice is ONE sentence: whether the prompt ends with "Now,
        /// translate this word:". Worth knowing before treating this classification as more
        /// load-bearing than it is.
        ///
        /// 🔸 Paragraph is returned but never distinguished from Phrase by the builders below —
        /// inherited granularity nothing reads yet.
        /// 🔸 A single character in one of those ranges is enough to switch the rule, so a mostly
        /// Latin string containing one ideogram is measured in characters.
        /// </summary>
        public static TextType Classify(string text)
        {
            if (string.IsNullOrEmpty(text)) return TextType.SingleWord;

            if (text.IndexOf('\n') >= 0) return TextType.Paragraph;

            bool scriptioContinua = text.Any(c =>
                (c >= 0x4E00 && c <= 0x9FFF) ||   // Chinese (CJK Unified Ideographs)
                (c >= 0x3040 && c <= 0x30FF) ||   // Japanese Hiragana/Katakana
                (c >= 0xAC00 && c <= 0xD7AF) ||   // Korean Hangul
                (c >= 0x0E00 && c <= 0x0E7F) ||   // Thai
                (c >= 0x0E80 && c <= 0x0EFF) ||   // Lao
                (c >= 0x1780 && c <= 0x17FF) ||   // Khmer
                (c >= 0x1000 && c <= 0x109F) ||   // Burmese
                (c >= 0x0F00 && c <= 0x0FFF));    // Tibetan

            if (scriptioContinua)
                return text.Length <= 4 ? TextType.SingleWord : TextType.Phrase;

            return text.IndexOf(' ') < 0 ? TextType.SingleWord : TextType.Phrase;
        }

        /// <summary>
        /// Which placeholders a given text actually contains.
        ///
        /// ⚠ Presence in THIS text, never "the game has variables somewhere". Announcing a
        /// placeholder the text does not contain invites the model to invent one — small models
        /// answered "[!STR*0]" on its own, or glued it to an otherwise correct translation.
        /// </summary>
        public struct Markers
        {
            public bool LineBreaks;
            public bool Tags;
            public bool Numbers;
            public bool Variables;
        }

        /// <summary>
        /// The instructions for a game's own text.
        /// </summary>
        /// <param name="targetLanguage">Where it is going. Always known.</param>
        /// <param name="sourceLanguage">Where it comes from, or null when nobody said.</param>
        /// <param name="gameContext">What kind of game, in the author's words. Empty falls back.</param>
        /// <param name="strictSourceLanguage">
        /// Whether a text that is NOT in the source language should come back as
        /// <see cref="SkipMarker"/> instead of being translated anyway. Only possible when the
        /// source language is known.
        /// </param>
        public static string ForGameText(
            string targetLanguage,
            string? sourceLanguage,
            string? gameContext,
            bool strictSourceLanguage,
            TextType textType,
            Markers markers)
        {
            var prompt = new StringBuilder();

            string context = string.IsNullOrEmpty(gameContext)
                ? "video game UI, menus and dialogues"
                : gameContext!;

            if (strictSourceLanguage && sourceLanguage != null)
            {
                prompt.AppendLine("=== CRITICAL RULE ===");
                prompt.AppendLine($"Source language: {sourceLanguage}");
                prompt.AppendLine($"- If text is NOT in {sourceLanguage}: reply ONLY with exactly: {SkipMarker}");
                prompt.AppendLine($"- If text IS in {sourceLanguage}: translate to {targetLanguage}");
                prompt.AppendLine();
            }

            prompt.AppendLine("=== CONTEXT ===");
            prompt.AppendLine(sourceLanguage != null
                ? $"Translating video game ({context}) from {sourceLanguage} to {targetLanguage}."
                : $"Translating video game ({context}) to {targetLanguage}.");
            prompt.AppendLine();

            prompt.AppendLine("=== TRANSLATION RULES ===");
            prompt.AppendLine("- Output the translation only, no explanation");
            prompt.AppendLine("- Translation must be correct in target language");
            prompt.AppendLine("- Keep it concise for UI");
            prompt.AppendLine("- Do not add punctuation if not in the source to translate");
            prompt.AppendLine("- Keep unchanged: keyboard keys (Tab, Esc, Space...), technical settings (VSync, Auto)");

            AppendMarkerRules(prompt, markers);
            AppendSingleWordClosing(prompt, textType);

            return prompt.ToString();
        }

        /// <summary>
        /// The instructions for the mod's own interface.
        ///
        /// ⚠ A different job, hence different rules: the terms to leave alone are this tool's own
        /// vocabulary rather than a game's settings, and there is no CRITICAL RULE because the
        /// source is always English — the interface is written in it.
        /// </summary>
        public static string ForOwnInterface(string targetLanguage, TextType textType, Markers markers)
        {
            var prompt = new StringBuilder();

            prompt.AppendLine("=== CONTEXT ===");
            prompt.AppendLine($"Translating a game translation tool interface from English to {targetLanguage}.");
            prompt.AppendLine("Technical UI with terms: AI, cache, merge, sync, upload, download, API, hotkey, config, JSON.");
            prompt.AppendLine();

            prompt.AppendLine("=== TRANSLATION RULES ===");
            prompt.AppendLine("- Output the translation only, no explanation");
            prompt.AppendLine("- Translation must be understandable and correct in target language");
            prompt.AppendLine("- Keep it concise for UI");
            prompt.AppendLine("- Do not add punctuation if not in the source to translate");
            prompt.AppendLine("- Keep technical terms unchanged: API, URL, UUID, JSON, AI");
            prompt.AppendLine("- Keep keyboard shortcuts as-is: Ctrl, Alt, Shift, F1-F12, Tab, Esc");

            AppendMarkerRules(prompt, markers);
            AppendSingleWordClosing(prompt, textType);

            return prompt.ToString();
        }

        private static void AppendMarkerRules(StringBuilder prompt, Markers markers)
        {
            if (markers.LineBreaks)
                prompt.AppendLine("- IMPORTANT: Keep [!nl] placeholders exactly where they are, do not remove or move them");
            if (markers.Tags)
                prompt.AppendLine("- IMPORTANT: Keep [!t*0], [!t*1], etc. tag placeholders exactly as-is, do not modify or remove them");
            if (markers.Numbers)
                prompt.AppendLine("- IMPORTANT: Keep [!v*0], [!v*1], etc. placeholders exactly as-is, do not modify them");
            if (markers.Variables)
                prompt.AppendLine("- IMPORTANT: Keep [!STR*0], [!STR*1], etc. string variable placeholders exactly as-is, do not translate or modify them");
        }

        private static void AppendSingleWordClosing(StringBuilder prompt, TextType textType)
        {
            if (textType != TextType.SingleWord) return;

            prompt.AppendLine();
            prompt.Append("Now, translate this word:");
        }
    }
}
