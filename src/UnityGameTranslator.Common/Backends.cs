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
            var prepared = new PreparedText { Leading = "", Trailing = "", Tags = new List<string>() };

            if (string.IsNullOrEmpty(text))
            {
                prepared.ToSend = text;
                prepared.NothingToSend = true;
                return prepared;
            }

            // 1. Line breaks → [!nl], so the answer has to give the shape back rather than
            //    reflowing it. Before the trim, deliberately — see the note on order above.
            string work = text.Replace("\n", LineBreak);

            // 2. Markup tags → [!t*N]. The model never sees markup it could translate, reorder or
            //    invent.
            work = Markup.Extract(work, out List<string> tags);
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

            prepared.ToSend = work;
            prepared.NothingToSend = string.IsNullOrWhiteSpace(work);
            return prepared;
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

            string result = Markup.Restore(answer, prepared.Tags);
            result = result.Replace(LineBreak, "\n");

            if (prepared.Leading.Length > 0 || prepared.Trailing.Length > 0)
                result = prepared.Leading + result + prepared.Trailing;

            return result;
        }
    }
}
