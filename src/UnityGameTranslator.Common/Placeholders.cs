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

            foreach (var entry in inAnswer)
            {
                if (!inSource.ContainsKey(entry.Key))
                    errors.Add($"token {entry.Key} does not exist in the source");
            }

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
            var message = new StringBuilder();
            message.AppendLine("Your translation is INVALID:");

            foreach (string error in errors) message.AppendLine($"- {error}");

            if (frozen.Count > 0)
            {
                message.AppendLine("These exact character sequences from the source must appear unchanged in your translation:");
                message.AppendLine(string.Join(", ", frozen.Select(sequence => $"\"{sequence}\"")));
            }

            message.Append("Reply with ONLY the corrected translation, nothing else.");
            return message.ToString();
        }

        /// <summary>
        /// The last attempt's extra instructions: a fresh start, with the sequences spelt out.
        /// </summary>
        public static string MandatorySequences(List<string> frozen)
        {
            var section = new StringBuilder();
            section.AppendLine("=== MANDATORY EXACT SEQUENCES ===");
            section.AppendLine("The text contains technical placeholders. Your output MUST contain these exact character sequences, copied character-for-character, unmodified:");

            foreach (string sequence in frozen) section.AppendLine($"\"{sequence}\"");

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
