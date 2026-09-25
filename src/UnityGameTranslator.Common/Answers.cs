using System;
using System.Text.RegularExpressions;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// What becomes of one line — the four things a translator can do with it, and nothing else.
    ///
    /// 🔴 **The name says what is stored AND under which tag**, because the two have never been
    /// separable: a line kept as it is only means anything tagged S, and an interface line is only
    /// out of the game's file because it is tagged M. Splitting them is how a refused interface
    /// label came to be filed S in the game's file.
    /// </summary>
    public enum Filing
    {
        /// <summary>Store nothing at all. The line stays as the game wrote it.</summary>
        Nothing,

        /// <summary>Store what came back, as the machine's work — tag A.</summary>
        Machine,

        /// <summary>Store the SOURCE text, as a line somebody ruled must not be translated — tag S.</summary>
        KeptAsIs,

        /// <summary>Store what came back in the mod's own interface — tag M, and never the game's file.</summary>
        Interface,

        /// <summary>Store an empty value: met in game, nobody has written it yet — tag H.</summary>
        Captured,
    }

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
        /// Read a mark out of ten from a rating pass, or null when there is not one to read.
        ///
        /// 🔴 **Null rather than a default.** A mark nobody produced, sitting in a column of marks
        /// that were, is a measurement invented by the tool — and it would land in the middle of
        /// the range, exactly where a real bad mark would have shown something. Better an empty
        /// cell that says "this model would not answer the question", which is itself a result.
        ///
        /// ⚠ Deliberately tolerant about what surrounds the number and strict about the number:
        /// models answer "7", "7/10", "**7**", "Rating: 7". All of those are an answer to the
        /// question. What is refused is a sentence with several numbers in it, where picking one
        /// would be guessing which one was meant.
        ///
        /// ⚠ A decimal is read and rounded rather than refused — a model that answers 7.5 has
        /// understood the question perfectly well.
        /// </summary>
        public static int? ReadRating(string? answer)
        {
            if (answer == null) return null;

            // "7/10" is one answer, not two numbers: the ten is the scale we asked for.
            string text = Regex.Replace(answer, @"/\s*10\b", " ");

            var numbers = Regex.Matches(text, @"-?\d+(?:[.,]\d+)?");
            if (numbers.Count != 1) return null;

            string found = numbers[0].Value.Replace(',', '.');
            if (!double.TryParse(found, System.Globalization.NumberStyles.Float,
                                 System.Globalization.CultureInfo.InvariantCulture, out double value))
                return null;

            // Out of range is a misread question, not a low mark: 0 and 10 both mean something.
            if (value < 0 || value > 10) return null;

            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

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
        /// What becomes of a line the backend answered for.
        ///
        /// 🔴 **Where a line comes from outranks what came back**, and getting that the wrong way
        /// round has cost twice. The refusal marker used to win over the origin, so a declined
        /// interface label was filed S — a tag that means "a person ruled this line must stay as it
        /// is" — in the GAME's file, counted in its contributions and merged like any game line.
        /// An interface line is an interface line whatever the model answered.
        ///
        /// ⚠ **A declined interface label is stored NOWHERE.** The source of the mod's own labels
        /// is always English, so a refusal there is the model declining a job it was handed
        /// wrongly, not a decision worth recording. Storing it would also make the refusal
        /// permanent: nothing would ask again.
        ///
        /// ⚠ In the socle because a Core in another language has to reach the same five answers, and
        /// because <see cref="AnswerKind"/>, <see cref="ModUi.Tag"/> and
        /// <see cref="Composition.Letter"/> — the three things this combines — already live here.
        /// </summary>
        /// <param name="fromOwnUi">The line is one of the mod's own labels, settled when it was queued.</param>
        /// <param name="kind">What came back, as <see cref="Read"/> judged it.</param>
        public static Filing Store(bool fromOwnUi, AnswerKind kind)
        {
            if (kind == AnswerKind.Unusable) return Filing.Nothing;

            if (fromOwnUi)
                return kind == AnswerKind.Skip ? Filing.Nothing : Filing.Interface;

            return kind == AnswerKind.Skip ? Filing.KeptAsIs : Filing.Machine;
        }

        /// <summary>
        /// What becomes of a line met while only COLLECTING the game's text — no backend is asked.
        ///
        /// 🔴 **The mod's own interface is not collected**, and a branch that ignored this filed the
        /// mod's menu labels in the GAME's file as empty human captures. Collecting gathers the
        /// game's strings for somebody to translate later, on the site or in the browser editor;
        /// the interface goes to neither, so an entry for it would have no editor, no destination
        /// and nothing to become.
        /// </summary>
        public static Filing Capture(bool fromOwnUi)
        {
            return fromOwnUi ? Filing.Nothing : Filing.Captured;
        }

        /// <summary>
        /// The tag a filing writes into the file, or null when it writes nothing.
        ///
        /// ⚠ Every letter comes from <see cref="Composition.Letter"/> or <see cref="ModUi.Tag"/> —
        /// none is spelled out here. A fifth spelling of "A" is how two products come to disagree
        /// about what a file says.
        ///
        /// ⚠ <see cref="Filing.Captured"/> is tagged H with an EMPTY value, which is what the file
        /// has always carried for a line nobody has written yet — see <see cref="Merge.PriorityOf"/>,
        /// which ranks that pair below everything. It is not <see cref="TagBand.Captured"/>, whose
        /// letter is empty because that band describes a line on a screen, not one in a file.
        /// </summary>
        public static string? TagOf(Filing filing)
        {
            switch (filing)
            {
                case Filing.Machine: return Composition.Letter(TagBand.Machine);
                case Filing.KeptAsIs: return Composition.Letter(TagBand.Skipped);
                case Filing.Interface: return ModUi.Tag;
                case Filing.Captured: return Composition.Letter(TagBand.Human);
                default: return null;
            }
        }

        /// <summary>
        /// Whether this filing stores the SOURCE text rather than what came back.
        ///
        /// ⚠ Asked separately because it is the one place the stored value is not the answer: a
        /// line kept as it is holds the game's own words, and the tag alone does not say so.
        /// </summary>
        public static bool StoresTheSource(Filing filing)
        {
            return filing == Filing.KeptAsIs;
        }

        /// <summary>
        /// Take off everything a model wrapped around its translation.
        ///
        /// ⚠ Each rule below is narrow on purpose, because every one of them can eat real text.
        /// Quotes come off only when they wrap the WHOLE answer, since a line of dialogue may
        /// legitimately be quoted. An explanation is cut only after a blank line and only when it
        /// opens the way models open one. A prefix is removed only at the very start. A code block
        /// comes off only when it is the whole answer (see <see cref="Unfence"/>).
        ///
        /// ⚠ Applied before an answer is judged, in the game and on the bench alike. A model that
        /// wraps its answer in quotation marks is not a model that broke the rules — it is one a
        /// game copes with — and scoring it as a failure measures the bench.
        ///
        /// 🔴 **Nothing comes off that the SOURCE carries too.** A game line quoted as a whole
        /// ("\"Hello\""), one that opens "Translation: English", one in a code block, one with
        /// **emphasis** — its translation carries the same thing, and it is the game's, not the
        /// model's. Each rule is therefore asked of the source first, with the same pattern, and
        /// skipped when the source answers yes. Until 2026-09-25 only the answer was read, and
        /// such a line lost its quotes or its first word.
        /// </summary>
        /// <param name="text">What the model sent back.</param>
        /// <param name="source">What it was given — the text as sent, placeholders included. Null
        /// or empty when there is none to compare with: every rule then applies.</param>
        public static string Clean(string text, string? source)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // As sent, a line break is [!nl]; read as one here, so a blank line in the game's text
            // is a blank line whichever way the model wrote it back.
            string from = (source ?? "").Replace("[!nl]", "\n");

            // Reasoning models emit their working out first.
            if (!Think.IsMatch(from)) text = Think.Replace(text, "");

            // ⚠ Only the literal form. The mod stopped sending these markers — reasoning is turned
            // off through a request field instead — but a model or a server-side template can still
            // echo one. Once a model TRANSLATES the marker it is unrecognisable, which is precisely
            // why sending it was abandoned.
            if (from.IndexOf("/no_think", StringComparison.Ordinal) < 0)
                text = text.Replace(" /no_think", "").Replace("/no_think", "");
            if (from.IndexOf("/think", StringComparison.Ordinal) < 0)
                text = text.Replace(" /think", "").Replace("/think", "");

            if (!Emphasis.IsMatch(from)) text = Emphasis.Replace(text, "$1");

            if (!Announcing.IsMatch(from)) text = Announcing.Replace(text, "");

            // After a blank line, and only when it opens the way an explanation opens — otherwise
            // a translation that genuinely contains a blank line would lose everything after it.
            if (!Explaining.IsMatch(from))
            {
                Match explanation = Explaining.Match(text);
                if (explanation.Success) text = text.Substring(0, explanation.Index);
            }

            text = text.Trim();
            string trimmedSource = StripDirection(from.Trim(), out _, out _);

            // ⚠ A model writing right to left may put an invisible direction mark before or after
            // everything, outside the fences or the quotes it added. The wrappers are judged
            // inside those marks, and the marks are put back where they were: they order what is
            // shown, and taking them off is not this method's business.
            string inner = StripDirection(text, out string leading, out string trailing);

            if (!IsFenced(trimmedSource)) inner = Unfence(inner);

            foreach (string quote in new[] { "\"", "'" })
            {
                if (IsWrappedIn(trimmedSource, quote)) continue;
                if (IsWrappedIn(inner, quote)) { inner = inner.Substring(1, inner.Length - 2); break; }
            }

            return (leading + inner.Trim() + trailing).Trim();
        }

        /// <summary>
        /// The text without the direction marks and spacing at its two ends, and those ends.
        /// The marks: LRM, RLM, ALM, the embeddings and overrides, the isolates — Unicode's
        /// bidirectional controls, none of which is a character anybody reads.
        /// </summary>
        private static string StripDirection(string text, out string leading, out string trailing)
        {
            int start = 0, end = text.Length;
            while (start < end && (IsDirectionMark(text[start]) || char.IsWhiteSpace(text[start]))) start++;
            while (end > start && (IsDirectionMark(text[end - 1]) || char.IsWhiteSpace(text[end - 1]))) end--;

            leading = text.Substring(0, start).Trim();
            trailing = text.Substring(end).Trim();
            return text.Substring(start, end - start);
        }

        private static bool IsDirectionMark(char c) =>
            c == '\u200E' || c == '\u200F' || c == '\u061C'
            || (c >= '\u202A' && c <= '\u202E') || (c >= '\u2066' && c <= '\u2069');

        // One pattern per rule, asked of the answer to take it off and of the source to know
        // whether it is the game's own.
        private static readonly Regex Think = new Regex(@"<think>[\s\S]*?</think>\s*", RegexOptions.IgnoreCase);
        private static readonly Regex Emphasis = new Regex(@"\*\*([^*]+)\*\*");
        private static readonly Regex Announcing = new Regex(
            @"^(Translation|Traduction|Here'?s?|The translation is)\s*[:\-]?\s*", RegexOptions.IgnoreCase);
        private static readonly Regex Explaining = new Regex(
            @"\n\n(Note:|I |This |Here |The above|Explanation:|Translation note:)", RegexOptions.IgnoreCase);

        private static bool IsWrappedIn(string text, string quote) =>
            text.Length >= 2 * quote.Length
            && text.StartsWith(quote, StringComparison.Ordinal) && text.EndsWith(quote, StringComparison.Ordinal);

        private static bool IsFenced(string text) =>
            text.StartsWith(Fence, StringComparison.Ordinal) || text.EndsWith(Fence, StringComparison.Ordinal);

        private const string Fence = "```";

        /// <summary>
        /// A markdown code block wrapping the WHOLE answer, taken off; anything else left alone.
        ///
        /// ⚠ Seen in a real translation file: two lines stored as "```\n…\n```", shown so in game.
        ///
        /// ⚠ Two shapes only, both complete: the block as markdown writes it — the opening fence
        /// with an optional language name on its own line, the closing fence on its own line — and
        /// the fences on one line around a text with no line break. A fence anywhere inside, or a
        /// shape that is neither, is text somebody may have written, and stays. In the block shape
        /// the first line is dropped only when it is empty or a bare language name ("json"), which
        /// is why the closing fence must stand on its own line too: "```Oui\nNon```" is not a
        /// block, and dropping "Oui" as a language name would eat a word.
        /// </summary>
        private static string Unfence(string text)
        {
            if (text.Length < 2 * Fence.Length
                || !text.StartsWith(Fence, StringComparison.Ordinal)
                || !text.EndsWith(Fence, StringComparison.Ordinal)) return text;

            string inner = text.Substring(Fence.Length, text.Length - 2 * Fence.Length);
            if (inner.IndexOf(Fence, StringComparison.Ordinal) >= 0) return text;

            int firstBreak = inner.IndexOf('\n');
            if (firstBreak < 0) return inner.Trim();

            int lastBreak = inner.LastIndexOf('\n');
            if (inner.Substring(lastBreak + 1).Trim().Length > 0) return text;

            string opening = inner.Substring(0, firstBreak).Trim();
            if (!Regex.IsMatch(opening, @"^[A-Za-z0-9_+\-]*$")) return text;

            return inner.Substring(firstBreak + 1).Trim();
        }
    }
}
