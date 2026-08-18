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
        /// <summary>The refusal a model is told to answer with. Read back by <see cref="Answers"/>.</summary>
        public const string SkipMarker = Answers.SkipMarker;

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
        /// <param name="gameName">
        /// The game's own name, or null when we do not have one worth sending.
        ///
        /// ⚠ **Only a name the game states about itself** — Unity's `productName`. Never a folder
        /// name: those are routinely `HyperEchelon6vYY3` or `Forsaken.Frontiers.v1510`, which teach
        /// a model nothing and invite it to invent something from the noise. The caller decides;
        /// this builder only writes what it is handed.
        /// </param>
        /// <param name="gameContext">What kind of game, in the author's words. Empty falls back.</param>
        /// <param name="strictSourceLanguage">
        /// Whether a text that is NOT in the source language should come back as
        /// <see cref="SkipMarker"/> instead of being translated anyway. Only possible when the
        /// source language is known.
        /// </param>
        public static string ForGameText(
            string targetLanguage,
            string? sourceLanguage,
            string? gameName,
            string? gameContext,
            bool strictSourceLanguage,
            TextType textType,
            Markers markers)
        {
            var prompt = new StringBuilder();

            string context = string.IsNullOrEmpty(gameContext)
                ? "video game UI, menus and dialogues"
                : gameContext!;

            // Named when we have one, so a model that knows the game can draw on its vocabulary.
            // Stated flatly, as a label — not "you know this game", which is what invites a small
            // model to invent a universe for one it has never heard of.
            string subject = string.IsNullOrEmpty(gameName)
                ? "video game"
                : $"the video game \"{gameName}\"";

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
                ? $"Translating {subject} ({context}) from {sourceLanguage} to {targetLanguage}."
                : $"Translating {subject} ({context}) to {targetLanguage}.");
            prompt.AppendLine();

            prompt.AppendLine("=== TRANSLATION RULES ===");
            prompt.AppendLine("- Output the translation only, no explanation");
            AppendCommonRules(prompt, targetLanguage);
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
            AppendCommonRules(prompt, targetLanguage);
            prompt.AppendLine("- Keep technical terms unchanged: API, URL, UUID, JSON, AI");
            prompt.AppendLine("- Keep keyboard shortcuts as-is: Ctrl, Alt, Shift, F1-F12, Tab, Esc");

            AppendMarkerRules(prompt, markers);
            AppendSingleWordClosing(prompt, textType);

            return prompt.ToString();
        }

        /// <summary>
        /// A second pass that asks a model to mark a translation out of ten — **the bench only**,
        /// never a game.
        ///
        /// The job is deliberately easier than translating: nothing to produce, two texts to
        /// compare in languages it already reads. That is what makes it worth asking of the small
        /// models this project runs on.
        ///
        /// 🔴 **What it is asked to judge is exactly what no test can see.** Markers, punctuation
        /// and technical terms are checked mechanically, case by case; handing them to a judge
        /// would measure the same thing twice and let a structural failure sink a mark that is
        /// supposed to be about language. So the criteria are the five the machine is blind to:
        /// right language, faithful meaning, tone kept, comparable length, natural reading.
        ///
        /// ⚠ **But the markers must still be EXPLAINED to it**, or it reads `[!v*0]` on both sides,
        /// takes it for gibberish and marks a correct translation down.
        ///
        /// 🔴 **Three limits, and they decide how the number may be read.**
        /// 1. **A judge cannot mark a language it does not have.** The model that wrote fluent
        ///    French believing it was writing Breton would have given itself a good mark, because
        ///    it would have re-read French believing it was re-reading Breton. The mark is least
        ///    trustworthy precisely where it would be most useful. No prompt fixes that.
        /// 2. Requests carry no memory, so the judge does not know the translation is its own —
        ///    but a model still prefers text that is stylistically familiar to it. A bias without
        ///    recognition, which is why the bench can point the judge at a different model: to
        ///    MEASURE that gap, not to pretend it has been corrected.
        /// 3. Small models bunch everything between 6 and 8. What carries meaning is the gap
        ///    between two models or two versions of the prompt, never one mark on its own.
        /// ⇒ Hence "self-assessment" wherever this is shown: the word says the limit out loud.
        ///
        /// ⚠ The answer must be a bare number. Anything else is refused by the caller rather than
        /// repaired — a default mark would be a measurement nobody made.
        /// </summary>
        public static string ForRating(string sourceLanguage, string targetLanguage, Markers markers)
        {
            var prompt = new StringBuilder();

            prompt.AppendLine("=== CONTEXT ===");
            prompt.AppendLine($"Rating one translation of video game text, from {sourceLanguage} to {targetLanguage}.");
            prompt.AppendLine("You are not translating. You are comparing two texts.");
            prompt.AppendLine();

            prompt.AppendLine("=== WHAT TO JUDGE ===");
            prompt.AppendLine($"- Is the translation written in {targetLanguage}?");
            prompt.AppendLine("- Does it say what the source says?");
            prompt.AppendLine("- Does it keep the tone of the source?");
            prompt.AppendLine("- Is it about as long as the source?");
            prompt.AppendLine("- Does it read naturally?");
            prompt.AppendLine();

            if (markers.LineBreaks || markers.Tags || markers.Numbers || markers.Variables)
            {
                prompt.AppendLine("=== IGNORE THESE ===");
                prompt.AppendLine("[!nl], [!t*0], [!v*0], [!STR*0] and the like are slots the game fills in.");
                prompt.AppendLine("They are checked elsewhere. Do not judge them.");
                prompt.AppendLine();
            }

            prompt.AppendLine("=== ANSWER ===");
            prompt.Append("Reply with one number from 0 to 10. Nothing else.");

            return prompt.ToString();
        }

        /// <summary>
        /// The three rules both jobs share: which language, which tone, which length.
        ///
        /// 🔴 **The target language is NAMED, not referred to.** It used to read "correct in target
        /// language", which a small model can satisfy while writing something else entirely — asked
        /// for Breton, one wrote fluent French for every long line and no rule was broken. Naming it
        /// makes the language appear twice in the prompt, which is the cheapest hold a small model
        /// gives you.
        ///
        /// ⚠ **The tone rule carries NO examples, deliberately.** "formal, casual or playful" reads
        /// as a closed list: the model picks one of the three instead of keeping what is there, and
        /// ironic, solemn, childish or menacing quietly disappear.
        ///
        /// 🔴 **Length is measured against the SOURCE, never against a threshold.** This replaced
        /// "Keep it concise for UI", which asserted a nature — *this is UI* — that is false for a
        /// character name, a place, an item or a line of dialogue. A relative rule needs no such
        /// guess, and it is the only formulation that survives every script: three Latin words and
        /// three ideograms are not the same amount of text, so any counting rule we wrote would be
        /// wrong somewhere. The model knows both languages; it converts better than a number would.
        ///
        /// ⚠ Which is also why <see cref="TextType"/> does NOT branch here. Sorting a text by shape
        /// to guess what it is cannot be done reliably — "Kyoto" and "OK" have the same shape — and
        /// a relative rule makes the guess unnecessary.
        ///
        /// ⚠ And why the model is not asked to classify the text itself either: it answers with the
        /// verdict in the line — `UI label: Annuler` — and that line is what gets written into the
        /// game. Same reason reasoning is turned off through a parameter rather than by adding a
        /// marker to the text (see the mod's request builder).
        /// </summary>
        private static void AppendCommonRules(StringBuilder prompt, string targetLanguage)
        {
            prompt.AppendLine($"- Write natural, correct {targetLanguage}");
            prompt.AppendLine("- Keep the tone of the source");
            prompt.AppendLine("- Keep it about as long as the source");
            prompt.AppendLine("- Do not add punctuation if not in the source to translate");
        }

        /// <summary>
        /// One rule per kind of placeholder the text really carries.
        ///
        /// ⚠ **Each one says what it REPLACES, not merely that it must survive**, and that costs no
        /// extra line. `[!v*0]` used to be described as nothing at all — "placeholders" — and `[!nl]`
        /// no better. Naming them buys real things: knowing `[!v*0]` stands for a number lets the
        /// model agree the plural around it, and knowing `[!STR*0]` is text the game drops in is
        /// exactly what Korean needs — 은/는 and 이/가 are chosen by the last sound of the word
        /// before them, which the marker hides — and what French, Spanish or Arabic need to avoid
        /// guessing a gender. A model told this can reach for a neutral turn of phrase instead.
        ///
        /// ⚠ `[!STR*N]` is **text**, deliberately not "a name": it holds whatever the game puts in
        /// a string variable — an item, a place, a turn of phrase. Saying "name" would be narrower
        /// than the truth and invite the model to treat everything else as translatable.
        /// </summary>
        private static void AppendMarkerRules(StringBuilder prompt, Markers markers)
        {
            if (markers.LineBreaks)
                prompt.AppendLine("- IMPORTANT: [!nl] is a line break: keep it exactly where it is, do not remove or move it");
            if (markers.Tags)
                prompt.AppendLine("- IMPORTANT: [!t*0], [!t*1], etc. are formatting tags: keep them exactly as-is, do not modify or remove them");
            if (markers.Numbers)
                prompt.AppendLine("- IMPORTANT: [!v*0], [!v*1], etc. are numbers: keep them exactly as-is, do not modify them");
            if (markers.Variables)
                prompt.AppendLine("- IMPORTANT: [!STR*0], [!STR*1], etc. are text the game inserts: keep them exactly as-is, do not translate them");
        }

        private static void AppendSingleWordClosing(StringBuilder prompt, TextType textType)
        {
            if (textType != TextType.SingleWord) return;

            prompt.AppendLine();
            prompt.Append("Now, translate this word:");
        }
    }
}
