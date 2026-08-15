using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The flags we draw ourselves.
    ///
    /// ⚠ These check the RULES, not the drawings. Whether France's blue is the right blue is a
    /// question for an eye; whether every flag fills its grid, whether a language that shares its
    /// flag with another gets its tag beside it, and whether the two generated tables agree with
    /// each other are questions a machine can answer and a person will not, ninety times.
    /// </summary>
    internal static class FlagChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            check(Flags.Width > 0 && Flags.Height > 0,
                "the grid has a size",
                "every renderer lays pixels out from these two numbers");

            var known = new List<string>(Flags.Known());
            check(known.Count > 0, "some flags are drawn", "an empty table renders nothing anywhere");

            // ── Every flag fills its grid, in colours that resolve ────────────
            foreach (var id in known)
            {
                var pixels = Flags.Pixels(id);

                check(pixels != null && pixels.Length == Flags.Width * Flags.Height,
                    id + " fills the grid exactly",
                    "a short row would shift every pixel after it and draw a different flag");

                if (pixels == null) continue;

                var opaque = 0;
                var colours = new HashSet<int>();
                foreach (var pixel in pixels)
                {
                    if (pixel.Transparent) continue;
                    opaque++;
                    colours.Add(pixel.Rgb);
                }

                check(opaque > 0, id + " is not entirely transparent",
                    "a flag nobody can see is a flag nobody has");

                // ⚠ The one mistake a generator cannot catch: a palette whose keys all resolve to
                // the same colour, or a row typed with one letter throughout. Both compile, both
                // pass every other check, and both draw a coloured rectangle.
                check(colours.Count > 1, id + " has more than one colour",
                    "a single flat colour is a typo that survives every other check");
            }

            // ── An unknown flag says so rather than inventing one ─────────────
            check(Flags.Pixels("this-is-not-a-flag") == null,
                "an unknown flag returns nothing",
                "a placeholder flag would name a country at random");

            check(Flags.For(null) == null && Flags.For("Klingon") == null,
                "and a language with no flag returns nothing",
                "not having drawn one yet is ordinary — it renders as the tag alone");

            // ── The two tables have to agree ──────────────────────────────────
            //
            // 🔴 They come from two different files: the mapping from languages.json, the drawings
            // from flags.json. A flag renamed in one and not the other compiles perfectly and
            // renders nothing, for that language only, on every product at once.
            var drawn = new HashSet<string>(known);
            foreach (var name in Languages.Names())
            {
                var flag = Flags.For(name);
                if (flag == null) continue;

                check(drawn.Contains(flag),
                    name + " points at a flag that exists",
                    "the mapping and the drawings come from two files and must not drift apart");
            }

            // ── The tag chip is decided from the data ─────────────────────────
            //
            // 🔴 The case the whole rule exists for: ten Indian languages carry one flag because no
            // Indian state has a flag of its own, and two Norwegians share theirs because they are
            // two written standards of one country. A control meant to tell languages apart cannot
            // show them the same picture and stop there.
            var counts = new Dictionary<string, int>();
            foreach (var name in Languages.Names())
            {
                var flag = Flags.For(name);
                if (flag == null) continue;

                int seen;
                counts[flag] = counts.TryGetValue(flag, out seen) ? seen + 1 : 1;
            }

            foreach (var pair in counts)
            {
                check(Flags.SharedBySeveral(pair.Key) == (pair.Value > 1),
                    pair.Key + (pair.Value > 1 ? " is shared, so its languages show their tag"
                                               : " stands for one language and needs no tag"),
                    "derived from the catalogue, never from a list somebody has to maintain");
            }

            check(!Flags.SharedBySeveral(null) && !Flags.SharedBySeveral("nothing"),
                "and nothing is shared by an absent flag",
                "a missing flag must not turn every tag chip on");

            // ── What a renderer actually asks for ─────────────────────────────
            foreach (var name in Languages.Names())
            {
                var mark = Flags.Mark(name);

                // 🔴 Something must always name the language. A row with neither flag nor tag is a
                // language nobody can identify, which is the one outcome this whole thing exists
                // to prevent.
                check(mark.Flag != null || mark.ShowTag,
                    name + " is named by something",
                    "no flag and no tag would leave a row saying nothing at all");

                if (mark.Flag == null)
                    check(mark.ShowTag, name + " with no flag shows its tag",
                        "the tag is then the only thing naming it");
            }

            var french = Flags.Mark("French");
            check(french.Flag == "fr" && !french.ShowTag && french.Tag == "fr",
                "a language with a flag of its own shows the flag alone",
                "a chip beside every flag would be noise on eighty of them");
        }
    }
}
