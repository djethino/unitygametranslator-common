using System;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// Where a translation stands ONCE AN ACTION HAS BEEN APPLIED.
    ///
    /// 🔴 **The question is "who holds the result afterwards", NOT "which file does this write".**
    /// Written this way because the other reading was taken twice, by the same person, days apart,
    /// and each time it reclassified real buttons the wrong way. Downloading the published version
    /// writes only the local file — under "which file does it write" that is Local, and it is
    /// wrong: afterwards the two sides carry the same translation, which is the whole thing
    /// somebody wants to know before pressing it.
    ///
    /// ⚠ Ask it of every action in one sentence: **after applying, do both sides carry the same
    /// data?** Yes is <see cref="Both"/>. No, and it is here, is <see cref="Local"/>. No, and it is
    /// on the site, is <see cref="Server"/>. <see cref="SideAfter"/> exists so a caller answers the
    /// two halves of that question instead of picking a name.
    /// </summary>
    public enum EditSide
    {
        /// <summary>
        /// The published translation carries the result; this machine does not.
        ///
        /// ⚠ Which in practice means work done with no game and no manager on the other end —
        /// merging a contribution from the website, changing what a translation says about itself.
        /// </summary>
        Server,

        /// <summary>
        /// Both carry the same translation afterwards.
        ///
        /// ⚠ Publishing and downloading are BOTH this, and that surprises people: each one writes a
        /// single file. What matters is where it leaves things — in step — not which hand moved.
        /// </summary>
        Both,

        /// <summary>
        /// The file on this machine carries the result; the published version does not.
        /// </summary>
        Local,
    }

    /// <summary>Why a side cannot be worked on right now.</summary>
    public enum SideBlock
    {
        /// <summary>It can.</summary>
        None,

        /// <summary>Nothing is signed in, and the server side has no meaning without a name.</summary>
        SignedOut,

        /// <summary>There is no file on this machine — nothing local to edit.</summary>
        NoLocalFile,

        /// <summary>
        /// Nothing of this lineage is published under this account. Sending would not update
        /// anything, it would create something — which is publishing, a separate act.
        /// </summary>
        NothingPublished,

        /// <summary>
        /// This lineage is led by somebody else. Their translation is not ours to rewrite, and a
        /// contribution to it is a separate act with its own words.
        /// </summary>
        SomebodyElses,

        /// <summary>
        /// This screen is running somewhere that cannot reach the machine's files — a browser on
        /// its own, with no game and no manager on the other end.
        /// </summary>
        NoMachineHere,
    }

    /// <summary>One position of the switch, and whether it can be chosen.</summary>
    public struct SideStanding
    {
        public EditSide Side;
        public SideBlock Block;

        public bool Available => Block == SideBlock.None;
    }

    /// <summary>
    /// What an editing screen is about to change — the local file, the published translation, or
    /// both — and which of those three is even possible where it is running.
    ///
    /// ⚠ **Written once because it has to LOOK the same in three products.** The same switch appears
    /// in a game, in a browser and in the manager, and somebody who learns it in one must not have
    /// to relearn it in the next. What varies between them is only what is reachable from where
    /// they stand, which is what this decides.
    ///
    /// ⚠ **A greyed position must always be explained.** A switch with a dead third of it and no
    /// reason reads as a broken window; the same switch with "nothing of yours is published here"
    /// under it is a product telling somebody where they stand. Every block below carries its
    /// sentence for exactly that reason.
    ///
    /// ⚠ **Never a silent fallback.** When the side somebody asked for is unavailable, the answer
    /// is to say so and offer what is — not to quietly act on the other one. Saving to the wrong
    /// copy is the single worst outcome this switch exists to prevent.
    /// </summary>
    public static class EditScope
    {
        /// <summary>
        /// Where each of the three sides stands.
        /// </summary>
        /// <param name="hasLocalFile">A translation file exists on this machine.</param>
        /// <param name="canReachMachine">
        /// This screen has something on the other end that can read and write that file — a running
        /// game, or the manager. False for a browser opened on its own, where "local" is a place
        /// nothing can reach.
        /// </param>
        /// <param name="signedIn">An account is signed in.</param>
        /// <param name="publishedByThisAccount">
        /// This lineage has a published translation belonging to the signed-in account.
        /// </param>
        /// <param name="publishedBySomebodyElse">
        /// This lineage is led by another account. ⚠ Distinct from "nothing published": one means
        /// there is nothing to update, the other that there is something and it is not ours.
        /// </param>
        public static SideStanding[] Sides(bool hasLocalFile, bool canReachMachine, bool signedIn,
                                           bool publishedByThisAccount, bool publishedBySomebodyElse)
        {
            var local = !canReachMachine ? SideBlock.NoMachineHere
                : !hasLocalFile ? SideBlock.NoLocalFile
                : SideBlock.None;

            var server = !signedIn ? SideBlock.SignedOut
                : publishedBySomebodyElse && !publishedByThisAccount ? SideBlock.SomebodyElses
                : !publishedByThisAccount ? SideBlock.NothingPublished
                : SideBlock.None;

            // Both is exactly what its name says: it needs each of them. Reported with the reason
            // of whichever is missing — naming one obstacle is actionable, "unavailable" is not.
            var both = local != SideBlock.None ? local
                : server != SideBlock.None ? server
                : SideBlock.None;

            return new[]
            {
                new SideStanding { Side = EditSide.Server, Block = server },
                new SideStanding { Side = EditSide.Both, Block = both },
                new SideStanding { Side = EditSide.Local, Block = local },
            };
        }

        /// <summary>
        /// The side a screen should start on: the one asked for when it is possible, otherwise the
        /// most cautious one that is.
        ///
        /// ⚠ Local before Server. Falling back to the server side would mean somebody who opened an
        /// editor expecting to change their own copy publishes instead.
        /// </summary>
        public static EditSide Default(SideStanding[] sides, EditSide preferred)
        {
            foreach (var side in sides)
                if (side.Side == preferred && side.Available) return preferred;

            foreach (var side in sides)
                if (side.Side == EditSide.Local && side.Available) return EditSide.Local;

            foreach (var side in sides)
                if (side.Side == EditSide.Server && side.Available) return EditSide.Server;

            // Nothing is available. The caller shows the switch dead, with the reasons under it,
            // rather than pretending a side was chosen.
            return preferred;
        }

        /// <summary>
        /// Which side an action leaves things on, from the only two facts that decide it.
        ///
        /// 🔴 **Use this instead of naming a side by hand.** Every mark that was wrong got that way
        /// the same manner: somebody looked at a button, thought "this one publishes", and wrote
        /// Server. The name invites it. These two questions do not — they are about the state
        /// afterwards, which is what the control tells a user.
        ///
        /// 🔴 **"Published" means YOUR published translation. Never somebody else's.** Taking a
        /// community translation leaves this machine carrying it — and leaves the published copy
        /// that belongs to you exactly as it was, or non-existent. So it is <see cref="Local"/>,
        /// not <see cref="Both"/>, and calling it Both would reassure at the precise moment a Main
        /// owner's own published version stops matching their machine.
        ///
        /// ⚠ That is the same rule the server enforces on writes: a Main may only write its own
        /// translation, a Branch only its own branch, and holding somebody else's file makes you a
        /// user of it, not its owner. The switch has to read the world the same way, or two parts
        /// of one product describe one situation differently.
        ///
        /// ⚠ Neither true is not a side, it is an action that changes no translation of yours.
        /// Reported as <see cref="EditSide.Local"/> because that is the harmless answer: a screen
        /// that claims to leave the published version alone and does is never a lie, where the
        /// reverse would be.
        /// </summary>
        /// <param name="onThisMachine">
        /// Once applied, the file in the game carries the result of this action.
        /// </param>
        /// <param name="yourPublishedCopy">
        /// Once applied, the translation published UNDER YOUR OWN NAME carries the result. True for
        /// publishing, and true for taking the latest of a lineage you lead. ⚠ FALSE for taking
        /// somebody else's translation, however completely it lands on this machine: what you now
        /// hold is theirs, and nothing of yours moved.
        /// </param>
        public static EditSide SideAfter(bool onThisMachine, bool yourPublishedCopy)
        {
            if (onThisMachine && yourPublishedCopy) return EditSide.Both;
            if (yourPublishedCopy) return EditSide.Server;
            return EditSide.Local;
        }

        /// <summary>The name of a side, identical in every product.</summary>
        public static string Name(EditSide side)
        {
            switch (side)
            {
                case EditSide.Server: return "Published";
                case EditSide.Both: return "Both";
                default: return "On this machine";
            }
        }

        /// <summary>
        /// Which picture stands for a side — an identity, never the picture itself.
        ///
        /// ⚠ **The icons are what make the two sizes one control.** The switch is shown in full
        /// beside a title, where there is room for words, and as the three marks alone beside an
        /// action, where there is not. The small one is only recognisable as the big one if the
        /// pictures are identical, so the metaphor is fixed here and nowhere else: a cloud for what
        /// is published, a screen for this machine, and the two linked for both.
        ///
        /// ⚠ **A name, not a drawing.** A browser has a CSS class, the manager a vector path and the
        /// mod a sprite built from raw pixels — three technologies with nothing in common. What they
        /// CAN share is the decision of which picture means which side, and that is a rule, so it
        /// lives here beside <see cref="Name"/> and <see cref="Effect"/>. Anything returning pixels
        /// from this library would be rendering, which this library does not do.
        /// </summary>
        public static string Mark(EditSide side)
        {
            switch (side)
            {
                case EditSide.Server: return "cloud";
                case EditSide.Both: return "link";
                default: return "display";
            }
        }

        /// <summary>
        /// Why a side cannot be chosen, in one sentence somebody can act on. Empty when it can.
        /// </summary>
        public static string Explain(SideBlock block)
        {
            switch (block)
            {
                case SideBlock.SignedOut:
                    return "Sign in to work on the published version.";
                case SideBlock.NoLocalFile:
                    return "There is no translation on this machine yet.";
                case SideBlock.NothingPublished:
                    return "Nothing of yours is published for this game yet — publishing it is a "
                         + "separate step.";
                case SideBlock.SomebodyElses:
                    return "This translation is led by somebody else. Yours would be a contribution "
                         + "to it, which is a separate step.";
                case SideBlock.NoMachineHere:
                    return "This page is open on its own, with no game or manager on the other end "
                         + "to reach the file on your machine.";
                default:
                    return "";
            }
        }

        /// <summary>
        /// What saving on this side will actually do, said before it is done.
        ///
        /// ⚠ The one line that has to be identical everywhere: somebody moves this switch to learn
        /// what a save means, and three products describing the same act in three ways would defeat
        /// the whole point of having one switch.
        /// </summary>
        public static string Effect(EditSide side)
        {
            switch (side)
            {
                case EditSide.Server:
                    return "Saving changes the published translation. The copy on your machine is "
                         + "left alone.";
                case EditSide.Both:
                    return "Saving changes the copy on your machine and the published translation, "
                         + "keeping them in step.";
                default:
                    return "Saving changes the copy on your machine. Nothing is published.";
            }
        }
    }
}
