using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// How a translation's own history is kept, checked against the rule rather than the code.
    ///
    /// ⚠ The stake: this is the last copy of work that exists nowhere else. A rotation that drops
    /// one too many, a deliberate copy evicted to make room, or a restore that silently swaps one
    /// lineage for another are all losses nobody can undo — and none of the three would throw.
    /// </summary>
    internal static class BackupsChecks
    {
        private static BackupEntry Auto(string stamp) => new BackupEntry
        {
            Id = "auto-" + stamp,
            At = DateTime.ParseExact(stamp, "yyyyMMdd-HHmmss",
                                     System.Globalization.CultureInfo.InvariantCulture),
            Reason = BackupReason.Installed,
        };

        private static BackupEntry Saved(string stamp) => new BackupEntry
        {
            Id = "saved-" + stamp,
            At = DateTime.ParseExact(stamp, "yyyyMMdd-HHmmss",
                                     System.Globalization.CultureInfo.InvariantCulture),
            Reason = BackupReason.Saved,
        };

        public static void Run(Action<bool, string, string> check)
        {
            // ── Naming ────────────────────────────────────────────────────
            var at = new DateTime(2026, 8, 17, 14, 32, 10);

            check(Backups.NewId(BackupReason.Saved, at) == "saved-20260817-143210"
                  && Backups.NewId(BackupReason.Installed, at) == "auto-20260817-143210",
                "the family is in the folder name",
                "a reader of the folder, and a rotation, must tell them apart without opening anything");

            check(string.CompareOrdinal(Backups.NewId(BackupReason.Saved, at.AddSeconds(-1)),
                                        Backups.NewId(BackupReason.Saved, at)) < 0,
                "names sort in the order they were taken",
                "ordering on a file timestamp breaks the day something copies the folder");

            check(Backups.IsBackupFolder("auto-20260817-143210", out var savedA) && !savedA
                  && Backups.IsBackupFolder("saved-20260817-143210", out var savedB) && savedB
                  && !Backups.IsBackupFolder("fonts", out _)
                  && !Backups.IsBackupFolder("", out _),
                "only our own folders are recognised",
                "sweeping a folder we did not create is how somebody's own files disappear");

            // ── Rotation ──────────────────────────────────────────────────
            var five = new List<BackupEntry>
            {
                Auto("20260817-100000"), Auto("20260817-110000"), Auto("20260817-120000"),
                Auto("20260817-130000"), Auto("20260817-140000"),
            };

            check(Backups.AutomaticToDrop(five).Count == 0,
                $"{Backups.AutomaticKept} automatic copies drop nothing",
                "the limit is a ceiling, not a target");

            var six = new List<BackupEntry>(five) { Auto("20260817-150000") };
            var dropped = Backups.AutomaticToDrop(six);

            check(dropped.Count == 1 && dropped[0] == "auto-20260817-100000",
                "the sixth drops the OLDEST, not the newest",
                "dropping the wrong end would keep the copy nobody wants and lose the one they do");

            // 🔴 The rule that protects work somebody chose to protect.
            var mixed = new List<BackupEntry>(six);
            for (var i = 0; i < 12; i++) mixed.Add(Saved("202608" + (10 + i) + "-090000"));

            var droppedMixed = Backups.AutomaticToDrop(mixed);
            var touchedSaved = false;
            foreach (var id in droppedMixed)
            {
                if (id.StartsWith("saved-", StringComparison.Ordinal)) touchedSaved = true;
            }

            check(!touchedSaved,
                "rotation never touches a copy somebody saved",
                "even twelve of them, past the limit: evicting one is deleting what they protected");

            // ── The ceiling refuses rather than evicts ────────────────────
            var full = new List<BackupEntry>();
            for (var i = 0; i < Backups.SavedKept; i++) full.Add(Saved("202608" + (10 + i) + "-090000"));

            check(!Backups.CanSaveAnother(full) && Backups.WhyCannotSave(full) is { Length: > 0 },
                "full means refused, with a reason in words",
                "a greyed button without words is a dead end; a silent eviction is worse");

            check(Backups.CanSaveAnother(five) && Backups.WhyCannotSave(five) is null,
                "automatic copies do not fill the deliberate slots",
                "the two families are counted apart or the ceiling arrives early");

            // ── What a copy carries ───────────────────────────────────────
            var assets = Backups.AssetsToCopy(new[] { "title.png", (string?)null, "  " },
                                              new[] { "arial.ttf" });

            check(assets.Count == 2 && assets.Contains("images/title.png")
                  && assets.Contains("fonts/arial.ttf"),
                "only the assets the translation names, in their own folders",
                "copying the whole folder would carry the generated atlases, the largest thing in it");

            var escaping = Backups.AssetsToCopy(new[] { "../../config.json", "sub/dir/x.png" }, null);

            check(escaping.Count == 2
                  && escaping.Contains("images/config.json") && escaping.Contains("images/x.png"),
                "a name that walks up is reduced to its file name",
                "these come out of a file anybody may edit; a path would let a copy reach outside");

            var twice = Backups.AssetsToCopy(new[] { "a.png", "A.PNG" }, null);
            check(twice.Count == 1, "the same asset is taken once", "case differs, the file does not");

            // ── Words ─────────────────────────────────────────────────────
            check(Backups.Describe(BackupReason.Installed, "@Seniorito")
                      .Contains("@Seniorito"),
                "the act names whose translation was involved",
                "'before installing a translation' is a date with extra steps; a name is a memory");

            check(Backups.Describe(BackupReason.Installed) != Backups.Describe(BackupReason.Merged)
                  && Backups.Describe(BackupReason.Unknown) is { Length: > 0 },
                "every act reads differently, and the unknown one still says something",
                "two rows that read alike are two rows nobody can choose between");

            // ── The mistake that cannot be undone ─────────────────────────
            check(Backups.IsAnotherLineage("aaa", "bbb"),
                "a copy from another lineage is flagged",
                "restoring it replaces your work with a file that shares nothing with it");

            check(!Backups.IsAnotherLineage("aaa", "AAA"),
                "the same lineage in another case is not another lineage",
                "a uuid echoed back differently would accuse every copy of being foreign");

            check(!Backups.IsAnotherLineage(null, "bbb") && !Backups.IsAnotherLineage("aaa", null),
                "an unknown uuid accuses nobody",
                "warning on silence teaches people to ignore the warning");

            // ── What is asked before acting ───────────────────────────────
            var restore = Backups.ConfirmRestoreBody(3227, 3180, "17 Aug 14:32", false);

            check(restore.Contains("3227") && restore.Contains("3180"),
                "the restore question names both sides in figures",
                "'are you sure?' cannot be answered: the asker knows the stake, the answerer does not");

            check(restore.Contains("backed up"),
                "and says the current state survives it",
                "the difference between a decision and a gamble");

            check(!restore.Contains(Backups.AnotherLineageNote)
                  && Backups.ConfirmRestoreBody(1, 1, "x", true).Contains(Backups.AnotherLineageNote),
                "the foreign-lineage warning appears only when it applies",
                "a warning shown always is a warning nobody reads");

            var delete = Backups.ConfirmDeleteBody("\"before the AI pass\"", 3227);

            check(delete.Contains("3227") && delete.Contains("cannot be undone"),
                "the delete question names the loss and that it is final",
                "this is the one act here nothing puts back");

            check(delete.Contains("stays exactly as it is"),
                "and says the game is not touched",
                "somebody deleting a backup must not fear for the translation they are playing");

            // ── One word for the thing, one for the act ───────────────────
            //
            // 🔴 Checked rather than merely written down, because this is the drift that happened:
            // a screen called Backups whose buttons said "Save a copy" and "Put it back". Somebody
            // reading it in their fourth language had to work out on their own that "copy" and
            // "backup" were the same thing, with nothing on the screen saying so.
            var everyPhrase = new[]
            {
                Backups.ScreenTitle, Backups.SavedHeading, Backups.AutomaticHeading,
                Backups.AutomaticNote, Backups.PrivacyNote, Backups.AnotherLineageNote,
                Backups.ConfirmRestoreTitle, Backups.ConfirmRestoreVerb,
                Backups.ConfirmDeleteTitle, Backups.ConfirmDeleteVerb,
                restore, delete, Backups.WhyCannotSave(Full()) ?? "",
                Backups.Describe(BackupReason.Saved), Backups.Describe(BackupReason.Restored),
                Backups.Describe(BackupReason.Installed, "@someone"),
                Backups.Describe(BackupReason.Unknown),
            };

            check(!everyPhrase.Any(p => p.IndexOf("copy", StringComparison.OrdinalIgnoreCase) >= 0
                                     || p.IndexOf("copies", StringComparison.OrdinalIgnoreCase) >= 0),
                "nothing shown to anybody calls a backup a \"copy\"",
                "two words for one thing, and the invented one says neither what nor where");

            check(Backups.ConfirmRestoreVerb == "Restore",
                "the verb for putting one back is the one every program uses",
                "\"Put it back\" is fresher and needs explaining; \"Restore\" needs none");

            check(!Backups.ConfirmRestoreVerb.Contains(" ") && !Backups.ConfirmDeleteVerb.Contains(" "),
                "and both verbs are one word",
                "a button is a verb, not a sentence — the screen already names the subject");

            // ── Kept is not a reason ──────────────────────────────────────────
            //
            // 🔴 An automatic copy somebody decided to keep carries BOTH: why it was taken, and
            // the fact that it stays. Both writers understood that and wrote `kept` beside the
            // reason; both readers then rebuilt "is it kept" from the reason, which by then said
            // Merged. So keeping worked, the screen went on listing the copy as automatic, and its
            // Keep button could only refuse.
            var keptMerge = Auto("20260824-201908");
            keptMerge.Reason = BackupReason.Merged;
            keptMerge.Kept = true;

            check(keptMerge.IsSaved,
                "an automatic copy somebody kept reads as saved",
                "the reason it was taken must survive being kept, so it cannot carry the answer");

            check(keptMerge.Reason == BackupReason.Merged,
                "and it still says WHY it was taken",
                "\"before writing a merge\" is precisely what makes a copy worth keeping");

            check(Saved("20260824-120000").IsSaved,
                "a copy taken by the Backup button is saved too",
                "two ways in: a deliberate copy, and an automatic one somebody kept");

            // ⚠ Kept means out of the rotation, whatever the reason says.
            var rotation = new List<BackupEntry>();
            for (var i = 0; i < Backups.AutomaticKept + 2; i++)
                rotation.Add(Auto("2026081" + i + "-120000"));
            rotation.Add(keptMerge);

            check(!Backups.AutomaticToDrop(rotation).Contains(keptMerge.Id),
                "and it no longer rotates",
                "keeping a copy that the next automatic one deletes keeps nothing");

            // ── Keeping COPIES, so the button must know it has already been used ──
            var both = new List<BackupEntry> { Auto("20260824-201908"), Saved("20260824-201908") };

            check(Backups.AlreadyKept(both, both[0]),
                "an automatic copy whose moment is already in the saved list is already kept",
                "the row stays after being kept, so its button must stop offering what will fail");

            check(!Backups.AlreadyKept(both, Auto("20260824-202155")),
                "and another moment is not",
                "matching on anything looser would disable buttons that would have worked");

            check(Backups.AlreadyKeptHint.Trim().Length > 0,
                "and the disabled button says why",
                "a control greyed out with no reason reads as a decision somebody else took");
        }

        /// <summary>A full saved list, so the refusal sentence can be read.</summary>
        private static List<BackupEntry> Full()
        {
            var all = new List<BackupEntry>();
            for (var i = 0; i < Backups.SavedKept; i++)
                all.Add(new BackupEntry { Id = "saved-" + i, Reason = BackupReason.Saved });

            return all;
        }
    }
}
