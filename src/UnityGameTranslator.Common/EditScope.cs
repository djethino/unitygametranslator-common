using System;

namespace UnityGameTranslator.Common
{
    /// <summary>Which copy of a translation an editing screen is working on.</summary>
    public enum EditSide
    {
        /// <summary>The published translation, on the community site.</summary>
        Server,

        /// <summary>Both at once: what is saved lands here AND is published.</summary>
        Both,

        /// <summary>The file on this machine, in the game.</summary>
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
