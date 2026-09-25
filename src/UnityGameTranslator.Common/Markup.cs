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
