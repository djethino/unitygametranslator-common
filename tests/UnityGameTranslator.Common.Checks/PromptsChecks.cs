using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// What a model is actually told, and how a text is sorted before being asked for.
    ///
    /// Every sentence below leaves the machine. They are the instructions a game sends and the
    /// instructions a bench scores models against — the same ones now, which is the point.
    /// </summary>
    internal static class PromptsChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            // Sorting a text: spaces, except where a script does not use them.
            check(Prompts.Classify("Play") == TextType.SingleWord, "one Latin word", "no space in it");
            check(Prompts.Classify("New game") == TextType.Phrase, "two are a phrase", "a space says so");
            check(Prompts.Classify("Line one\nLine two") == TextType.Paragraph, "line breaks make a paragraph",
                "detected on the raw text, before breaks become placeholders");
            check(Prompts.Classify("") == TextType.SingleWord, "nothing is treated as a word", "nothing to split");

            // ⚠ Scripts written without spaces. Judged by length or every sentence is "one word".
            check(Prompts.Classify("開始") == TextType.SingleWord, "two ideograms are one word", "four characters or fewer");
            check(Prompts.Classify("新しいゲームを始める") == TextType.Phrase,
                "a Japanese sentence is a phrase", "it has no spaces at all, and counting them would call it a word");
            check(Prompts.Classify("เริ่มเกมใหม่") == TextType.Phrase, "Thai likewise", "same reason, different script");

            // The single-word closing is the only thing the classification changes.
            check(Game(TextType.SingleWord).TrimEnd().EndsWith("Now, translate this word:", StringComparison.Ordinal),
                "a single word gets its closing line", "the one effect the whole classification has");
            check(!Game(TextType.Phrase).Contains("translate this word"),
                "a phrase does not", "it would be asking for the wrong thing");

            // Context and target.
            check(Game(TextType.Phrase).Contains("to French"), "the target language is named", "the whole point");
            check(Game(TextType.Phrase, source: "English").Contains("from English to French"),
                "and the source when it is known", "so the model does not have to guess");
            check(!Game(TextType.Phrase).Contains("from "), "left out when it is not",
                "claiming a source we do not know would be worse than saying nothing");
            check(Game(TextType.Phrase).Contains("video game UI, menus and dialogues"),
                "a default context when the author gave none", "an empty parenthesis says nothing");
            check(Game(TextType.Phrase, context: "grim dungeon crawler").Contains("grim dungeon crawler"),
                "and the author's own words when they did", "they know their game");

            // The strict-source block, and what it is gated on.
            check(Game(TextType.Phrase, source: "English", strict: true).StartsWith("=== CRITICAL RULE ===", StringComparison.Ordinal),
                "strict source comes first", "it decides whether to translate at all");
            check(Game(TextType.Phrase, source: "English", strict: true).Contains(Prompts.SkipMarker),
                "and names the exact answer expected", "anything else would be taken as a translation");
            check(!Game(TextType.Phrase, source: null, strict: true).Contains("CRITICAL RULE"),
                "impossible without a source language", "there would be nothing to compare against");

            // ⚠ It applies to EVERY text, not only to a test of itself.
            check(Game(TextType.Phrase, source: "English", strict: true).Contains("=== TRANSLATION RULES ==="),
                "and it is added to the ordinary rules, not instead of them",
                "the bench used to try it alone, so it never saw it affect a normal line");

            // Marker rules: only for what the text really contains.
            var none = new Prompts.Markers();
            check(!Game(TextType.Phrase, markers: none).Contains("[!nl]"), "silence about absent placeholders",
                "naming one invites a model to invent it");
            var all = new Prompts.Markers { LineBreaks = true, Tags = true, Numbers = true, Variables = true };
            string full = Game(TextType.Phrase, markers: all);
            check(full.Contains("[!nl]") && full.Contains("[!t*0]") && full.Contains("[!v*0]") && full.Contains("[!STR*0]"),
                "and one rule each for those it does", "four kinds, four sentences");

            // Reading an answer: three outcomes, and the third is the point.
            check(Prompts.ReadAnswer(Prompts.SkipMarker) == Prompts.AnswerKind.Skip,
                "the marker alone is a refusal", "the caller keeps the original and tags it S");
            check(Prompts.ReadAnswer("  " + Prompts.SkipMarker + "\n") == Prompts.AnswerKind.Skip,
                "surrounding blank space does not change that", "models add a newline");
            check(Prompts.ReadAnswer("Démarrer la partie") == Prompts.AnswerKind.Translation,
                "an ordinary answer is a translation", "nothing special about it");
            check(Prompts.ReadAnswer("Démarrer la partie " + Prompts.SkipMarker) == Prompts.AnswerKind.Unusable,
                "a translation carrying the marker is thrown away",
                "read as a refusal it drops a good line; read as a translation it writes the marker into the game");
            check(Prompts.ReadAnswer("") == Prompts.AnswerKind.Unusable
                  && Prompts.ReadAnswer(null) == Prompts.AnswerKind.Unusable,
                "and so is nothing at all", "there is no line to store");

            // The mod's own interface: a different job, different rules.
            string ui = Prompts.ForOwnInterface("French", TextType.Phrase, none);
            check(ui.Contains("from English to French"), "its source is always English", "the interface is written in it");
            check(!ui.Contains("CRITICAL RULE"), "so it can never carry the strict-source block", "there is nothing to detect");
            check(ui.Contains("API, URL, UUID, JSON, AI") && ui.Contains("Ctrl, Alt, Shift"),
                "it protects this tool's own vocabulary", "not a game's settings");
            check(!ui.Contains("VSync"), "and not the other way round", "a game's rules would be noise here");
        }

        private static string Game(TextType textType, string? source = null, string? context = null,
                                   bool strict = false, Prompts.Markers markers = default) =>
            Prompts.ForGameText("French", source, context, strict, textType, markers);
    }
}
