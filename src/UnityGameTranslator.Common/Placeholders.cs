using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// The rules a machine translation has to obey to be usable in a game, and what to say to a
    /// model that broke them.
    ///
    /// A game's text carries technical placeholders — [!v*0] for a value, [!t*0] for a tag,
    /// [!STR*0] for a nested string, [!nl] for a line break. They are not decoration: the game
    /// substitutes them at runtime, so one lost, duplicated or invented token is a line that
    /// breaks, and no amount of good prose makes up for it.
    ///
    /// ⚠ This is the mod's own logic, and it was reproduced in the manager to measure how well
    /// models comply with it. A reproduction is exactly what must not exist here: what the tests
    /// score has to be what a game actually enforces, down to the wording sent back to the model
    /// on a second attempt, or the score describes the test bench rather than the model.
    ///
    /// The two copies were compared character by character before being merged — same regex, same
    /// expansion over brackets, same checks in the same order, same sentences. What follows is
    /// that agreement, written once.
    /// </summary>
    public static class Placeholders
    {
        /// <summary>
        /// How many times a text is sent before it is left untranslated.
        ///
        /// Three, and the shape matters as much as the number: a first plain attempt, a second
        /// carrying <see cref="Correction"/> as targeted feedback, a third starting fresh with
        /// <see cref="MandatorySequences"/> added to the instructions.
        /// </summary>
        public const int MaxAttempts = 3;

        /// <summary>Every frozen token: [!v*N], [!t*N], [!STR*N], [!nl].</summary>
        private static readonly Regex TokenPattern = new Regex(
            @"\[!(?:v\*\d+|t\*\d+|STR\*\d+|nl)\]", RegexOptions.Compiled);

        /// <summary>
        /// Each placeholder together with the delimiters the game wrapped around it, e.g. "({[!v*0]})".
        ///
        /// Expanding left over opening characters only and right over closing ones cannot swallow a
        /// neighbour, since every token itself starts with '[' and ends with ']'. These sequences
        /// have to come back verbatim: an answer that keeps the token but drops the bracket the
        /// game put around it still breaks the line.
        /// </summary>
        public static List<string> FrozenSequences(string source)
        {
            var sequences = new List<string>();
            if (string.IsNullOrEmpty(source)) return sequences;

            foreach (Match match in TokenPattern.Matches(source))
            {
                int start = match.Index;
                int end = match.Index + match.Length; // exclusive

                while (start > 0 && (source[start - 1] == '{' || source[start - 1] == '(' || source[start - 1] == '['))
                    start--;

                while (end < source.Length && (source[end] == '}' || source[end] == ')' || source[end] == ']'))
                    end++;

                string sequence = source.Substring(start, end - start);
                if (!sequences.Contains(sequence)) sequences.Add(sequence);
            }

            return sequences;
        }

        /// <summary>
        /// Whether a game would accept this answer.
        ///
        /// ⚠ Containment is not enough — "[{[!v*0]}]" contains "[!v*0]" and is still wrong. So:
        /// the frozen sequences verbatim, then the tokens as the same multiset (nothing missing,
        /// duplicated or invented), then the brackets unchanged, which catches the answer that
        /// wrapped a placeholder in a pair of its own.
        ///
        /// The error lines are not for a log: they are handed to the model on the next attempt,
        /// which is why they name the token and the counts rather than saying "invalid".
        /// </summary>
        public static bool Accepts(string source, string translation, List<string> frozen, out List<string> errors)
        {
            KeepsEveryPlaceholder(source, translation, frozen, out errors);

            // ⚠ The one check a PERSON is not held to — see AcceptsEdit. It exists for a model that
            // wraps a placeholder in a pair of its own, which the frozen sequences catch for the
            // placeholders themselves; over the whole text it also refuses a bracket somebody added
            // on purpose, and a model cannot be asked what it meant.
            foreach (char bracket in new[] { '{', '}', '[', ']' })
            {
                int expected = source.Count(c => c == bracket);
                int found = translation.Count(c => c == bracket);
                if (expected != found)
                    errors.Add($"character '{bracket}' appears {found} time(s) instead of {expected}");
            }

            return errors.Count == 0;
        }

        /// <summary>
        /// Whether a game would accept a translation somebody typed HERE, in an editor, with the
        /// result in front of them.
        ///
        /// 🔴 **The same rule about placeholders as a model is held to, and that is the point.** A
        /// dropped, duplicated or invented token breaks the line whoever wrote it: the game
        /// substitutes at runtime and does not care that a human was at the keyboard. This used to
        /// be a looser rule of the mod's own — missing and unknown only — so a person could
        /// duplicate a placeholder where a model would have been refused, and save it.
        ///
        /// ⚠ **One check is deliberately left out: the count of brackets over the whole text.** It
        /// guards against a model wrapping a placeholder in a pair of its own, and the frozen
        /// sequences already cover that for the placeholders. Applied to a person it would refuse
        /// an edit like "Save" → "Save [F5]", which is theirs to make and breaks nothing.
        ///
        /// ⚠ The lines it returns are read on a screen rather than sent to a model, so they name
        /// the token and the count and stop there — <see cref="Correction"/> is the other audience.
        /// </summary>
        public static bool AcceptsEdit(string source, string edited, List<string> frozen, out List<string> errors)
        {
            KeepsEveryPlaceholder(source, edited, frozen, out errors);
            return errors.Count == 0;
        }

        /// <summary>
        /// What both audiences are held to: the frozen sequences verbatim, then the tokens as the
        /// same multiset — nothing missing, duplicated or invented.
        /// </summary>
        private static void KeepsEveryPlaceholder(string source, string translation,
                                                  List<string> frozen, out List<string> errors)
        {
            errors = new List<string>();

            foreach (string sequence in frozen)
            {
                if (Occurrences(translation, sequence) < Occurrences(source, sequence))
                    errors.Add($"the exact sequence \"{sequence}\" is missing or altered");
            }

            Dictionary<string, int> inSource = Tally(source);
            Dictionary<string, int> inAnswer = Tally(translation);

            foreach (var entry in inSource)
            {
                int found;
                inAnswer.TryGetValue(entry.Key, out found);
                if (found != entry.Value)
                    errors.Add($"token {entry.Key} appears {found} time(s) instead of {entry.Value}");
            }

            // ⚠ Through Invented, so this clause and the belt the mod puts after the backends are
            // one implementation. Only the wording is decided here: these lines go to the model on
            // the next attempt, and the belt has no model to talk to.
            foreach (string token in Invented(source, translation))
                errors.Add($"token {token} does not exist in the source");
        }

        /// <summary>
        /// The one repair worth making without asking again: line breaks trimmed off the very end.
        ///
        /// Models love tidying trailing newlines. Seen in the field on a credits roll of 51 tokens
        /// that came back with 50 every single time, burning three calls on every launch, forever.
        ///
        /// ⚠ Only ever APPENDS, and only when the trailing deficit explains the whole difference.
        /// Every other mismatch keeps the strict refusal: guessing at what a model meant elsewhere
        /// would put words in a game that nobody wrote. Null when it does not apply.
        /// </summary>
        public static string? RepairTrailingBreaks(string source, string translation)
        {
            const string token = "[!nl]";
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(translation)) return null;

            int deficit = Occurrences(source, token) - Occurrences(translation, token);
            if (deficit <= 0) return null;

            if (Trailing(source, token) - Trailing(translation, token) != deficit) return null;

            var repaired = new StringBuilder(translation.TrimEnd());
            for (int i = 0; i < deficit; i++) repaired.Append(token);
            return repaired.ToString();
        }

        /// <summary>
        /// The second attempt's message: what exactly was wrong, and what has to reappear.
        ///
        /// Targeted feedback corrects far better than "try again" — which is the whole reason a
        /// second attempt is worth its cost.
        /// </summary>
        public static string Correction(List<string> errors, List<string> frozen)
        {
            // ⚠ Line breaks are "\n", never the platform's: the message is frozen verbatim in the
            // corpus (rules/placeholders.json) and a port on another system must produce it byte
            // for byte. A model reads either the same.
            var message = new StringBuilder();
            message.Append("Your translation is INVALID:").Append('\n');

            foreach (string error in errors) message.Append($"- {error}").Append('\n');

            if (frozen.Count > 0)
            {
                message.Append("These exact character sequences from the source must appear unchanged in your translation:").Append('\n');
                message.Append(string.Join(", ", frozen.Select(sequence => $"\"{sequence}\""))).Append('\n');
            }

            message.Append("Reply with ONLY the corrected translation, nothing else.");
            return message.ToString();
        }

        /// <summary>
        /// The last attempt's extra instructions: a fresh start, with the sequences spelt out.
        /// </summary>
        public static string MandatorySequences(List<string> frozen)
        {
            // Same "\n" as Correction, for the same reason.
            var section = new StringBuilder();
            section.Append("=== MANDATORY EXACT SEQUENCES ===").Append('\n');
            section.Append("The text contains technical placeholders. Your output MUST contain these exact character sequences, copied character-for-character, unmodified:").Append('\n');

            foreach (string sequence in frozen) section.Append($"\"{sequence}\"").Append('\n');

            return section.ToString();
        }

        /// <summary>
        /// Every placeholder in a text, in order, repeats included.
        ///
        /// Exposed so that nothing else has to carry the pattern: it was written twice — here and
        /// in the mod, for uses that have nothing to do with retrying — and two literals of the
        /// same regular expression are two chances to add a token form to one of them only.
        /// </summary>
        public static IEnumerable<string> Tokens(string text)
        {
            if (string.IsNullOrEmpty(text)) yield break;

            foreach (Match match in TokenPattern.Matches(text))
                yield return match.Value;
        }

        /// <summary>
        /// The placeholders an answer carries that its source never had, in the order they appear,
        /// each named once. Empty when nothing was invented.
        ///
        /// 🔴 **Its own question because it is asked where <see cref="Accepts"/> is not.** That one
        /// runs only when the source HAS placeholders — no placeholder, nothing to keep, single
        /// attempt, no validation. So the one answer nobody was checking is the answer to a source
        /// with none: a small model replying with a bare "[!STR*0]", or appending one to an
        /// otherwise correct sentence. Such an entry replaces the text on screen AND is shared with
        /// everyone on upload.
        ///
        /// ⚠ It was a loop of its own in the mod, testing tokens against the raw source with a
        /// substring search. Same answers, but written twice — and this is exactly the file whose
        /// own comment says two literals of one pattern are two chances to teach only one of them a
        /// new token form.
        /// </summary>
        public static List<string> Invented(string source, string translation)
        {
            var invented = new List<string>();
            if (string.IsNullOrEmpty(translation)) return invented;

            Dictionary<string, int> inSource = Tally(source);

            foreach (string token in Tokens(translation))
            {
                if (inSource.ContainsKey(token)) continue;
                if (!invented.Contains(token)) invented.Add(token);
            }

            return invented;
        }

        /// <summary>How many times each placeholder appears.</summary>
        public static Dictionary<string, int> Tally(string text)
        {
            var tally = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (Match match in TokenPattern.Matches(text))
            {
                int count;
                tally.TryGetValue(match.Value, out count);
                tally[match.Value] = count + 1;
            }

            return tally;
        }

        /// <summary>
        /// Counted by walking, not by splitting: overlapping is impossible here and a split would
        /// allocate a whole array to answer a question about a number.
        /// </summary>
        private static int Occurrences(string text, string token)
        {
            if (string.IsNullOrEmpty(token)) return 0;

            int count = 0;
            int index = 0;

            while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }

        private static int Trailing(string text, string token)
        {
            int count = 0;
            string trimmed = text.TrimEnd();

            while (trimmed.EndsWith(token, StringComparison.Ordinal))
            {
                count++;
                trimmed = trimmed.Substring(0, trimmed.Length - token.Length).TrimEnd();
            }

            return count;
        }
    }
}
