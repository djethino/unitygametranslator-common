using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The tag saying whose figures a quality bar is counting.
    ///
    /// ⚠ Most of what follows checks that this tag stays UNLIKE <see cref="EditScope"/>. The two
    /// answer different questions — where a save lands, against where a count came from — and the
    /// whole reason this one exists as a separate thing is that they were once the same control.
    /// Somebody who reads "On this machine" over a set of numbers and takes it for the switch has
    /// been told their figures are a destination.
    /// </summary>
    internal static class ProvenanceChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            // ── Which of the three ────────────────────────────────────────────
            check(Provenance.Of(countedFromDisk: true, haveCopyHere: true) == Origin.YourFile,
                "counting the file on disk gives your file",
                "having also published it changes nothing about what was counted");

            check(Provenance.Of(countedFromDisk: true, haveCopyHere: false) == Origin.YourFile,
                "and that holds however the rest of the world stands",
                "the origin of a measurement is not a matter of opinion");

            check(Provenance.Of(countedFromDisk: false, haveCopyHere: true) == Origin.PublishedWithCopy,
                "published figures, with a copy here, say so",
                "the two diverge the moment somebody plays, and the reader has to know which they see");

            check(Provenance.Of(countedFromDisk: false, haveCopyHere: false) == Origin.PublishedOnly,
                "published figures with nothing here are marked not downloaded",
                "that is the one place where 'not downloaded' means anything");

            // ── Every state is named and explained ────────────────────────────
            foreach (Origin origin in Enum.GetValues(typeof(Origin)))
            {
                check(Provenance.Name(origin).Length > 0 && Provenance.Effect(origin).Length > 0,
                    $"{origin} is named and explained",
                    "an unlabelled bar is what made this necessary in the first place");
            }

            // ── 🔴 It must not speak the switch's language ────────────────────
            //
            // The failure this guards against is a rename: somebody tidying the wording lands on
            // "On this machine" here because it is the phrase used next door, and the two controls
            // become indistinguishable again without a single line of layout changing.
            foreach (Origin origin in Enum.GetValues(typeof(Origin)))
            {
                foreach (EditSide side in Enum.GetValues(typeof(EditSide)))
                {
                    check(!string.Equals(Provenance.Name(origin), EditScope.Name(side),
                              StringComparison.OrdinalIgnoreCase),
                        $"{origin} does not borrow the {side} side's words",
                        "an origin and a destination described by the same phrase is the confusion "
                        + "this tag was split off to end");
                }
            }
        }
    }
}
