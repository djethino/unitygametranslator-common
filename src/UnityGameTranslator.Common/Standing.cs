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

        /// <summary>
        /// Contributions waiting on this Main. Null when the question does not apply.
        ///
        /// 🔴 **Not "how many branches exist".** Two filters, and dropping either produces noise:
        /// these are the ones their Main has NOT been through in their current state, AND that are
        /// holding something a merge would offer. Counting the rest sends somebody to review
        /// emptiness, and a number that never falls to zero is a number nobody reads.
        /// </summary>
        public int? BranchesWaiting;

        /// <summary>
        /// How many lines those contributions hold, counted once each. Null when unknown — an
        /// older server — which is not the same as none.
        ///
        /// ⚠ Distinct keys, never a sum: two contributions offering the same line are one line to
        /// recover, and adding their counts would promise twice the work that exists.
        /// </summary>
        public int? LinesAvailable;

        /// <summary>A Branch whose Main is no longer on the site.</summary>
        public bool MainMissing;

        /// <summary>The Main is still there and its owner erased their account.</summary>
        public bool MainAbandoned;

        /// <summary>The Main still exists and has closed its contributions since this branch was sent.</summary>
        public bool BranchFrozen;

        /// <summary>
        /// The author's own declaration that the translation is complete. Null when the server did
        /// not say — an older server, or a lineage nobody has asked about — which is not "no".
        /// </summary>
        public bool? Finished;

        /// <summary>
        /// Who leads this lineage, when somebody else does.
        ///
        /// ⚠ Only used to name them in the <see cref="Publication.NotYours"/> sentence. Null is
        /// ordinary — this account leads it, nothing is published, or the server did not say — and
        /// the sentence falls back to "Somebody else" rather than to nothing.
        /// </summary>
        public string? MainOwner;
    }

    /// <summary>
    /// What this machine knows about the translation on its own — read off the file and the
    /// engine, without asking anybody.
    /// </summary>
    public struct LocalFacts
    {
        /// <summary>How many lines the file holds. Zero is "nothing here".</summary>
        public int Lines;

        /// <summary>Lines changed since the last sync, as the mod counts them.</summary>
        public int LocalChanges;

        /// <summary>The file's own settings changed since the last sync, lines aside.</summary>
        public bool MetadataDirty;

        /// <summary>The server content last agreed with, or null for a file that never synced.</summary>
        public string? LastSyncedHash;

        /// <summary>The content as it stands, hashed as <see cref="ContentHash"/> does. Null when nobody computed it.</summary>
        public string? ContentHash;

        /// <summary>
        /// A fork still holding, line for line, the file it came from. Publishing it would put a
        /// second identical entry on the site under a new name.
        /// </summary>
        public bool ForkStillTheCopy;
    }

    /// <summary>
    /// What the server last said about this lineage — one cached answer, never a request.
    ///
    /// ⚠ <see cref="Role"/> is only an answer when <see cref="IsOwner"/> holds: the public
    /// endpoint answers about a translation and never about a person, and fills the role with
    /// what an anonymous caller can be told.
    /// </summary>
    public struct ServerFacts
    {
        /// <summary>The server has been asked at all — even to learn that it knows nothing.</summary>
        public bool Checked;

        /// <summary>Something of this lineage is on the site.</summary>
        public bool Exists;

        /// <summary>The account reading this holds a row in the lineage.</summary>
        public bool IsOwner;

        /// <summary>That row's role. Meaningful only when <see cref="IsOwner"/>.</summary>
        public LineageRole Role;

        /// <summary>The row's id on the site, to name it — "Update #123". Null when there is none.</summary>
        public int? SiteId;

        /// <summary>The published content's hash, to compare with the local one.</summary>
        public string? Hash;

        /// <summary>Who published the row the server described.</summary>
        public string? Uploader;

        /// <summary>Who leads the lineage, when the server names them apart from the uploader.</summary>
        public string? MainUsername;

        /// <summary>How many contributions exist on this Main, whatever their state.</summary>
        public int BranchesCount;

        /// <summary>How many of them hold something a merge would offer. Null on an older server.</summary>
        public int? BranchesWithWork;

        /// <summary>How many distinct lines those contributions hold. Null when not counted.</summary>
        public int? LinesAvailable;

        /// <summary>Whether the Main takes contributions. Null when unknown.</summary>
        public bool? AcceptsBranches;

        /// <summary>The Main this branch hangs from is gone. Null when unknown.</summary>
        public bool? MainMissing;

        /// <summary>The Main's owner erased their account. Null when unknown.</summary>
        public bool? MainAbandoned;

        /// <summary>The Main closed its contributions since this branch was sent. Null when unknown.</summary>
        public bool? BranchFrozen;

        /// <summary>The Main was told of this branch's work, came back, and took nothing in. Null when unknown.</summary>
        public bool? MainIgnoring;

        /// <summary>"in_progress" or "complete", as published. Null when unknown.</summary>
        public string? Status;
    }

    /// <summary>Under whose name this screen acts, and whether it can reach the site at all.</summary>
    public struct AccountFacts
    {
        /// <summary>An account is signed in on this screen.</summary>
        public bool SignedIn;

        /// <summary>The product may talk to the site at all.</summary>
        public bool Online;
    }

    /// <summary>What somebody may do from here, and why not when they may not.</summary>
    public static class Standings
    {
        /// <summary>
        /// Where somebody stands with a translation, composed from the facts — the one reading of
        /// them every screen shares.
        ///
        /// 🔴 **Written because one screen read the same facts twice** (2026-09-16). The mod's main
        /// screen derived a layout state from the server state, then rebuilt a Standing from the
        /// same state PLUS that layout state — two passes, two chances to disagree, and they did:
        /// the Standing it built never carried MainMissing, so the chip announcing a vanished Main
        /// never appeared in a game while the notice beside it, reading the server state directly,
        /// did. Every field is filled here or nowhere.
        ///
        /// ⚠ **The role is this account's role, or none.** The public endpoint answers about a
        /// translation and never about a person: read by somebody signed out, its "role" is what an
        /// anonymous caller can be told. So a role is taken only from an answer that concerns the
        /// reader — <see cref="ServerFacts.IsOwner"/> — and is None otherwise.
        ///
        /// ⚠ **What is waiting is asked only of a Main.** A branch has nobody waiting on it, and a
        /// count carried over from the lineage would send its author to review other people's
        /// work. Unknown is not zero: an older server that cannot say which contributions hold
        /// work gives the raw count rather than nothing, since "nothing waiting" is a claim.
        /// </summary>
        public static Standing From(LocalFacts local, ServerFacts server, AccountFacts account)
        {
            bool here = local.Lines > 0;
            var publication = Publications.Of(here, server.Exists, server.Exists ? server.IsOwner : (bool?)null);
            var role = server.IsOwner ? server.Role : LineageRole.None;
            bool leads = server.IsOwner && (role == LineageRole.Main || role == LineageRole.Fork);

            return new Standing
            {
                Publication = publication,

                // Nothing published to compare against is not "in sync": it is no comparison at all.
                Sync = server.Exists
                    ? Sync.Decide(local.ContentHash ?? "", server.Hash ?? "", local.LastSyncedHash ?? "",
                                  local.LocalChanges > 0 || local.MetadataDirty)
                    : (SyncDirection?)null,

                Account = account.SignedIn ? AccountStanding.Ours : AccountStanding.Anonymous,
                Role = role,
                BranchesWaiting = leads ? (server.BranchesWithWork ?? server.BranchesCount) : (int?)null,
                LinesAvailable = leads ? server.LinesAvailable : null,
                MainMissing = server.MainMissing == true,
                MainAbandoned = server.MainAbandoned == true,
                BranchFrozen = server.BranchFrozen == true,
                Finished = server.Status == null
                    ? (bool?)null
                    : string.Equals(server.Status, "complete", System.StringComparison.OrdinalIgnoreCase),

                // Somebody else leads it: named when the server named them, the uploader otherwise.
                MainOwner = server.Exists && !server.IsOwner
                    ? (string.IsNullOrEmpty(server.MainUsername) ? server.Uploader : server.MainUsername)
                    : null,
            };
        }

        /// <summary>
        /// This account leads the lineage — a Main, or a Fork, which is a Main that left another.
        /// What a screen used to call "owner of a Main": the state that reviews contributions.
        /// </summary>
        public static bool LeadsTheLineage(Standing standing)
        {
            return standing.Publication == Publication.Published
                   && (standing.Role == LineageRole.Main || standing.Role == LineageRole.Fork);
        }

        /// <summary>This account holds a contribution to somebody else's Main — it has sent something.</summary>
        public static bool OnABranch(Standing standing)
        {
            return standing.Publication == Publication.Published && standing.Role == LineageRole.Branch;
        }

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
