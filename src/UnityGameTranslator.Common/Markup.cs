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

        /// <summary>
        /// Any placeholder a lifted tag becomes: &lt;color1&gt;, &lt;/color1&gt;, &lt;br2/&gt; — see
        /// <see cref="Tokens"/>.
        /// </summary>
        private static readonly Regex TokenPattern = new Regex(@"</?[a-z][a-z0-9-]*\d+/?>", RegexOptions.Compiled);

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
        /// Replace every tag with its placeholder (<see cref="Tokens"/>). The tags come back in
        /// <paramref name="tags"/>, in the order they stood.
        /// </summary>
        public static string Extract(string text, out List<string> tags)
        {
            tags = new List<string>();
            if (string.IsNullOrEmpty(text)) return text;

            var matches = TagPattern.Matches(text);
            if (matches.Count == 0) return text;

            foreach (Match match in matches) tags.Add(match.Value);
            List<string> tokens = Tokens(tags);

            var result = new StringBuilder(text.Length);
            int lastIndex = 0;
            int i = 0;
            foreach (Match match in matches)
            {
                result.Append(text, lastIndex, match.Index - lastIndex);
                result.Append(tokens[i++]);
                lastIndex = match.Index + match.Length;
            }

            result.Append(text, lastIndex, text.Length - lastIndex);
            return result.ToString();
        }

        /// <summary>
        /// The placeholder each tag travels as, in the order of <paramref name="tags"/>: a pair
        /// as &lt;color1&gt;…&lt;/color1&gt; — the tag's own name and ONE number for both ends — and a
        /// tag with no partner as &lt;br2/&gt;.
        ///
        /// 🔴 **Written like the markup a model already knows, measured** (2026-09-26, nine
        /// models, French from Chinese and English, first attempt): opaque [!t*0]…[!t*1] kept a
        /// colour on the right words in 54 answers of 90; &lt;color1&gt;…&lt;/color1&gt; in 66, and
        /// most on the hardest shape, a title the translation moves in front of a name (23 → 32
        /// of 45). [!t*0] reads as a position to keep, like [!nl]; &lt;color1&gt; reads as a colour
        /// that opens and closes around words. The same number on both ends ([!t*0]…[/!t*0])
        /// did worse than either. Detail: analyse/balises-ia.md.
        ///
        /// ⚠ The number is what ties the placeholder to its tag on the way back — the attribute
        /// (#FD1430) never travels. A name ending in a digit gets a hyphen (&lt;h1-3&gt;) so the
        /// number stays readable, and a tag with no name travels as "tag".
        /// </summary>
        public static List<string> Tokens(IList<string> tags)
        {
            var tokens = new List<string>(tags.Count);
            int[] pairs = Pairs(tags);
            var closedBy = new int[tags.Count];
            for (int i = 0; i < closedBy.Length; i++) closedBy[i] = -1;
            for (int close = 0; close < pairs.Length; close++)
                if (pairs[close] >= 0) closedBy[pairs[close]] = close;

            var numberOf = new int[tags.Count];
            int next = 1;
            for (int i = 0; i < tags.Count; i++)
            {
                string name = TokenName(tags[i]);
                if (IsClosing(tags[i]) && pairs[i] >= 0)
                {
                    tokens.Add("</" + name + numberOf[pairs[i]] + ">");
                }
                else if (!IsClosing(tags[i]) && closedBy[i] >= 0)
                {
                    numberOf[i] = next++;
                    tokens.Add("<" + name + numberOf[i] + ">");
                }
                else
                {
                    tokens.Add("<" + name + (next++) + "/>");
                }
            }
            return tokens;
        }

        private static string TokenName(string tag)
        {
            var name = new StringBuilder();
            foreach (char c in NameOf(tag))
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-') name.Append(c);
            if (name.Length == 0 || !(name[0] >= 'a' && name[0] <= 'z')) name.Insert(0, "tag");
            if (char.IsDigit(name[name.Length - 1])) name.Append('-');
            return name.ToString();
        }

        /// <summary>Whether a text carries any tag placeholder.</summary>
        public static bool HasTokens(string text) => !string.IsNullOrEmpty(text) && TokenPattern.IsMatch(text);

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
        /// The tag placeholders of the source an answer does not carry exactly as many times, as
        /// lines a model can act on. The slot placeholders ([!v*0], [!nl]…) are
        /// <see cref="Placeholders"/>' business; these are the tags', written differently on
        /// purpose (<see cref="Tokens"/>).
        /// </summary>
        public static List<string> Miscounted(string source, string answer, IList<string>? tags)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(source) || tags == null || tags.Count == 0) return errors;
            foreach (string token in Tokens(tags))
            {
                int expected = Occurrences(source, token);
                if (expected == 0) continue;
                int found = Occurrences(answer ?? "", token);
                if (found != expected)
                    errors.Add($"tag {token} appears {found} time(s) instead of {expected}");
            }
            return errors;
        }

        private static int Occurrences(string text, string token)
        {
            int count = 0, at = 0;
            while ((at = text.IndexOf(token, at, System.StringComparison.Ordinal)) >= 0) { count++; at += token.Length; }
            return count;
        }

        /// <summary>
        /// The closing placeholders an answer puts before the opening one they close, as lines a
        /// model can act on. Empty when every pair of the source comes back open-then-close.
        ///
        /// 🔴 **Counting cannot see this.** An answer "&lt;/color1&gt;texte&lt;color1&gt;" carries
        /// each placeholder once; restored, the game got "&lt;/color&gt;texte&lt;color=…&gt;" and
        /// coloured the wrong part or nothing. A model writing right to left is the likeliest to
        /// do it.
        ///
        /// ⚠ Only ORDER within a pair is judged. Where a styled span goes in the sentence, and
        /// which of two spans comes first, is the language's business.
        /// </summary>
        public static List<string> OutOfOrder(string answer, IList<string>? tags)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(answer) || tags == null || tags.Count == 0) return errors;

            int[] pairs = Pairs(tags);
            List<string> tokens = Tokens(tags);
            for (int close = 0; close < pairs.Length; close++)
            {
                int open = pairs[close];
                if (open < 0) continue;

                string opening = tokens[open];
                string closing = tokens[close];
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
        /// that is not there at all is <see cref="Miscounted"/>'s to report.
        ///
        /// 🔴 **Counting and ordering cannot see this.** "仙霞派&lt;color&gt;外门弟子&lt;/color&gt;"
        /// came back with both placeholders side by side at the end: each once, open before
        /// close — accepted, and the game got an empty colour (2026-09-26). What sits between is
        /// compared as content, never as language: anything but spacing, other tags left out.
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
            List<string> tokens = Tokens(tags);
            for (int close = 0; close < pairs.Length; close++)
            {
                int open = pairs[close];
                if (open < 0) continue;

                string opening = tokens[open];
                string closing = tokens[close];
                if (string.IsNullOrEmpty(Between(source, opening, closing))) continue;
                if (Between(answer, opening, closing) == "")
                    errors.Add($"{opening} and {closing} surround words in the source: put the translation of those words between them");
            }

            return errors;
        }

        /// <summary>
        /// The markup an answer carries that was never sent — every tag is lifted out before a
        /// text leaves (<see cref="Extract"/>), so any tag in the answer that is not one of the
        /// placeholders handed over is the model's own.
        ///
        /// ⚠ A model that writes real markup — "&lt;b&gt;Chef&lt;/b&gt;", or a placeholder of its own
        /// like &lt;color2&gt; — would put tags nobody wrote into the game. Read with the very pattern
        /// that lifts tags, so what counts as a tag here is what counted as one on the way out.
        /// </summary>
        public static List<string> Invented(string answer, IList<string>? tags)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(answer)) return errors;
            var ours = new HashSet<string>(tags == null ? new List<string>() : Tokens(tags));
            foreach (Match match in TagPattern.Matches(answer))
                if (!ours.Contains(match.Value))
                    errors.Add($"{match.Value} is not in the source: write no tag of your own");
            return errors;
        }

        /// <summary>
        /// Whether some pair of tag placeholders in this text encloses something — the case where
        /// a model has to be told that tags come in pairs and what sits between them stays there.
        /// Read from the placeholders alone: a pair is the same name and number, opened and closed.
        /// </summary>
        public static bool HasFilledPair(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (Match open in TokenPattern.Matches(text))
            {
                string token = open.Value;
                if (token[1] == '/' || token.EndsWith("/>", System.StringComparison.Ordinal)) continue;
                string closing = "</" + token.Substring(1);
                if (!string.IsNullOrEmpty(Between(text, token, closing))) return true;
            }
            return false;
        }

        /// <summary>
        /// What stands between the two tokens, taken in order — other tag placeholders left out,
        /// spacing trimmed — or null when either is missing or they come the wrong way round.
        /// </summary>
        private static string? Between(string text, string opening, string closing)
        {
            int at = text.IndexOf(opening, System.StringComparison.Ordinal);
            if (at < 0) return null;
            int from = at + opening.Length;
            int end = text.IndexOf(closing, from, System.StringComparison.Ordinal);
            if (end < 0) return null;
            return TokenPattern.Replace(text.Substring(from, end - from), "").Trim();
        }

        /// <summary>
        /// An answer with every plain closing tag the model wrote — &lt;/color&gt; — tied back to the
        /// placeholder it closes, &lt;/color1&gt;: the innermost one of that name still open, the way
        /// any HTML parser reads it. Anything it cannot tie is left as it is, for
        /// <see cref="Invented"/> to refuse.
        ///
        /// 🔴 **Measured, not assumed** (2026-09-26, bench of nine models): given
        /// &lt;color1&gt;Warning&lt;/color1&gt;, small models answered
        /// "&lt;color1&gt;Avertissement&lt;/color&gt;" — the pair understood and placed right, the
        /// number dropped from the closing end as real HTML writes it. Refused, those lines stayed
        /// untranslated. There is nothing to guess: a plain closing tag has exactly one reading.
        /// Nothing a game wrote can be touched either — every tag is lifted out before a line is
        /// sent, so a plain &lt;/color&gt; in an answer can only be the model's.
        /// </summary>
        public static string CloseUnnumbered(string answer, IList<string>? tags)
        {
            if (string.IsNullOrEmpty(answer) || tags == null || tags.Count == 0 || answer.IndexOf("</", System.StringComparison.Ordinal) < 0)
                return answer;

            var ours = new HashSet<string>(Tokens(tags));
            var open = new List<string>();   // opening placeholders still open, innermost last
            var result = new StringBuilder(answer.Length);
            int last = 0;

            foreach (Match match in TagPattern.Matches(answer))
            {
                string tag = match.Value;
                string replacement = tag;

                if (ours.Contains(tag))
                {
                    if (tag[1] == '/')
                    {
                        string opening = "<" + tag.Substring(2);
                        int at = open.LastIndexOf(opening);
                        if (at >= 0) open.RemoveAt(at);
                    }
                    else if (!tag.EndsWith("/>", System.StringComparison.Ordinal))
                    {
                        open.Add(tag);
                    }
                }
                else if (tag.Length > 3 && tag[1] == '/' && tag.IndexOf(' ') < 0)
                {
                    string name = tag.Substring(2, tag.Length - 3).ToLowerInvariant();
                    for (int i = open.Count - 1; i >= 0; i--)
                    {
                        string opening = open[i];
                        string openName = opening.Substring(1, opening.Length - 2).TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9').TrimEnd('-');
                        if (openName != name) continue;
                        string closing = "</" + opening.Substring(1);
                        if (ours.Contains(closing))
                        {
                            replacement = closing;
                            open.RemoveAt(i);
                        }
                        break;
                    }
                }

                result.Append(answer, last, match.Index - last).Append(replacement);
                last = match.Index + match.Length;
            }

            result.Append(answer, last, answer.Length - last);
            return result.ToString();
        }

        /// <summary>Put each placeholder back as the tag it stood for.</summary>
        public static string Restore(string text, List<string>? tags)
        {
            if (string.IsNullOrEmpty(text) || tags == null || tags.Count == 0) return text;

            List<string> tokens = Tokens(tags);
            string result = text;
            for (int i = 0; i < tags.Count; i++)
                result = result.Replace(tokens[i], tags[i]);
            return result;
        }
    }
}
