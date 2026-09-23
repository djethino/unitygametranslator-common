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
