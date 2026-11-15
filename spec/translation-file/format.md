# The format of a translation file: who reads which, and who may send what

Written 2026-11-15, the day the socle passed to the format 2 (`TranslationFormat.Current = 2`, user: « le mod reste compatible v1 mais passe à la v2 »).
The cases are `corpus/rules/translation_format.json` and `uploads.json` (`format_wall`, `button`), and `spec/api-v1` (`format` of `check-uuid`).

## Three numbers, never mixed

| number | what it is | where it comes from |
|---|---|---|
| **a file's format** | what the file CONTAINS | `TranslationFormat.OfContent` (2 only when it holds a field of the format 2, or a `_lists` section), stamped as `_engine_version`. A file of the format 1 stays one until a field of the format 2 is written in it |
| **what a client reads** | the highest format it can use | its constant `Current` (2 for this socle), announced as `X-UGT-Format` (`?format=` on a stream). A client that says nothing reads the format 1: `Unannounced`, forever |
| **what a server keeps** | the highest format it stores, hashes and serves back whole | the field `format` of the answer that precedes every upload (`check-uuid`). A server that says nothing keeps the format 1: `TranslationFormat.ServerKeeps` |

`Current` is therefore never the format of a file nor the format of a dictionary that declares none, and it is not what a game with no mod
yet will read: the mod it will have is the one already published.

## Who refuses what

- A client never loads a file past what it reads, and SAYS so (« Made for a newer UGT Mod. Update UGT Mod to use it. »).
- A server never serves a client a file past what the client announced (409 `FormatTooNew`; not listed, not announced as an update).
- A client never SENDS a file past what the server keeps (`TranslationFormat.CanSend`): the button is closed before the click, with the
  wall `Walls.FormatNotKept` (« This server keeps format 1 only. This translation is format 2. It cannot be published here. It still works
  in the game. »). A server not asked yet has said nothing: no wall until it answered.
- A server never keeps what it cannot read: a file of a format past its own `KEEPS`, or a file stamped lower than what it contains, is refused.
- A tool that lays a file in a game (the Manager) refuses one past what the game's mod reads (`FormatGuard`); a game with no mod yet is held
  to `Unannounced`.

## The crossings, one by one

| crossing | what happens |
|---|---|
| mod v2 and a site that keeps 2 | everything holds: it announces 2, is served files of the format 2, and sends them |
| mod v2 and the site of today (silent) | the announcement is ignored (every file there is the format 1); a file of the format 1 is sent as it always was; a file of the format 2 is NOT sent: the wall says it |
| mod v2 and a player's file of the format 1 | stays the format 1 until a field of the format 2 is written; merged by its REAL format, never by `Current` |
| a file turned into the format 2 and a published mod (v1) | the published mod says nothing and reads the format 1: the Manager refuses to lay the file there; a mod from the rule's day on refuses to load it and says so |
| a mod v1 and a site that keeps 2 | the mod announces nothing (1): the site never serves it a file of the format 2 |
