using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// A text taken apart for sending, and everything needed to put it back together.
    ///
    /// ⚠ The parts are held rather than re-derived: the tags are keyed by position in this list,
    /// and the whitespace was deliberately cut off — neither can be found again in the answer.
    /// </summary>
    public struct PreparedText
    {
        /// <summary>What a backend is actually given.</summary>
        public string ToSend;

        /// <summary>The markup that was lifted out, in the order its placeholders are numbered.</summary>
        public List<string> Tags;

        /// <summary>Visual padding held back, put on again at the very end.</summary>
        public string Leading;

        /// <inheritdoc cref="Leading"/>
        public string Trailing;

        /// <summary>
        /// The tag pairs that enclosed the whole text, in wire form ("[!t*0]" … "[!t*1]"), held
        /// back rather than sent — see <see cref="Backends.Prepare"/>. Empty when none did.
        /// </summary>
        public string Opening;

        /// <inheritdoc cref="Opening"/>
        public string Closing;

        /// <summary>True when nothing translatable is left. Nobody is asked.</summary>
        public bool NothingToSend;
    }

    /// <summary>
    /// What every backend does to a text before sending it, and to the answer before handing it on.
    ///
    /// 🔴 **Written once because it was written twice** — once per backend in the mod — and moved
    /// into the socle on 2026-09-23 because a third caller arrived: the Manager answers the browser
    /// editor's Retranslate while the game is closed, and must send exactly what the game would.
    ///
    /// 🔴 **The ORDER is the rule.** Going out: line breaks become tokens FIRST, then markup, then
    /// the padding is cut. The trim has to be last — once a trailing newline is a `[!nl]` token it
    /// is no longer whitespace, so it survives as something the answer must give back rather than
    /// being quietly shaved off. Coming back: markup, then line breaks, then the padding.
    ///
    /// ⚠ **Restoring never cleans a model's chatter** (changed 2026-09-23). It used to, AFTER the
    /// line breaks were back — and the clean ends with a trim, so a text ending in line breaks
    /// lost them on the model path, undoing exactly what the order above protects. A model's
    /// answer is now cleaned where <see cref="Answers.Clean"/> says it belongs: before it is judged,
    /// while its line breaks are still tokens (<see cref="LineTranslation"/>).
    ///
    /// ⚠ **Pure by contract**: strings in, strings out. What to send it to, how many times to ask,
    /// and what to do with a refusal stay with the caller.
    /// </summary>
    public static class Backends
    {
        /// <summary>The token a line break becomes while a text is away being translated.</summary>
        public const string LineBreak = "[!nl]";

        /// <summary>
        /// Take a text apart for sending: structure into tokens, padding held back.
        ///
        /// ⚠ Numbers are NOT done here. They are lifted much earlier, before the cache is even
        /// consulted, because the text with its numbers replaced IS the cache key.
        /// </summary>
        public static PreparedText Prepare(string text)
        {
            var prepared = new PreparedText { Leading = "", Trailing = "", Opening = "", Closing = "", Tags = new List<string>() };

            if (string.IsNullOrEmpty(text))
            {
                prepared.ToSend = text;
                prepared.NothingToSend = true;
                return prepared;
            }

            // 1-2. Line breaks, then markup, into tokens — see WireForm. Before the trim,
            //      deliberately — see the note on order above.
            string work = WireForm(text, out List<string> tags);
            prepared.Tags = tags;

            // 3. Visual padding held back. A model asked to translate "  Play  " answers about the
            //    spaces as often as not, and the game laid them out for a reason.
            string trimmed = work.TrimStart();
            if (trimmed.Length < work.Length)
            {
                prepared.Leading = work.Substring(0, work.Length - trimmed.Length);
                work = trimmed;
            }
            trimmed = work.TrimEnd();
            if (trimmed.Length < work.Length)
            {
                prepared.Trailing = work.Substring(trimmed.Length);
                work = trimmed;
            }

            // 4. A tag pair around the WHOLE text is held back too, and put on again around the
            //    answer. 🔴 Sent, a model drops it as often as not — "<color=…>疗伤效率+5%</color>"
            //    came back bare on all three attempts and the line stayed untranslated (2026-09-26)
            //    — and there is nothing to decide about where it goes: all of the translation
            //    belongs inside it, in any language and either direction. Only a real pair, the
            //    closing tag answering the opening one (Markup.Pairs): "<b>A</b> and <b>B</b>"
            //    starts and ends with a tag without one pair enclosing the rest, and is sent as
            //    it is. The numbering is kept, so a placeholder means the same tag either way.
            work = PeelEnclosingPairs(work, tags, out string opening, out string closing);
            prepared.Opening = opening;
            prepared.Closing = closing;

            prepared.ToSend = work;
            prepared.NothingToSend = string.IsNullOrWhiteSpace(work);
            return prepared;
        }

        /// <summary>
        /// A text in the form a model reads it, structure only: line breaks become [!nl] — so the
        /// answer has to give the shape back rather than reflowing it — then markup becomes
        /// [!t*N], so the model never sees a tag it could translate, reorder or invent. No
        /// padding is cut and no enclosing pair is held back: that is <see cref="Prepare"/>'s.
        ///
        /// 🔴 **Also how a translation is READ back in token form** by whoever checks one against
        /// what the game sent — the Manager's model bench. One conversion both ways: the bench
        /// once counted [!nl] in answers the game had already turned back into line breaks, and
        /// failed perfect ones (2026-09-26).
        /// </summary>
        public static string WireForm(string text, out List<string> tags)
        {
            tags = new List<string>();
            if (string.IsNullOrEmpty(text)) return text;
            return Markup.Extract(text.Replace("\n", LineBreak), out tags);
        }

        /// <summary>
        /// Put the answer back together: markup, line breaks, padding — in that order.
        ///
        /// ⚠ An empty answer is handed straight back. There is nothing to restore into it, and
        /// dressing an empty string in the original's padding would produce a translation made
        /// entirely of spaces.
        /// </summary>
        public static string Restore(PreparedText prepared, string answer)
        {
            if (string.IsNullOrEmpty(answer)) return answer;

            // The enclosing pairs first, in wire form, so the rest runs exactly as before. ⚠ Not
            // around an answer that already carries the opening one: a proposal recorded before
            // the pair was held back (the failed lines keep them across launches) has it inside.
            string opening = prepared.Opening ?? "", closing = prepared.Closing ?? "";
            if (opening.Length > 0 && answer.IndexOf(opening, System.StringComparison.Ordinal) < 0)
                answer = opening + answer + closing;

            string result = Markup.Restore(answer, prepared.Tags);
            result = result.Replace(LineBreak, "\n");

            if (prepared.Leading.Length > 0 || prepared.Trailing.Length > 0)
                result = prepared.Leading + result + prepared.Trailing;

            return result;
        }

        /// <summary>
        /// Take off, from both ends of a prepared text, every tag pair that encloses all of it —
        /// outermost first — and hand them back as the wire text to put on again. The padding
        /// found inside a pair goes with it: it was the game's, and it is not the model's to keep.
        /// </summary>
        private static string PeelEnclosingPairs(string work, List<string> tags, out string opening, out string closing)
        {
            opening = "";
            closing = "";
            if (tags == null || tags.Count < 2) return work;

            int[] pairs = Markup.Pairs(tags);
            while (true)
            {
                if (!TokenAtStart(work, out int first, out int firstLength)) break;
                if (!TokenBefore(work, work.Length, out int last, out int lastStart)) break;
                if (lastStart < firstLength || last >= pairs.Length || pairs[last] != first) break;

                string inner = work.Substring(firstLength, lastStart - firstLength);
                string innerTrimmed = inner.Trim();
                if (innerTrimmed.Length == 0) break;

                int lead = inner.Length - inner.TrimStart().Length;
                int trail = inner.Length - inner.TrimEnd().Length;
                opening += work.Substring(0, firstLength) + inner.Substring(0, lead);
                closing = inner.Substring(inner.Length - trail) + work.Substring(lastStart) + closing;
                work = innerTrimmed;
            }
            return work;
        }

        /// <summary>The tag placeholder the text starts with, if it starts with one.</summary>
        private static bool TokenAtStart(string text, out int index, out int length)
        {
            index = -1;
            length = 0;
            if (!text.StartsWith(Markup.PlaceholderPrefix, System.StringComparison.Ordinal)) return false;
            int end = text.IndexOf(Markup.PlaceholderSuffix, Markup.PlaceholderPrefix.Length, System.StringComparison.Ordinal);
            if (end < 0) return false;
            if (!int.TryParse(text.Substring(Markup.PlaceholderPrefix.Length, end - Markup.PlaceholderPrefix.Length),
                              System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out index))
                return false;
            length = end + Markup.PlaceholderSuffix.Length;
            return true;
        }

        /// <summary>The tag placeholder ending exactly at <paramref name="end"/>, if one does.</summary>
        private static bool TokenBefore(string text, int end, out int index, out int start)
        {
            index = -1;
            start = text.LastIndexOf(Markup.PlaceholderPrefix, end - 1, System.StringComparison.Ordinal);
            if (start < 0) return false;
            return TokenAtStart(text.Substring(start, end - start), out index, out int length) && start + length == end;
        }
    }
}
