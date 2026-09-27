using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// The markup a game wraps around its text (&lt;color=…&gt;, &lt;/b&gt;), lifted out before a
    /// backend sees it and put back afterwards.
    ///
    /// ⚠ In the socle because <see cref="Backends"/> needs it, and <see cref="Backends"/> is what a
    /// line goes through whoever translates it — the mod inside a game, the Manager answering the
    /// browser editor while the game is closed. Moved from the mod's TextNormalization on
    /// 2026-09-23 with no change of behaviour.
    /// </summary>
    public static class Markup
    {
        /// <summary>Any XML/HTML-like tag: &lt;tag&gt;, &lt;/tag&gt;, &lt;tag attr="val"&gt;, &lt;tag/&gt;.</summary>
        private static readonly Regex TagPattern = new Regex(@"<[^>]+>", RegexOptions.Compiled);

        /// <summary>What a lifted tag becomes: [!t*0], [!t*1]…</summary>
        public const string PlaceholderPrefix = "[!t*";

        /// <inheritdoc cref="PlaceholderPrefix"/>
        public const string PlaceholderSuffix = "]";

        /// <summary>
        /// Remove every tag — for comparisons against raw values (games wrap a typed value in colour
        /// tags, for instance).
        /// </summary>
        public static string Strip(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return TagPattern.Replace(text, "");
        }

        /// <summary>
        /// Replace every tag with a numbered placeholder. The tags come back in
        /// <paramref name="tags"/>, in the order their placeholders are numbered.
        /// </summary>
        public static string Extract(string text, out List<string> tags)
        {
            tags = new List<string>();
            if (string.IsNullOrEmpty(text)) return text;

            var matches = TagPattern.Matches(text);
            if (matches.Count == 0) return text;

            var result = new StringBuilder(text.Length);
            int lastIndex = 0;

            foreach (Match match in matches)
            {
                result.Append(text, lastIndex, match.Index - lastIndex);
                int tagIndex = tags.Count;
                tags.Add(match.Value);
                result.Append(PlaceholderPrefix).Append(tagIndex).Append(PlaceholderSuffix);
                lastIndex = match.Index + match.Length;
            }

            result.Append(text, lastIndex, text.Length - lastIndex);
            return result.ToString();
        }

        /// <summary>Whether a tag closes one: &lt;/b&gt;, &lt;/color&gt;.</summary>
        public static bool IsClosing(string tag) => tag != null && tag.Length > 2 && tag[0] == '<' && tag[1] == '/';

        /// <summary>
        /// The name that pairs an opening tag with its closing one, lowercase: "color" for both
        /// &lt;color=red&gt; and &lt;/COLOR&gt;.
        ///
        /// ⚠ TextMesh Pro's short colour form &lt;#RRGGBB&gt; has no name and is closed by
        /// &lt;/color&gt;, so it is named "color" here. Left unnamed it was never paired, and a
        /// right-to-left line coloured one letter instead of its span.
        /// </summary>
        public static string NameOf(string tag)
        {
            if (string.IsNullOrEmpty(tag) || tag[0] != '<') return "";
            int from = IsClosing(tag) ? 2 : 1;
            if (from < tag.Length && tag[from] == '#') return "color";

            int end = from;
            while (end < tag.Length && (char.IsLetterOrDigit(tag[end]) || tag[end] == '-')) end++;
            return tag.Substring(from, end - from).ToLowerInvariant();
        }

        /// <summary>
        /// For each closing tag, the index of the opening tag it closes, or -1 — matched by name,
        /// innermost first, the way a rich-text parser reads them.
        /// </summary>
        public static int[] Pairs(IList<string> tags)
        {
            var closes = new int[tags.Count];
            var open = new List<int>();

            for (int i = 0; i < tags.Count; i++)
            {
                closes[i] = -1;
                if (!IsClosing(tags[i])) { open.Add(i); continue; }

                string name = NameOf(tags[i]);
                for (int s = open.Count - 1; s >= 0; s--)
                {
                    if (NameOf(tags[open[s]]) != name) continue;
                    closes[i] = open[s];
                    open.RemoveRange(s, open.Count - s);
                    break;
                }
            }

            return closes;
        }

        /// <summary>
        /// A span that runs to the very end of the text — the shape of a reveal by markup, where a
        /// game uncovers a finished sentence by moving the opening of one tag along it
        /// (`Can y&lt;color=#00000000&gt;ou help?&lt;/color&gt;`). Gives the opening tag's start and
        /// length, and where the closing tag starts; the closing tag ends the text.
        ///
        /// ⚠ Says nothing about what the tag DOES — it may hide, fade or colour. Whoever uses it
        /// copies the game's own tag; it is never interpreted here.
        /// </summary>
        public static bool TrailingSpan(string text, out int openStart, out int openLength, out int closeStart)
        {
            openStart = openLength = closeStart = -1;
            if (string.IsNullOrEmpty(text)) return false;

            var matches = TagPattern.Matches(text);
            if (matches.Count == 0) return false;
            var last = matches[matches.Count - 1];
            if (last.Index + last.Length != text.Length || !IsClosing(last.Value)) return false;

            var tags = new List<string>(matches.Count);
            foreach (Match m in matches) tags.Add(m.Value);
            int opening = Pairs(tags)[tags.Count - 1];
            if (opening < 0) return false;

            openStart = matches[opening].Index;
            openLength = matches[opening].Length;
            closeStart = last.Index;
            return true;
        }

        /// <summary>
        /// A translation shown at the same point of a reveal by markup as its source: the game's own
        /// opening tag placed after the same SHARE of visible characters, its closing tag at the end.
        /// <paramref name="shown"/> of <paramref name="total"/> visible source characters are
        /// uncovered; the translation uncovers the same share, rounded up, so the last character of
        /// the source uncovers the last of the translation. A ratio of two lengths, the same measure
        /// a reveal counted in characters is carried over with — no language, no tag interpreted.
        /// The opening tag never lands inside one of the translation's own tags.
        /// </summary>
        public static string RevealUnder(string translation, string open, string close, int shown, int total)
        {
            if (translation == null) return null;
            int visible = Strip(translation).Length;
            int keep = total <= 0 || shown >= total ? visible
                     : shown <= 0 ? 0
                     : (int)System.Math.Ceiling((double)shown * visible / total);
            if (keep > visible) keep = visible;

            // The raw index after `keep` visible characters, stepping over whole tags.
            int raw = 0, seen = 0;
            while (raw < translation.Length && seen < keep)
            {
                if (translation[raw] == '<')
                {
                    var tag = TagPattern.Match(translation, raw);
                    if (tag.Success && tag.Index == raw) { raw += tag.Length; continue; }
                }
                raw++;
                seen++;
            }
            return translation.Substring(0, raw) + open + translation.Substring(raw) + close;
        }

        /// <summary>
        /// The closing placeholders an answer puts before the opening one they close, as lines a
        /// model can act on. Empty when every pair of the source comes back open-then-close.
        ///
        /// 🔴 **The placeholder check counts tokens and cannot see this.** An answer
        /// "[!t*1]texte[!t*0]" carries each token once and passed; restored, the game got
        /// "&lt;/color&gt;texte&lt;color=…&gt;" and coloured the wrong part or nothing. A model
        /// writing right to left is the likeliest to do it.
        ///
        /// ⚠ Only ORDER within a pair is judged. Where a styled span goes in the sentence, and
        /// which of two spans comes first, is the language's business.
        /// </summary>
        public static List<string> OutOfOrder(string answer, IList<string>? tags)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(answer) || tags == null || tags.Count == 0) return errors;

            int[] pairs = Pairs(tags);
            for (int close = 0; close < pairs.Length; close++)
            {
                int open = pairs[close];
                if (open < 0) continue;

                string opening = PlaceholderPrefix + open + PlaceholderSuffix;
                string closing = PlaceholderPrefix + close + PlaceholderSuffix;
                int at = answer.IndexOf(opening, System.StringComparison.Ordinal);
                int end = answer.IndexOf(closing, System.StringComparison.Ordinal);

                if (at >= 0 && end >= 0 && end < at)
                    errors.Add($"{closing} closes {opening}, so it must come after it");
            }

            return errors;
        }

        /// <summary>
        /// The pairs that hold something in the source and nothing in the answer, as lines a
        /// model can act on. Empty when every pair that styled text still styles some. A pair
        /// that is not there at all is the placeholder check's to report.
        ///
        /// 🔴 **Counting and ordering cannot see this.** "仙霞派&lt;color&gt;外门弟子&lt;/color&gt;"
        /// came back as "…Xianxia [!t*0][!t*1]": each token once, open before close — accepted,
        /// and the game got an empty colour (2026-09-26). What sits between is compared as
        /// content, never as language: anything but spacing, other tags left out.
        ///
        /// ⚠ **The source words are NOT quoted back** (measured the same day): named in the
        /// correction, they were copied into the answer by small models — "…&lt;color&gt;掌门&lt;/color&gt;"
        /// in a French line.
        /// </summary>
        public static List<string> Emptied(string source, string answer, IList<string>? tags)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(answer) || tags == null || tags.Count == 0) return errors;

            int[] pairs = Pairs(tags);
            for (int close = 0; close < pairs.Length; close++)
            {
                int open = pairs[close];
                if (open < 0) continue;

                string opening = PlaceholderPrefix + open + PlaceholderSuffix;
                string closing = PlaceholderPrefix + close + PlaceholderSuffix;
                if (string.IsNullOrEmpty(Between(source, opening, closing))) continue;
                if (Between(answer, opening, closing) == "")
                    errors.Add($"{opening} and {closing} surround words in the source: put the translation of those words between them");
            }

            return errors;
        }

        /// <summary>
        /// The markup an answer carries that was never sent — every tag is lifted out before a
        /// text leaves (<see cref="Extract"/>), so any tag in the answer is the model's own.
        ///
        /// ⚠ A model that writes real markup — "&lt;b&gt;Chef&lt;/b&gt;" for a pair it was handed as
        /// placeholders — would put tags nobody wrote into the game. Read with the very pattern
        /// that lifts tags, so what counts as a tag here is what counted as one on the way out.
        /// </summary>
        public static List<string> Invented(string answer)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(answer)) return errors;
            foreach (Match match in TagPattern.Matches(answer))
                errors.Add($"{match.Value} is not in the source: write no tag of your own, only the placeholders");
            return errors;
        }

        /// <summary>
        /// Whether some pair of tags in this text encloses something — the case where a model has
        /// to be told that tags come in pairs and what sits between them stays between them.
        /// </summary>
        public static bool HasFilledPair(string text, IList<string>? tags)
        {
            if (string.IsNullOrEmpty(text) || tags == null || tags.Count < 2) return false;
            int[] pairs = Pairs(tags);
            for (int close = 0; close < pairs.Length; close++)
            {
                int open = pairs[close];
                if (open < 0) continue;
                if (!string.IsNullOrEmpty(Between(text, PlaceholderPrefix + open + PlaceholderSuffix,
                                                  PlaceholderPrefix + close + PlaceholderSuffix)))
                    return true;
            }
            return false;
        }

        private static readonly Regex TagToken = new Regex(@"\[!t\*\d+\]", RegexOptions.Compiled);

        /// <summary>
        /// What stands between the two tokens, taken in order — other tags left out, spacing
        /// trimmed — or null when either is missing or they come the wrong way round.
        /// </summary>
        private static string? Between(string text, string opening, string closing)
        {
            int at = text.IndexOf(opening, System.StringComparison.Ordinal);
            if (at < 0) return null;
            int from = at + opening.Length;
            int end = text.IndexOf(closing, from, System.StringComparison.Ordinal);
            if (end < 0) return null;
            return TagToken.Replace(text.Substring(from, end - from), "").Trim();
        }

        /// <summary>Put each placeholder back as the tag it stood for.</summary>
        public static string Restore(string text, List<string>? tags)
        {
            if (string.IsNullOrEmpty(text) || tags == null || tags.Count == 0) return text;

            string result = text;
            for (int i = 0; i < tags.Count; i++)
                result = result.Replace(PlaceholderPrefix + i + PlaceholderSuffix, tags[i]);
            return result;
        }
    }
}
