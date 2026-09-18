using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The five bands of the quality bar, and the words that name them.
    ///
    /// ⚠ These cases exist because this exact drift has happened three times. They check the two
    /// things a second copy always gets wrong: the ORDER, and whether the word matches the one the
    /// website ships in nineteen languages.
    /// </summary>
    internal static class CompositionChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            var bands = Composition.Bands();

            check(bands.Length == 5, "there are five bands and no more",
                "a sixth would have no colour and no denominator");

            // ── The order IS the rule ─────────────────────────────────────────
            //
            // Settled first, still-to-do last, so the grey ends the bar and its length reads as the
            // work left without any arithmetic. Reordering silently rewrites every bar in three
            // products at once.
            check(bands[0] == TagBand.Human && bands[1] == TagBand.Validated
                  && bands[2] == TagBand.Machine && bands[3] == TagBand.Skipped
                  && bands[4] == TagBand.Captured,
                "and they come in the bar's order, grey last",
                "the length of the tail is what says how much is left");

            // ── The words the website ships ───────────────────────────────────
            //
            // Taken from lang/en.json (progress.human, .validated, .ai, .skipped, .capture). They
            // cannot be read from here — the site is PHP — so they are written out, which is
            // exactly what makes a divergence visible instead of merely possible.
            check(Composition.Name(TagBand.Human) == "Human", "Human", "progress.human");
            check(Composition.Name(TagBand.Validated) == "Validated", "Validated", "progress.validated");
            check(Composition.Name(TagBand.Machine) == "AI", "AI", "progress.ai");
            check(Composition.Name(TagBand.Skipped) == "Kept as is", "Kept as is", "progress.skipped");
            check(Composition.Name(TagBand.Captured) == "Captured", "Captured", "progress.capture");

            // ── The letters, and the one that must stay empty ─────────────────
            check(Composition.Letter(TagBand.Human) == "H"
                  && Composition.Letter(TagBand.Validated) == "V"
                  && Composition.Letter(TagBand.Machine) == "A"
                  && Composition.Letter(TagBand.Skipped) == "S",
                "the four tags carry the letters the editors show",
                "a fifth vocabulary for a fact that already has one");

            check(Composition.Letter(TagBand.Captured).Length == 0,
                "and a captured line carries none",
                "it holds no tag: inventing one teaches a tag the file does not contain");

            // ── One entry carries BOTH figures ────────────────────────────────
            //
            // 🔴 The bar draws the proportion, so the percentage repeats the picture and the count
            // says what the picture cannot — a fifth of fifty lines or of six thousand. Dropping
            // either one was the state each product was in, in opposite directions.
            var entry = Composition.Entry(TagBand.Human, 1068, 20);

            check(entry.Contains("1,068"), "an entry says how many lines",
                "20% of fifty and 20% of six thousand are not the same proposition");

            check(entry.Contains("20%"), "and what share of the file that is",
                "a count alone does not place the band against the rest");

            check(entry.Contains(Composition.Name(TagBand.Human)), "under the band's own name",
                "a figure with no name is a figure nobody can act on");

            // ⚠ Invariant grouping. The running culture is whatever the machine carries, and the
            // same file would print 1,068 for one player and 1.068 for another — for a figure the
            // website prints one way for everybody.
            check(Composition.Amount(1068) == "1,068" && Composition.Amount(999) == "999",
                "counts are grouped the way the website groups them",
                "a separator that follows the machine is invisible until a screenshot");

            // ── Each band explains itself ─────────────────────────────────────
            foreach (var band in bands)
            {
                check(Composition.Name(band).Trim().Length > 0 && Composition.Effect(band).Trim().Length > 0,
                    band + " is named and explained",
                    "a coloured band nobody can name is a decoration");
            }

            // ── A count carries the word for what it counts ───────────────────
            //
            // 🔴 The alternative every product had reached for was "1 line(s)", which nobody says
            // out loud and which a reader in their fourth language has to decode. Both words are
            // given because English has no rule a machine can apply.
            check(Composition.Amount(1, "line", "lines") == "1 line"
                  && Composition.Amount(2, "line", "lines") == "2 lines"
                  && Composition.Amount(0, "line", "lines") == "0 lines",
                "one is singular, everything else is plural",
                "including zero, which reads as a plural in English");

            check(Composition.Amount(1068, "line", "lines") == "1,068 lines",
                "and the number is grouped like every other count on screen",
                "a card grouping its thousands beside a dialog that does not is two measurements");

            // ── The shares add up to 100, whoever draws them ──────────────────
            //
            // 🔴 The file that showed it: 14 human, 15 validated, 2,498 AI. Rounded on their own
            // the three read 1, 1 and 99 — 101 in the mod — while the Manager and the website,
            // letting the last band shown absorb the remainder, read 98.
            var seen = Composition.Shares(new[] { 14, 15, 2498, 0, 0 });
            check(seen[0] == 1 && seen[1] == 1 && seen[2] == 98 && seen[3] == 0 && seen[4] == 0,
                "14 / 15 / 2,498 lines read 1%, 1%, 98%",
                "the last band holding anything takes the remainder; the mod said 99");

            // The absorber is the last band HOLDING something, not the last band: trailing empty
            // bands stay at 0 whether a product draws them or not.
            var trailing = Composition.Shares(new[] { 1, 1, 0, 1, 0 });
            check(trailing[0] == 33 && trailing[1] == 33 && trailing[2] == 0 && trailing[3] == 34 && trailing[4] == 0,
                "an empty band stays at 0 and the last full one absorbs",
                "the website draws the first three even empty, the Manager none that is — the figures must agree");

            foreach (var counts in new[] { new[] { 1, 1, 1, 1, 1 }, new[] { 7, 0, 0, 0, 0 }, new[] { 3, 3, 3, 0, 0 }, new[] { 0, 0, 0, 0, 5 } })
            {
                int sum = 0;
                foreach (var share in Composition.Shares(counts)) sum += share;
                check(sum == 100, "the shares of " + string.Join("/", counts) + " add up to 100",
                    "a key adding up to 99 or 101 invites the reader to look for the mistake");
            }

            var empty = Composition.Shares(new[] { 0, 0, 0, 0, 0 });
            check(empty[0] == 0 && empty[4] == 0, "nothing at all is five zeros, not a division",
                "a bar with no line has no share to state");

            // ── The stage words live in ONE place ─────────────────────────────
            //
            // 🔴 They were moved to Quality.StageName after existing twice, and the Manager still
            // held a private copy that had drifted back to "Review well under way" — the idiom the
            // socle replaced. Named here rather than merely agreed upon.
            check(Quality.StageName(ReviewStage.Advanced) == "Review in progress",
                "an advanced review is 'Review in progress'",
                "'well under way' is transparent to a native and opaque to everybody else");

            check(Quality.StageName(ReviewStage.Reviewed) == "Fully reviewed"
                  && Quality.StageName(ReviewStage.Started) == "Review started"
                  && Quality.StageName(ReviewStage.Machine) == "Machine translation",
                "and the other three are the words both products show",
                "a second copy of a verdict is a second chance to disagree with yourself");
        }
    }
}
