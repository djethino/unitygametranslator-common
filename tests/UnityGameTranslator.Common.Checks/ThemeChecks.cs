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
            };

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
        }
    }
}
