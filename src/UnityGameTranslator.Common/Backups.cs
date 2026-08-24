using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// Why a copy of a translation was taken, when nobody asked for it.
    ///
    /// 🔴 **A closed list, and it is the most useful column of the whole screen.** Choosing between
    /// five dated copies is a lottery; choosing between "before installing somebody's translation"
    /// and "before writing a merge" is a decision. The act is known at the moment of writing and
    /// costs nothing to record — it was simply never recorded.
    /// </summary>
    public enum BackupReason
    {
        /// <summary>Somebody asked for this copy. No act caused it.</summary>
        Saved,

        /// <summary>A community translation was installed over the one that was here.</summary>
        Installed,

        /// <summary>The result of a merge was written.</summary>
        Merged,

        /// <summary>What came back from the browser editor was written.</summary>
        Edited,

        /// <summary>An update was downloaded from the site.</summary>
        Downloaded,

        /// <summary>The local translation was removed.</summary>
        Removed,

        /// <summary>Another copy was put back, and this was what stood there.</summary>
        Restored,

        /// <summary>
        /// Something replaced the file and did not say what. Never written on purpose: it is what
        /// a copy from an older version reads as, and what a caller that forgot to say gets.
        /// </summary>
        Unknown,
    }

    /// <summary>
    /// One copy of a translation, as a screen needs to describe it.
    ///
    /// ⚠ Everything here comes from the copy's own little `about` file, NOT from opening the
    /// translation beside it. Fifteen files of half a megabyte, parsed to draw a list, is a list
    /// that takes a second to appear in a game.
    /// </summary>
    public struct BackupEntry
    {
        /// <summary>Folder name, which is also the identity: `auto-20260817-143210`.</summary>
        public string Id;

        /// <summary>When it was taken. Local time, as the person experienced it.</summary>
        public DateTime At;

        public BackupReason Reason;

        /// <summary>Whose translation was involved, when the act involved somebody. May be null.</summary>
        public string? By;

        /// <summary>What the person called it. Only ever set on a saved copy.</summary>
        public string? Label;

        public int Lines;

        /// <summary>Of those, the ones a human wrote or settled. The figure that says "my work".</summary>
        public int ByHand;

        /// <summary>The lineage this copy belongs to — see <see cref="Backups.IsAnotherLineage"/>.</summary>
        public string? Uuid;

        /// <summary>Whether it carries the fonts and images the translation names.</summary>
        public bool WithAssets;

        /// <summary>
        /// Somebody decided this copy stays — it is out of the automatic rotation's reach.
        ///
        /// 🔴 **A second axis, and squeezing it into <see cref="Reason"/> broke both.** Why a copy
        /// was taken (before a merge, before a restore, before an install) and whether somebody
        /// decided to keep it are independent: the whole point of keeping one is that "before
        /// installing @somebody's translation" is exactly what makes it worth keeping, so the
        /// reason must survive. Both writers already understood that — they set `kept` in the
        /// description and leave `reason` alone — and both readers then rebuilt "is it kept" from
        /// `reason`, which by then said Merged. So keeping a copy worked, and the screen went on
        /// listing it as automatic with a Keep button that could only refuse.
        /// </summary>
        public bool Kept;

        /// <summary>
        /// Out of the rotation: kept by somebody, or taken by the Backup button in the first place.
        ///
        /// ⚠ Two ways in, deliberately. A copy taken on purpose carries the reason
        /// <see cref="BackupReason.Saved"/>; an automatic one somebody kept carries its own reason
        /// and this flag.
        /// </summary>
        public bool IsSaved => Kept || Reason == BackupReason.Saved;
    }

    /// <summary>
    /// How a translation's own history is kept — the same way in the mod and in the manager.
    ///
    /// 🔴 **Here because the mod exists without the manager**, today and after the manager ships.
    /// The mod is the only product present in every installation, so it owns the mechanism and the
    /// manager is a second window onto the same folders. What must never differ is the layout, the
    /// limits and the words: a copy taken in the game and read by the tool has to be the same thing.
    ///
    /// 🔴 **And because a write path forgot once already.** The mod's own comment, on the fourth
    /// place that replaces the file: *"This one overwrote a player's own work with a community
    /// version and left nothing behind — and the settings dialog was meanwhile promising a
    /// backup."* Nine call sites across two products each had to remember. The answer is not a
    /// longer list; it is that the copy belongs INSIDE the one write path, so a path added later
    /// cannot skip it.
    ///
    /// ⚠ **Decisions only, no file access and no serializer.** This library has no dependencies by
    /// design (see its csproj), and the two products disagree about their JSON. So the rules live
    /// here and the reading and writing lives in each product — exactly as <see cref="Badges"/>
    /// decides the chips and leaves the drawing alone.
    /// </summary>
    public static class Backups
    {
        /// <summary>The folder, inside the mod's own data folder for that game.</summary>
        public const string FolderName = "backups";

        /// <summary>What a copy's description file is called, inside its folder.</summary>
        public const string AboutFileName = "about.json";

        /// <summary>
        /// How many automatic copies are kept, oldest dropped.
        ///
        /// ⚠ **Five, not three.** One session can chain three replacements — install a community
        /// translation, merge it, then write back what the browser editor returned. At three, the
        /// state from BEFORE that session is already gone, which is precisely the one somebody
        /// reaches for when they realise the session went wrong.
        /// </summary>
        public const int AutomaticKept = 5;

        /// <summary>
        /// How many copies somebody may hold deliberately.
        ///
        /// 🔴 **Nothing is ever evicted from these.** Full means the button refuses and says so;
        /// making room is a decision, taken by the person who decided to keep them. Rotating them
        /// would delete work somebody explicitly chose to protect — the opposite of what they
        /// pressed the button for.
        /// </summary>
        public const int SavedKept = 10;

        /// <summary>Folders holding the assets a translation may name.</summary>
        public static readonly string[] AssetFolders = { "fonts", "images" };

        // ── Naming ────────────────────────────────────────────────────────

        /// <summary>
        /// The folder name for a new copy: `auto-20260817-143210` or `saved-20260817-143210`.
        ///
        /// ⚠ Sortable as text, and that is deliberate: the order on screen must not depend on a
        /// file timestamp, which a copy, a sync tool or an archive extraction rewrites.
        /// </summary>
        public static string NewId(BackupReason reason, DateTime at) =>
            (reason == BackupReason.Saved ? "saved-" : "auto-")
            + at.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Whether a folder name is one of ours, and which family it belongs to.</summary>
        public static bool IsBackupFolder(string name, out bool saved)
        {
            saved = false;
            if (string.IsNullOrEmpty(name)) return false;

            if (name.StartsWith("saved-", StringComparison.Ordinal)) { saved = true; return true; }
            return name.StartsWith("auto-", StringComparison.Ordinal);
        }

        // ── What goes into a copy ─────────────────────────────────────────

        /// <summary>
        /// The asset files a copy must carry, relative to the mod's data folder.
        ///
        /// 🔴 **Only what the translation NAMES.** A translation file lists the images it puts in
        /// place (`_images[].file`) and the fonts it asks for, so the set is computable — which is
        /// what makes a copy of "the translation and its assets" possible at all rather than a copy
        /// of the whole folder.
        ///
        /// ⚠ **Generated atlases are never included.** They are rebuilt on demand from the font
        /// beside them, and they are the largest thing in that folder. Keeping them would multiply
        /// the size of every copy for something no one can lose.
        ///
        /// ⚠ Parsing stays with the caller: both products already read that file with their own
        /// JSON library, and this one has neither.
        /// </summary>
        /// <param name="imageFiles">Values of `_images[].file`, in any order. Nulls tolerated.</param>
        /// <param name="fontFiles">Font FILE names the translation asks for. Nulls tolerated.</param>
        public static List<string> AssetsToCopy(IEnumerable<string?>? imageFiles,
                                                IEnumerable<string?>? fontFiles)
        {
            var chosen = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Take(IEnumerable<string?>? names, string folder)
            {
                if (names == null) return;

                foreach (var name in names)
                {
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    // ⚠ A bare file name, never a path. These come out of a translation file that
                    // anybody may edit, and a name walking up with ".." would have the copy reach
                    // outside the folder it is copying.
                    var bare = name!.Trim().Replace('\\', '/');
                    var slash = bare.LastIndexOf('/');
                    if (slash >= 0) bare = bare.Substring(slash + 1);

                    if (bare.Length == 0 || bare == "." || bare == "..") continue;

                    var relative = folder + "/" + bare;
                    if (seen.Add(relative)) chosen.Add(relative);
                }
            }

            Take(imageFiles, "images");
            Take(fontFiles, "fonts");

            return chosen;
        }

        // ── What rotates, and what refuses ────────────────────────────────

        /// <summary>
        /// Which automatic copies to drop, newest kept — the ids, oldest first.
        ///
        /// ⚠ Saved copies are not eligible and are not even looked at: they do not rotate.
        /// </summary>
        public static List<string> AutomaticToDrop(IEnumerable<BackupEntry> all)
        {
            var automatic = new List<BackupEntry>();
            foreach (var entry in all)
            {
                if (!entry.IsSaved) automatic.Add(entry);
            }

            automatic.Sort((a, b) => b.At.CompareTo(a.At));

            var drop = new List<string>();
            for (var i = AutomaticKept; i < automatic.Count; i++) drop.Add(automatic[i].Id);

            return drop;
        }

        /// <summary>
        /// Whether this automatic copy has already been kept.
        ///
        /// 🔴 **Because keeping one COPIES it.** The automatic row stays where it is and goes on
        /// rotating — that is the point, the two mechanisms are independent — so its Keep button
        /// stays on screen too, and pressing it a second time can only fail. A control that cannot
        /// succeed is disabled and says why; it does not wait to be pressed to refuse.
        ///
        /// ⚠ Matched on the moment, which is what the saved copy takes from the one it came from.
        /// Comparing ids would never match: they differ by the very prefix that distinguishes them.
        /// </summary>
        public static bool AlreadyKept(IEnumerable<BackupEntry> all, BackupEntry entry)
        {
            foreach (var other in all)
            {
                if (other.IsSaved && other.At == entry.At) return true;
            }

            return false;
        }

        /// <summary>Said when Keep can do nothing, so the button explains itself before refusing.</summary>
        public const string AlreadyKeptHint = "Already kept — it is in your backups.";

        /// <summary>How many of the deliberate slots are used.</summary>
        public static int SavedCount(IEnumerable<BackupEntry> all)
        {
            var count = 0;
            foreach (var entry in all)
            {
                if (entry.IsSaved) count++;
            }

            return count;
        }

        /// <summary>Whether another deliberate backup may be taken.</summary>
        public static bool CanSaveAnother(IEnumerable<BackupEntry> all) =>
            SavedCount(all) < SavedKept;

        /// <summary>
        /// Why the button is unavailable, or null when it is available.
        ///
        /// ⚠ Never a greyed control without words — the rule this project holds everywhere.
        /// </summary>
        public static string? WhyCannotSave(IEnumerable<BackupEntry> all) =>
            CanSaveAnother(all)
                ? null
                : $"{SavedKept} backups kept. Delete one to make room.";

        // ── Words ─────────────────────────────────────────────────────────

        // 🔴 **ONE word for the thing, and it is "backup". One for the act, and it is "restore".**
        //
        // This file first said "copy" — "Save a copy", "Put it back", "Delete this copy?" — beside
        // a screen called Backups, a folder called `backups` and a class called `Backups`. Two
        // vocabularies for one thing, and the invented one said nothing: a copy OF WHAT, put back
        // WHERE. Somebody reading this in their fourth language has to work out on their own that
        // the two words mean the same thing, with nothing on the screen saying so.
        //
        // ⚠ And the pair is not decorative. "Backup" is the noun every program has used for thirty
        // years and "restore" is its verb. Reaching for a fresher phrase swaps a word that needs no
        // explaining for one that does.
        //
        // ⚠ **The screen carries the subject so the buttons do not repeat it**: it is named
        // "Translation backups", therefore the verb is `Backup`, never `Backup translation`. See
        // .claude/rules/name-things-in-ui.md — a button is a verb, not a sentence.

        /// <summary>
        /// What a row says about itself, in one line.
        ///
        /// 🔴 Plain international English: the mod and the manager ship no translations, so this is
        /// read as it stands by somebody whose fourth language it is. "Before installing a
        /// community translation", not "pre-install snapshot".
        /// </summary>
        /// <param name="by">Whose translation the act involved, already written as a mention.</param>
        public static string Describe(BackupReason reason, string? by = null)
        {
            var who = string.IsNullOrWhiteSpace(by) ? null : by!.Trim();

            switch (reason)
            {
                case BackupReason.Saved:
                    return "You asked for it";

                case BackupReason.Installed:
                    return who is null
                        ? "Before installing a community translation"
                        : "Before installing " + who + "'s translation";

                case BackupReason.Merged:
                    return "Before writing a merge";

                case BackupReason.Edited:
                    return "Before writing what came back from the browser";

                case BackupReason.Downloaded:
                    return who is null
                        ? "Before a downloaded update"
                        : "Before an update from " + who;

                case BackupReason.Removed:
                    return "When the translation was removed";

                case BackupReason.Restored:
                    return "Before another backup was restored";

                default:
                    return "Before something replaced the translation";
            }
        }

        /// <summary>
        /// The name of the screen, in both products — and the reason its buttons read `Backup` and
        /// `Restore` with nothing after them: the subject is written once, up here.
        /// </summary>
        public const string ScreenTitle = "Translation backups";

        /// <summary>Headings, so the two lists are named identically in both products.</summary>
        public const string SavedHeading = "Your backups";

        public const string AutomaticHeading = "Automatic backups";

        /// <summary>Said beside the automatic list, so nobody counts on one staying.</summary>
        public const string AutomaticNote = "the oldest goes on its own";

        /// <summary>
        /// Said at the top of the list. Copies never leave the game folder, and somebody looking at
        /// a list of their own work deserves to know that before they wonder.
        /// </summary>
        public const string PrivacyNote =
            "Nothing here is sent anywhere. Backups stay in this game's folder.";

        // ── The one mistake that cannot be undone ─────────────────────────

        /// <summary>
        /// Whether this copy belongs to a DIFFERENT translation from the one in place.
        ///
        /// 🔴 **The only restore that cannot be taken back with another click.** Putting back an
        /// earlier version of your own work costs you the newer version, which is itself kept. But
        /// restoring a copy of somebody else's translation replaces yours with a file that shares
        /// nothing with it — a different lineage, different lines, and the merge base to match.
        /// It has to be said on the row, beside the line count, not in small print.
        ///
        /// ⚠ False when either side is unknown: a copy from before uuids were recorded is not
        /// evidence of anything, and warning on silence teaches people to ignore the warning.
        /// </summary>
        public static bool IsAnotherLineage(string? backupUuid, string? currentUuid)
        {
            if (string.IsNullOrWhiteSpace(backupUuid) || string.IsNullOrWhiteSpace(currentUuid))
                return false;

            return !string.Equals(backupUuid!.Trim(), currentUuid!.Trim(),
                                  StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Said on such a row, and it names what it is rather than shouting.</summary>
        public const string AnotherLineageNote = "a different translation, not an earlier yours";

        // ── What is asked before an act that cannot be taken back ─────────

        /// <summary>
        /// The two questions asked before acting, worded HERE so the two products cannot drift.
        ///
        /// 🔴 One of them asked and the other did not: the mod confirmed a deletion, the manager
        /// removed the copy on the click. Two screens onto the same folder, disagreeing about
        /// whether losing work deserves a question — and the one that did not ask was the one
        /// where a mis-click is cheapest to make and dearest to notice.
        ///
        /// ⚠ Both name what stands to be lost IN FIGURES, which is the rule every confirmation in
        /// this project follows: "are you sure?" is a question nobody can answer usefully, because
        /// the person asking already knows what is at stake and the person answering does not.
        /// </summary>
        public const string ConfirmRestoreTitle = "Restore this backup?";

        /// <param name="lines">What the copy holds.</param>
        /// <param name="nowLines">What the game holds today — the thing being replaced.</param>
        /// <param name="when">When the copy was taken, already written the way the row writes it.</param>
        /// <param name="anotherLineage">Whether it belongs to a different translation entirely.</param>
        public static string ConfirmRestoreBody(int lines, int nowLines, string when,
                                                bool anotherLineage)
        {
            var body = $"This game will use the {lines}-line translation from {when} instead of "
                     + $"the {nowLines} lines it holds now.\n\nWhat it holds now is backed up "
                     + "first, so you can come back to it.";

            // ⚠ Repeated here even though the row already says it: a confirmation is read by
            // somebody who has decided, and this is the last moment the decision can change.
            if (anotherLineage)
            {
                body += "\n\n⚠ This backup is " + AnotherLineageNote
                      + ". Its lines and its history are not yours.";
            }

            return body;
        }

        public const string ConfirmRestoreVerb = "Restore";

        public const string ConfirmDeleteTitle = "Delete this backup?";

        /// <param name="what">The name it carries, or a phrase naming its date. Never blank.</param>
        public static string ConfirmDeleteBody(string what, int lines) =>
            $"This deletes the backup {what} and the {lines} lines it holds. It cannot be undone."
            + "\n\nNothing else is touched: the translation in the game stays exactly as it is.";

        public const string ConfirmDeleteVerb = "Delete";
    }
}
