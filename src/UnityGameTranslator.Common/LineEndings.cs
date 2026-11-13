using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// The rule for line endings, written once: **every text of a translation file is LF only, and
    /// every reader makes it so BEFORE anything else** — before keys are compared, before a line is
    /// counted, before the content hash. Nobody normalises afterwards, so a CR that survived would
    /// make two hashes for one text.
    ///
    /// CRLF becomes LF, then a lone CR becomes LF. Nothing else is a line ending here: U+0085,
    /// U+2028 and U+2029 are characters of the game's text and stay as they are (the hash vectors
    /// freeze that too).
    ///
    /// It used to exist three times (the mod's <c>TextNormalization</c>, the site's <c>toLf</c>, a
    /// copy in the Manager); a port takes its answers from corpus/rules/line_endings.json.
    /// </summary>
    public static class LineEndings
    {
        /// <summary>
        /// The text with LF alone. Null stays null, a text with no CR is returned as the very same
        /// string (no copy, nothing allocated).
        /// </summary>
        public static string? ToLf(string? text)
        {
            if (text == null || text.IndexOf('\r') < 0) return text;
            return text.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        /// <summary>
        /// A line with LF alone in EVERY text it holds — its translation, and the texts of the
        /// format 2 (written forms, the <c>was</c>, <c>to</c> and <c>own</c> of its links, the refused
        /// entries). The answer for a file that comes from elsewhere holding a CR in one of them:
        /// the same as for <c>v</c>, never a refusal. (A writer of this library still never PRODUCES
        /// one: <see cref="LineWrites.CarriageReturn"/>.) A line that needed nothing is returned as it is.
        /// </summary>
        public static TranslationLine Of(TranslationLine line)
        {
            var result = line;
            bool changed = false;

            result.Value = Fix(line.Value, ref changed)!;
            result.Forms = FixMap(line.Forms, ref changed);
            result.Class = line.Class;

            if (line.Links != null && line.Links.Count > 0)
            {
                bool linksChanged = false;
                var links = new List<LineLink>(line.Links.Count);
                foreach (var link in line.Links)
                {
                    var l = link;
                    l.To = Fix(link.To, ref linksChanged);
                    l.Was = Fix(link.Was, ref linksChanged);
                    l.Own = Fix(link.Own, ref linksChanged);
                    links.Add(l);
                }
                if (linksChanged) { result.Links = links; changed = true; }
            }

            if (line.Unlinked != null && line.Unlinked.Count > 0)
            {
                bool listChanged = false;
                var keys = new List<string>(line.Unlinked.Count);
                foreach (string key in line.Unlinked) keys.Add(Fix(key, ref listChanged)!);
                if (listChanged) { result.Unlinked = keys; changed = true; }
            }

            return changed ? result : line;
        }

        private static string? Fix(string? text, ref bool changed)
        {
            string? fixedText = ToLf(text);
            if (!ReferenceEquals(fixedText, text)) changed = true;
            return fixedText;
        }

        private static IReadOnlyList<KeyValuePair<string, string>>? FixMap(IReadOnlyList<KeyValuePair<string, string>>? map, ref bool changed)
        {
            if (map == null || map.Count == 0) return map;
            bool mapChanged = false;
            var copy = new List<KeyValuePair<string, string>>(map.Count);
            foreach (var pair in map) copy.Add(new KeyValuePair<string, string>(pair.Key, Fix(pair.Value, ref mapChanged)!));
            if (!mapChanged) return map;
            changed = true;
            return copy;
        }
    }
}
