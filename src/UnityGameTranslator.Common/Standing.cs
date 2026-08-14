namespace UnityGameTranslator.Common
{
    /// <summary>What a translation is in its lineage, on the server.</summary>
    public enum LineageRole
    {
        /// <summary>Nothing of this lineage is published under this name.</summary>
        None,

        /// <summary>Published, and this account leads the lineage.</summary>
        Main,

        /// <summary>A contribution to somebody else's Main. One becomes this by uploading.</summary>
        Branch,

        /// <summary>
        /// A Main that took another Main's work off on its own, keeping the trace of its parent.
        /// A Fork IS a Main — it is only distinguished so the tree of contributions stays readable.
        /// </summary>
        Fork,
    }

    /// <summary>Whose name, if anybody's, this screen is acting under.</summary>
    public enum AccountStanding
    {
        /// <summary>
        /// Nobody is signed in. ⚠ NOT a lesser state: somebody with no account can hold a community
        /// translation and diverge from it exactly like a Branch would.
        /// </summary>
        Anonymous,

        /// <summary>Signed in, and this is the account the game itself uses.</summary>
        Ours,

        /// <summary>
        /// Signed in, but the game belongs to a different account on this same site — or the game is
        /// signed in and this screen is not. One computer legitimately carries several people's games.
        /// </summary>
        SomebodyElses,
    }

    /// <summary>
    /// Where somebody stands with a translation, on four questions that are INDEPENDENT.
    ///
    /// 🔴 **Written because one enum was answering three of them at once.** The mod's
    /// `SyncStatusType` reads Synced · OutOfSync · Conflict · LocalOnly · NotLoggedIn · NoLocal —
    /// but "LocalOnly" answers *has it been published*, "NotLoggedIn" answers *whose name am I
    /// under*, and "NoLocal" answers *do I have the file*. Only three of the six are about being up
    /// to date. Worse, collapsing them lost information the other products keep: `OutOfSync` cannot
    /// say WHICH side moved, where <see cref="SyncDirection"/> distinguishes Download from Upload.
    ///
    /// ⚠ **So they are separated rather than merged.** Every screen in every product answers the
    /// same four, and a screen that cannot answer one says so instead of guessing.
    ///
    /// ⚠ **The four are genuinely independent.** An anonymous person can be behind the Main. A Main
    /// owner can have nothing local. Somebody looking at another account's game can be perfectly in
    /// step with it. Any pairing assumed here would be a screen unable to describe a real user.
    /// </summary>
    public struct Standing
    {
        /// <summary>Has this ever left the machine, and is it here at all.</summary>
        public Publication Publication;

        /// <summary>Which way it has drifted, when there is something to compare with.</summary>
        public SyncDirection? Sync;

        /// <summary>Whose name this screen acts under.</summary>
        public AccountStanding Account;

        /// <summary>What this translation is in its lineage.</summary>
        public LineageRole Role;

        /// <summary>Contributions waiting on this Main. Null when the question does not apply.</summary>
        public int? BranchesWaiting;

        /// <summary>A Branch whose Main is no longer on the site.</summary>
        public bool MainMissing;
    }

    /// <summary>What somebody may do from here, and why not when they may not.</summary>
    public static class Standings
    {
        /// <summary>
        /// May this screen change the translation FILE on this machine — merging, taking the Main's
        /// version again, editing it?
        ///
        /// 🔴 **Anonymous is allowed, deliberately.** Somebody with no account can hold a community
        /// translation and go on adding lines, so they diverge exactly like a Branch would while
        /// being neither Branch nor Fork. Merging and re-downloading write nothing but the local
        /// file — refusing them an account they do not need is how a product tells somebody their
        /// work does not count.
        ///
        /// ⚠ **Somebody else's game is refused**, and that is not about the server: it is about not
        /// breaking, by inattention, the setup another user of this computer put in place.
        /// </summary>
        public static bool MayWriteLocally(AccountStanding account)
        {
            return account != AccountStanding.SomebodyElses;
        }

        /// <summary>May this screen publish, contribute or fork? Those need a name.</summary>
        public static bool MayWriteToServer(AccountStanding account)
        {
            return account == AccountStanding.Ours;
        }

        /// <summary>
        /// Why writing was refused, in one sentence with the way out. Empty when it was not.
        ///
        /// ⚠ The way out matters more than the refusal. "Not your account" leaves somebody stuck;
        /// naming where the account is changed does not.
        /// </summary>
        public static string ExplainRefusal(AccountStanding account, bool toServer)
        {
            if (account == AccountStanding.SomebodyElses)
            {
                return "This game is set up under a different account. To change anything here, "
                     + "open the game and sign in with that account.";
            }

            if (toServer && account == AccountStanding.Anonymous)
            {
                return "Sign in to publish. Editing and merging your own copy need no account.";
            }

            return "";
        }
    }
}
