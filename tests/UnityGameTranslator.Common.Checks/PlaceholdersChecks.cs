using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// What a game will and will not accept back from a model.
    ///
    /// These rules decide whether a translated line works or breaks, and the sentences below are
    /// sent verbatim to a model on its second attempt — so the wording is part of the behaviour,
    /// not a message for us. Both the mod and the test suite now run this exact code; the point of
    /// the cases is that neither can quietly start scoring a different exercise.
    /// </summary>
    internal static class PlaceholdersChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            // What has to come back untouched, brackets included.
            check(Frozen("Hello [!v*0]").SequenceEqual(new[] { "[!v*0]" }),
                "a bare token is frozen as itself", "nothing around it to keep");
            check(Frozen("Cost: ({[!v*0]})").SequenceEqual(new[] { "({[!v*0]})" }),
                "the game's own brackets travel with it",
                "keeping the token and losing the bracket still breaks the line");
            check(Frozen("[!v*0] and [!v*0]").SequenceEqual(new[] { "[!v*0]" }),
                "the same sequence is listed once", "it is a list of what must appear, not a tally");
            check(Frozen("[!v*0][!t*1]").SequenceEqual(new[] { "[!v*0]", "[!t*1]" }),
                "neighbours are not swallowed",
                "expanding only over opening then closing characters cannot cross a token");
            check(Frozen("").Count == 0 && Frozen("plain text").Count == 0,
                "a text without placeholders freezes nothing", "and needs no validation at all");

            // ⚠ The case that makes containment insufficient.
            check(!Accepts("[!v*0]", "[{[!v*0]}]"),
                "brackets added around a token are refused",
                "\"[{[!v*0]}]\" contains \"[!v*0]\" and is still wrong");

            check(Accepts("Hello [!v*0]", "Bonjour [!v*0]"), "an ordinary translation passes", "same tokens, same brackets");
            check(!Accepts("A [!v*0] B [!v*1]", "A [!v*0] B"), "a dropped token is refused", "the game would substitute nothing");
            check(!Accepts("A [!v*0]", "A [!v*0] [!v*0]"), "a duplicated one too", "it would appear twice in game");
            check(!Accepts("A [!v*0]", "A [!v*7]"), "and an invented one", "the game has no seventh value to put there");

            // The error lines are the next prompt, so they have to say what is wrong.
            List<string> errors = ErrorsOf("A [!v*0] B [!v*1]", "A [!v*0] B");
            check(errors.Any(e => e.Contains("[!v*1]")), "the refusal names the token",
                "this text is handed to the model, not written in a log");

            // The one repair made without asking again.
            check(Repair("Line[!nl][!nl]", "Ligne") == "Ligne[!nl][!nl]",
                "trailing line breaks are put back", "models tidy them away, every single time");
            check(Repair("[!nl]A[!nl]", "A[!nl]") == null,
                "but only when the deficit is entirely at the end",
                "a break lost in the middle is a different sentence, and nobody wrote it");
            check(Repair("A[!nl]", "A[!nl]") == null, "nothing to repair means no repair", "no deficit");
            check(Repair("A", "A[!nl]") == null, "and an extra break is not repaired either", "only ever appends");
            check(Accepts("Line[!nl][!nl]", Repair("Line[!nl][!nl]", "Ligne")!),
                "what the repair produces is accepted", "otherwise it would cost an attempt for nothing");

            // The two messages sent back to the model.
            string correction = Placeholders.Correction(errors, Frozen("A [!v*0] B [!v*1]"));
            check(correction.StartsWith("Your translation is INVALID:", StringComparison.Ordinal),
                "the correction opens on the verdict", "the model must not have to look for it");
            check(correction.Contains("[!v*1]") && correction.TrimEnd().EndsWith("nothing else.", StringComparison.Ordinal),
                "it names what is missing and asks for the answer alone",
                "anything else comes back as prose around the translation");

            string mandatory = Placeholders.MandatorySequences(Frozen("({[!v*0]})"));
            check(mandatory.Contains("=== MANDATORY EXACT SEQUENCES ===") && mandatory.Contains("({[!v*0]})"),
                "the last attempt spells the sequences out", "a fresh start, with nothing left implicit");

            check(Placeholders.MaxAttempts == 3, "three attempts", "plain, corrected, then restated");
        }

        private static List<string> Frozen(string source) => Placeholders.FrozenSequences(source);

        private static bool Accepts(string source, string translation) =>
            Placeholders.Accepts(source, translation, Frozen(source), out _);

        private static List<string> ErrorsOf(string source, string translation)
        {
            Placeholders.Accepts(source, translation, Frozen(source), out List<string> errors);
            return errors;
        }

        private static string? Repair(string source, string translation) =>
            Placeholders.RepairTrailingBreaks(source, translation);
    }
}
