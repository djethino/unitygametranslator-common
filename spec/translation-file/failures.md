# The failures file — the lines the AI gave up on, kept beside the translation

Written 2026-11-14 from the code (`FailureStore`, `FailureLedger`, `TranslatorCore.LoadFailures`);
it states what the mod does today and changes nothing. `failures.json` holds the cases.

## What it is

- A **companion file**, never a key of the translation: `<translation path>.failures`
  (`TranslationFiles.FailuresOf`, the suffix is APPENDED, so the file never ends in `.json` and is
  never mistaken for a translation).
- Local to the machine. **Nothing uploads it**: an upload sends the content of the translation, and
  this file is not part of it. It holds the AI's refused answers, so it must never travel.
- Written and read by the mod alone (`FailureStore`). The Manager knows its NAME only, to sweep it
  with the other files of a game (`translations.json.` prefix) and never reads it. The site does
  not know it.
- Kept so that a line the AI gave up on is not asked again at every launch. It stays on the
  Failures tab until somebody settles it.

## Shape

```json
{ "lines": [
    { "key": "Play",
      "source": "Play",
      "elements": ["Canvas/Menu/PlayButton"],
      "attempts": [ { "value": "…", "errors": ["…"] } ] } ] }
```

| field | meaning | when missing or wrong |
|---|---|---|
| `lines` | the failed lines, in the order they were noted | not an array: no line |
| `key` | the line's key in the translation | not a non-empty string: the entry is ignored |
| `source` | the text that was asked, as the line keeps it | the key |
| `elements` | the paths of the game elements that showed the line | only non-empty strings are kept |
| `attempts` | what the AI answered, oldest first | no attempt |
| `attempts[].value` | the refused answer | empty text (an attempt with no answer: a server that refused) |
| `attempts[].errors` | why it was refused | only strings are kept |

- `errors` are **sentences, not codes**: those the model was told (`Placeholders.ModelSentence`,
  the markup and bracket refusals), or the text of a server or transport error, kept as it came.
  A file written by an older version holds older sentences: they are read and shown as they are.
- No text of any of them is in `common/spec`: a sentence is stored, never looked up.

## Reading

- A line noted twice (same key) is ONE line: the later replaces the earlier, keeps its place, and
  keeps the earlier one's `elements` when it has none of its own.
- A key the translation now holds a non-empty line for is **settled** and dropped at load: it was
  translated since (downloaded, restored, written by hand). A key whose line is empty stays.
- A file that is not JSON, or holds an attempt that is not an object, is **not read** (the load
  throws, the caller says so and goes on without it); the file is left as it is.
- A name the file does not list (at the top, in a line, in an attempt) is ignored when reading
  and **not kept** when the file is written again.

## Writing

- The whole file, or nothing (`AtomicFile`), indented. Every field above is written, in this order.
- An empty ledger **deletes** the file.

## What holds it

No port reads it, so the mod replays `failures.json` on a real folder (`FailureStoreChecks`).
`common` has no JSON dependency and no second reader: the tolerance above is not a pure rule over
values, it IS the reading, and moving it would copy `FailureStore` into the socle. It stays in
the mod. If the Manager ever shows a game's failures, it reads this document and the same cases.

Not checked by anything on the site: that nothing uploads this file rests on the upload sending the
translation's content (`openapi.json`, `POST /translations`), not on a test of its own.
