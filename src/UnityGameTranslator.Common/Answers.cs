using System;
using System.Text.RegularExpressions;

namespace UnityGameTranslator.Common
{
    /// <summary>What came back, once the refusal marker has been taken into account.</summary>
    public enum AnswerKind
    {
        /// <summary>A translation to use.</summary>
        Translation,

        /// <summary>A refusal: the text was not in the source language. Keep the original, tag it S.</summary>
        Skip,

        /// <summary>Neither. Do not store it.</summary>
        Unusable,
    }

    /// <summary>
    /// Reading what a model sent back.
    ///
    /// Separate from <see cref="Prompts"/> on purpose: one says what we ask, this one says what we
    /// do with what arrives. They change for different reasons.
    ///
    /// ⚠ A game shows what comes back, verbatim. Everything a model wraps around its answer —
    /// a "Translation:" prefix, markdown emphasis, a note explaining itself, quotation marks it
    /// added — ends up on a player's screen unless it is taken off here. That is why this exists,
    /// and why a bench that judges raw answers marks models down for something a game never sees.
    /// </summary>
    public static class Answers
    {
        /// <summary>
        /// What a model is told to answer when the text is not in the source language at all.
        ///
        /// Deliberately not a word: it must never collide with something a game could legitimately
        /// contain, and it must survive being echoed back verbatim.
        /// </summary>
        public const string SkipMarker = "AxNoTranslateXa";

        /// <summary>
        /// Read an answer: a translation, a refusal, or something to throw away.
        ///
        /// ⚠ A refusal is the marker ALONE. What follows depends on it: the caller keeps the
        /// original text and tags the entry "S", and it can only decide that if the answer says
        /// nothing else.
        ///
        /// ⚠ The third outcome is why this is not a boolean. An answer that translates AND appends
        /// the marker is a real thing models do, and both simple rules get it wrong: read as a
        /// refusal it drops a line that was translated perfectly well, read as a translation it
        /// writes the marker into the game. Neither is recoverable afterwards and neither says
        /// anything at the time, so it is discarded — one line this session, nothing corrupted.
        /// </summary>
        public static AnswerKind Read(string? answer)
        {
            if (answer == null) return AnswerKind.Unusable;

            string trimmed = answer.Trim();
            if (trimmed.Length == 0) return AnswerKind.Unusable;

            if (string.Equals(trimmed, SkipMarker, StringComparison.Ordinal)) return AnswerKind.Skip;

            return trimmed.IndexOf(SkipMarker, StringComparison.Ordinal) >= 0
                ? AnswerKind.Unusable
                : AnswerKind.Translation;
        }

        /// <summary>
        /// Take off everything a model wrapped around its translation.
        ///
        /// ⚠ Each rule below is narrow on purpose, because every one of them can eat real text.
        /// Quotes come off only when they wrap the WHOLE answer, since a line of dialogue may
        /// legitimately be quoted. An explanation is cut only after a blank line and only when it
        /// opens the way models open one. A prefix is removed only at the very start.
        ///
        /// ⚠ Applied before an answer is judged, in the game and on the bench alike. A model that
        /// wraps its answer in quotation marks is not a model that broke the rules — it is one a
        /// game copes with — and scoring it as a failure measures the bench.
        /// </summary>
        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Reasoning models emit their working out first.
            text = Regex.Replace(text, @"<think>[\s\S]*?</think>\s*", "", RegexOptions.IgnoreCase);

            // ⚠ Only the literal form. The mod stopped sending these markers — reasoning is turned
            // off through a request field instead — but a model or a server-side template can still
            // echo one. Once a model TRANSLATES the marker it is unrecognisable, which is precisely
            // why sending it was abandoned.
            text = text.Replace(" /no_think", "").Replace("/no_think", "");
            text = text.Replace(" /think", "").Replace("/think", "");

            text = Regex.Replace(text, @"\*\*([^*]+)\*\*", "$1");

            text = Regex.Replace(text, @"^(Translation|Traduction|Here'?s?|The translation is)\s*[:\-]?\s*", "",
                                 RegexOptions.IgnoreCase);

            // After a blank line, and only when it opens the way an explanation opens — otherwise
            // a translation that genuinely contains a blank line would lose everything after it.
            Match explanation = Regex.Match(text, @"\n\n(Note:|I |This |Here |The above|Explanation:|Translation note:)",
                                            RegexOptions.IgnoreCase);
            if (explanation.Success) text = text.Substring(0, explanation.Index);

            text = text.Trim();
            if ((text.StartsWith("\"", StringComparison.Ordinal) && text.EndsWith("\"", StringComparison.Ordinal)) ||
                (text.StartsWith("'", StringComparison.Ordinal) && text.EndsWith("'", StringComparison.Ordinal)))
            {
                text = text.Substring(1, text.Length - 2);
            }

            return text.Trim();
        }
    }
}
