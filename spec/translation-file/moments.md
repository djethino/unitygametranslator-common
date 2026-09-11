# The moments — what a Core writes beside the lines, and when

`schema.json` says what a translation file looks like. It cannot say **when** each `_` key moves,
and that is where a second Core would go wrong first: `Sync.Decide` (the socle) takes
`_source.hash`, the ancestor and `_local_changes` as facts and renders a verdict that is exactly as
right as those facts. Written 2026-09-11 from the code; every sentence names the field it touches
and the file that implements it today.

The three facts, once:

| fact | where it lives | what it answers |
|---|---|---|
| `_source.hash` | in the file | the server version this machine has SEEN — downloaded, uploaded or merged from |
| `_source.site_id` | in the file | which row on the site; what lets a mod with nobody signed in ask the public `check` |
| `_local_changes` | in the file | how many lines differ from the ancestor — what still needs publishing |
| the ancestor | `translations.json.ancestor` beside the file | what the two sides last AGREED on — the base of the next 3-way merge |
| `.mainancestor` | beside the file, branches only | the Main as last merged from; empty = additive, never replaced by `.ancestor` except for a non-branch at its first merge |

## After a download (the server's file replaces the local one)

- the file is the server's, verbatim, LF-normalised;
- `_source.hash` ← the server's `file_hash`; `_source.site_id` ← the row's id;
- `_local_changes` ← **0**: nothing has been changed locally yet. Leaving a count inherited from
  whoever uploaded it would make the mod believe the player had edits they never made, and offer
  to merge them;
- the ancestor ← the server's content (the same file).
- Manager: `TranslationInstaller.StampSource` (`_source`, `_local_changes`) — the ancestor is
  written beside. Mod: `TranslatorUIManager.DownloadUpdate` → `SaveAncestorCache`, `LastSyncedHash`.

## After a successful upload

- `_source.hash` ← the hash the site answered (`UploadResponse.translation.file_hash`);
  `_source.site_id` ← the id answered;
- the ancestor ← the local file as sent;
- `_local_changes` ← 0.
- Mod: `UploadPanel` → `SaveAncestorCache()`, `LastSyncedHash`. Manager: `TranslationPublisher`.

## After a merge (the server's version taken into the local one)

🔴 **A merge is three facts, and getting the other two wrong is worse than not merging at all**:
the next comparison would be computed against a baseline that never existed, inventing conflicts
or hiding them.

- the file ← the merged lines;
- `_source.hash` ← the PUBLISHED version's hash: the version we have now seen, whether or not we
  kept all of it;
- the ancestor ← the PUBLISHED content, **never the merged one**. The ancestor answers "what did
  the two sides last agree on", and what they last agreed on is what was published. Writing the
  merged file there would make every line just kept look like common ground, so the next merge
  would silently drop them;
- `_local_changes` ← what the merged file has that the published one does not — precisely what
  still needs publishing.
- Manager: `TranslationInstaller.WriteMerged` + `StampMerged`. Mod: `DownloadForMerge` →
  `SaveAncestorFromRemote`. ⚠ The mod's wizard path (`DownloadAndMerge`, path C of
  `analyse/sync-paths-audit.md`) still goes through the legacy merger.

## At every write

- `_local_changes` ← recomputed against the ancestor, never carried from memory: the count went
  stale on disk once and it took an outside reader (the Manager) to notice.
- Mod: `TranslatorCore.SaveCache`.

## At a fork (the file leaves its lineage)

- `_uuid` ← a new GUID; `_forked_from` ← `{site_id, hash, lines}` of the origin, filled BEFORE the
  reset; `_source` ← emptied; `_local_changes` ← every line (all of it is now local);
- the ancestors (`.ancestor`, `.mainancestor`) ← **deleted**: a lineage left has no common ground
  with the one joined. ⚠ This is the moment that did not happen for months
  (`ClearAncestorCache` looked for `.ancestor.json`) — `CompanionFiles.DeleteAncestors` holds it now.
- Mod: `TranslatorCore.CreateFork` → `CompanionFiles.DeleteAncestors`. Manager: never (forking is
  decided in the game).

## At a restore (a backup replaces the file)

- everything the file states about itself is RE-DERIVED from the restored file — `_source`,
  `_local_changes`, the six settings sections, the fonts, the screens — never inherited from the
  file that was loaded before. The rule, in the user's words (2026-09-08): *"chaque changement de
  traduction, que ce soit un download, une fusion vers le local ou un restore doit réappliquer
  toute la chaîne"*.
- Mod: `LoadedFile.Read` (a value for every field) and the reload chain.

## What holds each moment today

| moment | held by | how |
|---|---|---|
| a file states everything about itself, nothing inherited | `LoadedFileChecks`, `LoadedIdentityChecks` (mod) | `LoadedFile.Read` on documents; a lexical check that `LoadCache` assigns every field |
| a reload re-runs the whole chain | `ReloadChainChecks` (mod) | lexical, on the reload path |
| the fork deletes the ancestors, by their real names | `CompanionFilesChecks` (mod) | on a real folder |
| the sync verdict from the three facts | `SyncChecks` (socle) | `Sync.Decide` cases |
| the moments themselves, as sequences (mod) | `TranslationStoreChecks` over `Engine/TranslationStore` | `moments.json` replayed on a real folder: download, edit, remove, merge, upload, main merge, fork, write, load — 12 cases |
| the merge's three facts and the download stamp (Manager) | ⚠ **not yet**: `moments.json` holds 2 cases for the Manager (`held_by: manager`), the executor over `TranslationInstaller.Install` / `WriteMerged` is still to write | — |

⚠ Since 2026-09-12 the mod's side is closed: the stamps and the ancestors live in
`Engine/TranslationStore.cs`, one call per moment, and `TranslatorCore` keeps its old names as a
façade over it. The Manager's `StampSource` / `StampMerged` are held by the same cases once its
executor exists (`TODO.md`).
