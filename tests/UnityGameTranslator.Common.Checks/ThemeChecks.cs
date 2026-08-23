using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The palette, checked against what the website actually renders.
    ///
    /// The expected hexadecimals below were read out of the running site's CSS custom properties
    /// (`--color-*`) in a browser on 2026-08-13 and converted to sRGB — they are written here as
    /// literals, independently of <see cref="Theme"/>, so this checks the port rather than checking
    /// the port against itself.
    ///
    /// ⚠ The site runs Tailwind v4. Its palette is NOT v3's, and both consumers had v3 values at
    /// some point — the Manager throughout its theme, the mod in its accent. The v3 hexes are
    /// listed below as REFUSALS: if one of them ever comes back, that is the mistake returning, and
    /// this is where it stops.
    ///
    /// ⚠ What this cannot check is whether the site has since changed. Nothing in C# can reach the
    /// site's stylesheet, so a redesign there makes this file wrong and silent. That is the same
    /// bargain as Quality, whose reference is PHP too.
    /// </summary>
    internal static class ThemeChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            // What the site renders, token by token.
            var expected = new Dictionary<string, string>
            {
                { "SurfaceBase", "0F0F1A" },      // the page
                { "SurfaceDeep", "101828" },      // gray-900
                { "SurfaceCard", "1E2939" },      // gray-800
                { "SurfaceRaised", "364153" },    // gray-700
                { "SurfaceHover", "4A5565" },     // gray-600
                { "BorderSubtle", "364153" },     // gray-700
                { "BorderStrong", "4A5565" },     // gray-600
                { "TextPrimary", "F3F4F6" },      // gray-100
                { "TextSecondary", "D1D5DC" },    // gray-300
                { "TextMuted", "99A1AF" },        // gray-400
                { "Accent", "9810FA" },           // purple-600
                { "AccentEdge", "AD46FF" },       // purple-500
                { "AccentSoft", "C27AFF" },       // purple-400
                { "AccentDeep", "8200DB" },       // purple-700
                { "AccentDim", "59168B" },        // purple-900
                { "StatusSuccess", "05DF72" },    // green-400
                { "StatusWarning", "FFB900" },    // amber-400
                { "StatusError", "FF6467" },      // red-400
                { "StatusInfo", "50A2FF" },       // blue-400
                { "StatusNeutral", "6A7282" },    // gray-500
                { "QualityHuman", "00C950" },     // green-500
                { "QualityValidated", "2B7FFF" }, // blue-500
                { "QualityAi", "FF6900" },        // orange-500
                { "QualityKept", "AD46FF" },      // purple-500
                { "QualityCapture", "6A7282" },   // gray-500
                { "QualityTrack", "364153" },     // gray-700
                { "TagModUi", "009689" },         // teal-600
                { "MarkLit", "A5F3FC" },          // cyan-200
            };

            var actual = new Dictionary<string, Rgb>
            {
                { "SurfaceBase", Theme.SurfaceBase },
                { "SurfaceDeep", Theme.SurfaceDeep },
                { "SurfaceCard", Theme.SurfaceCard },
                { "SurfaceRaised", Theme.SurfaceRaised },
                { "SurfaceHover", Theme.SurfaceHover },
                { "BorderSubtle", Theme.BorderSubtle },
                { "BorderStrong", Theme.BorderStrong },
                { "TextPrimary", Theme.TextPrimary },
                { "TextSecondary", Theme.TextSecondary },
                { "TextMuted", Theme.TextMuted },
                { "Accent", Theme.Accent },
                { "AccentEdge", Theme.AccentEdge },
                { "AccentSoft", Theme.AccentSoft },
                { "AccentDeep", Theme.AccentDeep },
                { "AccentDim", Theme.AccentDim },
                { "StatusSuccess", Theme.StatusSuccess },
                { "StatusWarning", Theme.StatusWarning },
                { "StatusError", Theme.StatusError },
                { "StatusInfo", Theme.StatusInfo },
                { "StatusNeutral", Theme.StatusNeutral },
                { "QualityHuman", Theme.QualityHuman },
                { "QualityValidated", Theme.QualityValidated },
                { "QualityAi", Theme.QualityAi },
                { "QualityKept", Theme.QualityKept },
                { "QualityCapture", Theme.QualityCapture },
                { "QualityTrack", Theme.QualityTrack },
                { "TagModUi", Theme.TagModUi },
                { "MarkLit", Theme.MarkLit },
            };

            // ── The lit scope mark must OUTRANK the dimmed ones, on every button it can land on ──
            //
            // 🔴 This is the defect it was written for, and it is measurable: the lit mark used to
            // be AccentSoft, which on a purple-600 button scored 1.98 against the fill while the
            // DIMMED marks scored 2.13. The control read backwards, and nothing said so — the two
            // colours were each defensible on their own, and only their ratio against a third thing
            // was wrong.
            //
            // ⚠ Contrast is computed here from the WCAG definition, not from anything in Theme:
            // a check that borrowed the library's own arithmetic would only prove it agrees with
            // itself. Every fill a button can carry in any of the three products is listed.
            var fills = new Dictionary<string, Rgb>
            {
                { "the primary button", Theme.Accent },
                { "a primary button hovered", Theme.AccentEdge },
                { "a primary button pressed", Theme.AccentDeep },
                { "the secondary button", Theme.SurfaceRaised },
                { "a card", Theme.SurfaceCard },
                { "a recess", Theme.SurfaceDeep },
            };

            foreach (var fill in fills)
            {
                double lit = Contrast(Theme.MarkLit, fill.Value);
                double dimmed = Contrast(Theme.TextMuted, fill.Value);

                check(lit >= 3.0,
                    "the lit mark is legible on " + fill.Key,
                    "3.0 is the floor for something read as a picture; this scores "
                        + lit.ToString("0.00"));

                check(lit > dimmed * 1.4,
                    "and clearly outranks the dimmed ones there",
                    "lit " + lit.ToString("0.00") + " against dimmed " + dimmed.ToString("0.00")
                        + ": whichever is brighter is the one somebody reads as chosen");
            }

            // ⚠ Stated as a requirement rather than a hex: any colour a button is FILLED with is
            // disqualified, whatever it is, because the mark sits on top of it.
            foreach (var fill in fills)
            {
                check(Theme.MarkLit.ToHex() != fill.Value.ToHex(),
                    "the lit mark is not the colour of " + fill.Key,
                    "a mark the same colour as what it sits on is an invisible mark");
            }

            // 🔴 **A LIGHT fill takes no mark at all**, and this check exists because the first
            // draft of the list above included the warning button: cyan-200 scores 1.38 on amber,
            // and so would ANY light colour — amber is nearly as bright as white, so there is no
            // shade of a light mark that survives it. The answer is not a different mark, it is
            // that the marks never go there.
            //
            // ⚠ Which is not a limitation in practice, it is a rule that was already true: the
            // marks say where a save LANDS, and they sit on the actions that save — publish,
            // contribute, download, edit. None of those is styled as a warning or as a danger,
            // because none of them is one. If an action ever needs both, the button changes, not
            // the mark.
            foreach (var light in new Dictionary<string, Rgb>
                     {
                         { "a warning button", Theme.StatusWarning },
                         { "a danger button", Theme.StatusError },
                     })
            {
                check(Contrast(Theme.TextPrimary, light.Value) < 3.0,
                    "nothing light is readable on " + light.Key + ", marks included",
                    "if this ever passes, that fill darkened and could carry marks after all");
            }

            check(actual.Count == expected.Count,
                "every colour is checked",
                "a value added to Theme without a case here is a value nobody verified");

            foreach (var pair in expected)
            {
                Rgb got;
                bool known = actual.TryGetValue(pair.Key, out got);
                check(known && got.ToHex() == pair.Value,
                    pair.Key + " is #" + pair.Value,
                    known ? "the site renders #" + pair.Value + ", this says #" + got.ToHex()
                          : "missing from the table above");
            }

            // ⚠ The Tailwind v3 values, named so they cannot come back unnoticed.
            check(Theme.Accent.ToHex() != "9333EA",
                "the accent is not the v3 purple",
                "#9333EA is Tailwind v3's purple-600; the site moved to #9810FA");
            check(Theme.QualityAi.ToHex() != "F97316",
                "the AI band is not the v3 orange",
                "#F97316 is v3's orange-500; the site renders #FF6900");
            check(Theme.QualityHuman.ToHex() != "22C55E",
                "the human band is not the v3 green",
                "#22C55E is v3's green-500; the site renders #00C950");

            // The measure and the statuses must not drift back into each other. This is the
            // divergence that actually shipped: the mod drew the AI band with StatusWarning.
            check(Theme.QualityAi.ToHex() != Theme.StatusWarning.ToHex(),
                "the AI band is not the warning colour",
                "sharing one value is how the band turned amber in game and stayed orange elsewhere");
            check(Theme.QualityHuman.ToHex() != Theme.StatusSuccess.ToHex(),
                "the human band is not the success colour",
                "a measurement and a verdict are two registers");
            check(Theme.QualityValidated.ToHex() != Theme.StatusInfo.ToHex(),
                "the validated band is not the info colour",
                "same reason");

            // ── Tag chips ──────────────────────────────────────────────────────────────────────
            //
            // Read out of the running site on 2026-08-23 with getComputedStyle on the rendered
            // `.tag-*` chips — not off a class name, and written as literals here so this checks
            // the port rather than checking it against itself.
            var chips = new Dictionary<string, string>
            {
                { "H", "16A34A" },   // green-600
                { "V", "2563EB" },   // blue-600
                { "A", "EA580C" },   // orange-600
                { "S", "9333EA" },   // purple-600
                { "M", "0D9488" },   // teal-600
            };

            foreach (var pair in chips)
            {
                check(Theme.ChipBackground(pair.Key).ToHex() == pair.Value,
                    "chip " + pair.Key + " is the colour the site draws",
                    "expected #" + pair.Value + ", got #" + Theme.ChipBackground(pair.Key).ToHex());
            }

            // 🔴 A chip is six pixels of white type on a square, where a band is a wide filled
            // area: the site has always used the 600 ramp for one and 500 for the other. Sharing
            // them would make the letter thin, and the day somebody "tidied" them into a single
            // value is the day the chips became unreadable everywhere at once.
            check(Theme.ChipHuman.ToHex() != Theme.QualityHuman.ToHex(),
                "the chip ramp is not the band ramp",
                "600 behind small white type, 500 for a filled band — two jobs, two values");

            // Every chip carries white type, so every one of them has to be dark enough for it.
            foreach (var tag in new[] { "H", "V", "A", "S", "M" })
            {
                check(Contrast(Theme.ChipLetter, Theme.ChipBackground(tag)) >= 3.0,
                    "chip " + tag + " carries its letter",
                    "white on #" + Theme.ChipBackground(tag).ToHex() + " is under 3:1");
            }

            // An unknown tag is not a crash and not a sixth colour: it is what an unclassified
            // line already is everywhere else.
            check(Theme.ChipBackground("?").ToHex() == Theme.QualityCapture.ToHex(),
                "an unknown tag falls back on the capture grey",
                "inventing a colour for it would teach a meaning that does not exist");

            // Where two roles DO share a value, they share it on purpose, and saying so here keeps
            // the next reader from 'fixing' one of them.
            check(Theme.QualityCapture.ToHex() == Theme.StatusNeutral.ToHex(),
                "captured and neutral are the same grey",
                "gray-500 on both sides: what is pending IS the neutral state");
            check(Theme.BorderSubtle.ToHex() == Theme.SurfaceRaised.ToHex(),
                "a card's edge is the colour of the surface above it",
                "gray-700 does both jobs on the site, which is what makes the edge read as depth");

            // The four purples are four. One of them being wrong shows up as a flat accent, which
            // is exactly what nobody notices until the two products sit side by side.
            var purples = new HashSet<string>
            {
                Theme.Accent.ToHex(), Theme.AccentEdge.ToHex(),
                Theme.AccentSoft.ToHex(), Theme.AccentDeep.ToHex(),
            };
            check(purples.Count == 4,
                "the accent has four distinct shades",
                "fill, edge, text and pressed — the mod used one purple for all four");

            // A colour with no channel variation is a grey; an accent that becomes one is a
            // copy-paste that landed in the wrong line.
            check(Theme.Accent.R != Theme.Accent.G || Theme.Accent.G != Theme.Accent.B,
                "the accent is not a grey", "a flat value here means a line was overwritten");

            // The neutral text ramp must actually descend, or "muted" and "primary" say the same.
            check(Theme.TextPrimary.R > Theme.TextSecondary.R && Theme.TextSecondary.R > Theme.TextMuted.R,
                "the text ramp goes from bright to dim",
                "three levels that do not descend are one level written three times");

            // Rgb itself: the hex constructor is how the whole palette is written, so a mistake in
            // it would be a mistake in every value at once.
            var probe = new Rgb(0x9810FA);
            check(probe.R == 0x98 && probe.G == 0x10 && probe.B == 0xFA,
                "0xRRGGBB unpacks in that order",
                "swapped channels would recolour the entire product consistently, hence invisibly");
            check(probe.ToHex() == "9810FA", "and prints back the way it was written", "round trip");
            check(new Rgb(0xFFFFFF).Rf == 1f && new Rgb(0x000000).Rf == 0f,
                "the float form spans 0 to 1", "Unity wants floats; 255 there would be white everywhere");

            // Blending. Both products flatten their tinted states through this, so an error here
            // moves every selected row and every callout in both windows at once.
            var white = new Rgb(0xFFFFFF);
            var black = new Rgb(0x000000);
            check(white.Over(black, 0).ToHex() == "000000",
                "no strength leaves the surface untouched", "0 means the tint is not applied at all");
            check(white.Over(black, 1).ToHex() == "FFFFFF",
                "full strength is the tint itself", "1 means the surface is gone");
            check(white.Over(black, 0.5).ToHex() == "808080",
                "half way is half way, rounded up from 127.5",
                "truncating instead would darken every blend by a unit per channel");
            check(white.Over(black, 2).ToHex() == "FFFFFF" && white.Over(black, -1).ToHex() == "000000",
                "strength outside 0-1 is clamped", "a tint stronger than the colour means nothing");
            check(Theme.Accent.Over(Theme.SurfaceCard, 0.5).ToHex() ==
                  Theme.Accent.Over(Theme.SurfaceCard, 0.5).ToHex(),
                "blending is deterministic", "the same state must not differ between two windows");

            // The states themselves: what matters is that they stay READABLE, which is the whole
            // reason they are blends rather than the accent laid on raw.
            check(Theme.RowSelected.ToHex() != Theme.AccentDim.ToHex(),
                "a selected row is not the raw purple",
                "purple-900 undiluted swallows the text sitting on it");
            check(Theme.RowSelected.ToHex() != Theme.SurfaceCard.ToHex(),
                "and it is not the card either", "a selection nobody can see is not one");
            check(Theme.RowRelated.ToHex() != Theme.RowSelected.ToHex(),
                "related is not the same as selected", "two states, two shades, or one of them is decoration");
            check(Theme.CalloutError.ToHex() != Theme.CalloutWarning.ToHex()
               && Theme.CalloutInfo.ToHex() != Theme.CalloutSuccess.ToHex(),
                "the four callouts are four colours", "a callout that cannot be told apart says nothing");
        }

        /// <summary>
        /// WCAG 2.x relative luminance, written from the definition.
        ///
        /// ⚠ Deliberately not built on anything in <see cref="Theme"/>: the point of a check is to
        /// hold the library to an outside rule, and one that reuses the library's own arithmetic
        /// only proves it agrees with itself.
        /// </summary>
        private static double Luminance(Rgb colour)
        {
            return 0.2126 * Channel(colour.R) + 0.7152 * Channel(colour.G) + 0.0722 * Channel(colour.B);
        }

        private static double Channel(byte value)
        {
            double c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        /// <summary>The WCAG ratio of two colours, always ≥ 1.</summary>
        private static double Contrast(Rgb a, Rgb b)
        {
            double la = Luminance(a), lb = Luminance(b);
            double high = la > lb ? la : lb;
            double low = la > lb ? lb : la;
            return (high + 0.05) / (low + 0.05);
        }
    }
}
