using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>One pixel of a flag, already resolved to a colour.</summary>
    public struct FlagPixel
    {
        public int X;
        public int Y;

        /// <summary>0xRRGGBB. Meaningless when <see cref="Transparent"/>.</summary>
        public int Rgb;

        /// <summary>Outside the flag — the one shape that is not a rectangle.</summary>
        public bool Transparent;
    }

    /// <summary>
    /// The flags, drawn by us as indexed pixels so this project depends on no icon licence.
    ///
    /// ⚠ **A national flag is not a copyrighted work** — it is an official symbol. What the usual
    /// icon sets license is their SVG artwork. These are ours, and deliberately NOT faithful: at
    /// sixteen pixels wide an emblem is a patch of the right colour in the right place. That is
    /// also what keeps the few protected coats of arms out of it.
    ///
    /// 🔴 **A flag names a language here, not a country, and it cannot always do it.** Ten Indian
    /// languages share one flag because no Indian state has one of its own; bokmål and nynorsk are
    /// two written standards of the same country. Those cases get the flag AND the language tag
    /// beside it — see <see cref="SharedBySeveral"/>, which decides it from the data rather than
    /// from a list somebody has to maintain.
    ///
    /// ⚠ **Where a community has two flags, this carries the official one.** The Catalan senyera,
    /// never the estelada. The Belarusian state flag, never the white-red-white one — displaying
    /// that is prosecuted in Belarus, so the choice protects the user rather than expressing an
    /// opinion. Anybody adding a flag inherits that rule.
    /// </summary>
    public static partial class Flags
    {
        /// <summary>
        /// The flag standing for a language, by its catalogue name, or null.
        ///
        /// ⚠ Null is ordinary, not a failure: a language whose flag has not been drawn yet renders
        /// its tag alone, which is legible and says the truth.
        /// </summary>
        public static string For(string languageName)
        {
            if (string.IsNullOrEmpty(languageName)) return null;

            foreach (var row in CatalogueFlagsOfLanguages)
                if (row[0] == languageName) return row[1];

            return null;
        }

        /// <summary>
        /// Does this flag stand for more than one language of the catalogue?
        ///
        /// 🔴 **The reason the tag chip exists, and the reason it is not a hand-written list.** Ten
        /// languages carry the Indian flag; two carry the Norwegian one. A control whose whole job
        /// is to tell languages apart cannot show the same picture for ten of them, so those get
        /// their tag beside it. Deriving it here means adding an eleventh Indian language makes the
        /// chips appear on all of them, with nobody having to remember why.
        /// </summary>
        public static bool SharedBySeveral(string flagId)
        {
            if (string.IsNullOrEmpty(flagId)) return false;

            var seen = 0;
            foreach (var row in CatalogueFlagsOfLanguages)
            {
                if (row[1] != flagId) continue;
                seen++;
                if (seen > 1) return true;
            }

            return false;
        }

        /// <summary>What to show for one language: a flag, a tag, or both.</summary>
        public struct LanguageMark
        {
            /// <summary>The flag to draw, or null when none has been drawn for this language.</summary>
            public string Flag;

            /// <summary>The language's own code. Null only when the language is unknown.</summary>
            public string Tag;

            /// <summary>
            /// Show the tag beside the flag. Always true when there is no flag — something has to
            /// name the language.
            /// </summary>
            public bool ShowTag;
        }

        /// <summary>
        /// How a language is marked, decided once for the three products.
        ///
        /// 🔴 **One call rather than three.** A renderer that asked <see cref="For"/>, then
        /// <see cref="SharedBySeveral"/>, then the language's code, would be re-deciding the rule
        /// each time — and the third product to be written would decide it slightly differently.
        /// The RULE is here; drawing a flag beside a chip stays each product's business.
        /// </summary>
        /// <param name="nameIsWritten">
        /// The caller is also writing the language's NAME beside this mark.
        ///
        /// 🔴 **Then the chip is noise.** It exists for one reason — a flag that cannot say which
        /// language this is, because ten share it or none was drawn — and a written name answers
        /// that completely. "🇮🇳 hi Hindi" says the same thing twice and reads as a bug.
        /// </param>
        public static LanguageMark Mark(string languageName, bool nameIsWritten)
        {
            var flag = For(languageName);

            return new LanguageMark
            {
                Flag = flag,
                Tag = Languages.CodeOf(languageName),

                // No flag means the tag is the only thing naming this language; a shared flag means
                // it names ten of them at once. Both need the chip, for the same reason — unless
                // the name is right there, which settles it better than either.
                ShowTag = !nameIsWritten && (flag == null || SharedBySeveral(flag)),
            };
        }

        /// <summary>The mark on its own, with nothing else naming the language.</summary>
        public static LanguageMark Mark(string languageName)
        {
            return Mark(languageName, nameIsWritten: false);
        }

        /// <summary>Every flag drawn so far, for a renderer that wants to warm a cache.</summary>
        public static IEnumerable<string> Known()
        {
            foreach (var row in CataloguePixels) yield return row[0];
        }

        /// <summary>
        /// The pixels of one flag, left to right then top to bottom, or null when it is unknown.
        ///
        /// ⚠ Resolved to colours HERE rather than handing out the palette and the rows. Three
        /// products would otherwise each write the same lookup — and the one that got it wrong
        /// would draw a flag in somebody else's colours without anything failing.
        /// </summary>
        public static FlagPixel[] Pixels(string flagId)
        {
            if (string.IsNullOrEmpty(flagId)) return null;

            string[] rows = null;
            foreach (var row in CataloguePixels)
                if (row[0] == flagId) { rows = row; break; }

            if (rows == null) return null;

            var palette = PaletteOf(flagId);
            if (palette == null) return null;

            var pixels = new FlagPixel[Width * Height];
            var at = 0;

            for (var y = 0; y < Height; y++)
            {
                // Row 0 of the table is the id, so the rows themselves start at 1.
                var line = rows[y + 1];

                for (var x = 0; x < Width; x++)
                {
                    var key = line[x];
                    int rgb;

                    pixels[at++] = key == '.' || !palette.TryGetValue(key, out rgb)
                        ? new FlagPixel { X = x, Y = y, Transparent = true }
                        : new FlagPixel { X = x, Y = y, Rgb = rgb };
                }
            }

            return pixels;
        }

        private static Dictionary<char, int> PaletteOf(string flagId)
        {
            foreach (var row in CataloguePalettes)
            {
                if (row[0] != flagId) continue;

                var palette = new Dictionary<char, int>();
                for (var i = 1; i + 1 < row.Length; i += 2)
                    palette[row[i][0]] = ParseHex(row[i + 1]);

                return palette;
            }

            return null;
        }

        /// <summary>
        /// "#RRGGBB" to 0xRRGGBB.
        ///
        /// ⚠ Written here rather than taken from <see cref="Rgb"/>: the palette comes from a
        /// generated table this library controls end to end, and the check verifies this reading
        /// against the catalogue's own text.
        /// </summary>
        private static int ParseHex(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;

            var packed = 0;
            foreach (var c in value)
            {
                int digit;
                if (c >= '0' && c <= '9') digit = c - '0';
                else if (c >= 'a' && c <= 'f') digit = c - 'a' + 10;
                else if (c >= 'A' && c <= 'F') digit = c - 'A' + 10;
                else continue;

                packed = (packed << 4) | digit;
            }

            return packed;
        }
    }
}
