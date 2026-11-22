# Editing a sentence: `LineEditing` (the whole conversation)

What the edit window of a sentence does, with no drawing: open the sentence, be told every change of the text, say what the preview
draws, take the acts on a passage, ask the question saving needs, and hand back the LINES to write. The rules underneath are
`LinkEditing`'s (`editing.md`, `corpus/rules/link_editing.json`); what any writer owes the file is `LineWrites`'. This file is the
conversation around them, held by `corpus/rules/line_edit.json` — a script of gestures replayed by the mod's checks and by the
editors' port (`website/resources/js/rules/line-edit.js`). Companion of `term-edit.md` (the window of a term) and `editing.md`.

## The state is the edit's, kept between gestures

`LineEdit` = the key, the line as the file holds it (null when the key is no line yet), the state at opening (`Original`), the state now
(`State`), the question asked. **It is never rebuilt from the text.** An editor that kept only the text and replayed it from the opening
at every call lost the answer to the first question when the second was asked — and asked the first again, for ever. The corpus
(`two-questions-keep-the-first-answer`, `three-questions-in-order`) replays two and three questions; a port that does not keep the state
fails them.

## Gestures

| gesture | rule |
|---|---|
| `Open(key, line, current, fileFormat)` | `current` is a line edited and waiting: the conversation resumes from it while « nothing changed » is judged against the file. A file of the format 1 opens too (no passage, no question: the same string comes out); an editor that has nothing to say about links just does not call it |
| `Type(edit, text)` | ONE change, exact (`LinkEditing.Step`). The same text is no change. A question that was open is closed: the words moved under it |
| `Preview` | the text, each passage (`index, start, length, source, needs_review, case_not_applied, pending, open, term, by, own`), the passages LOST with their reason (`Deleted`, `Straddled`, `EntryGone`: codes, never words), `damaged`, `pending` |
| `Unlink(passage)`, `LinkSelection(start, length, term, form)`, `MarkRead` | an act answers the new edit, or a problem code and the edit as it was |
| `Waiting` | what `Finish` would ask next, without writing: for an editor to know whether Save is blocked |
| `Answer(choice)` | `AllTexts` or `OnlyThisText`, to the question `Finish` asked (else the one `Waiting` names). Call `Finish` again |

## `Finish` — one of five

- `nothing` — nothing differs from what was shown (judged against the file, never against the last gesture).
- `problem` — the text cannot be saved as it stands (`text-has-link-marker`).
- `question` — answer it and finish again. One at a time; `edit` is the settled edit to keep.
- `refused` — **the file would not accept the write** (`LineWrites.Refusal`, on the file as it will be: the sentence AND the terms that
  gained a form). An editor never offers a save the file will refuse (« never open a path known to end in an error »); the server
  still asks. The format is that of the CONTENT: a sentence that holds a field of the format 2 makes the file one.
- `write` — the lines to write, all or none: the sentence first, then each term that gained a written form (one line each, however many forms).

**The sentence's tag.** A person's writing is `H` (`LineEditing.TagAfterPerson`) — except a line tagged `M` (the mod's own words) or `S`
(skipped), which stays what it is: every save of the site has always kept them (`EditSessionController`; `displayTag` of the page). A tag she
chose by hand is the caller's and wins. **A proposal of the AI she keeps as it came is `V`** (`LineEditing.TagOfWords(stored, written, proposal)`): she
saw the machine's words and accepted them, which is what the A → V gesture records (`A` is the AI nobody read, `H` would claim she wrote them; decided by the
user, 2026-11-22); the tag drives the quality score and what the community sees. Equality with the proposal,
to the letter, is the whole rule; `M` and `S` stay what they are. The same rule writes the word of a term (`TermEdit.Write`). A term that gained a form keeps its own
tag (`A` when it has none) and its own words.

**What a line carries.** The line ENTIRE: its words with the markers, and every other field it held (term mark, forms, use, class, oracle) —
`links` and `unlinked` replaced by what she left, each written only when it holds something. A form added with the id of one the term holds
replaces it, in place; a new id goes last.

A term that gained a form is read through the same lookup as the passage was a moment before: a lookup that answers differently twice is a
fault of the caller (the call throws), not a state to hide.

## `TermChoices(file, query, limit = 50)` — which term to link a selection to

The lines marked `term` whose key or words contain the query, **the case of a letter not counting but by `Names`** (Unicode's simple
mapping, one code point to one: never the runtime's — `toLowerCase`, `ToLowerInvariant` and `Compare` disagree on « İ » and on the final
sigma, and the corpus pins both). The query is trimmed (`Spaces`). `total` is how many there are, `items` the first `limit` in the
**ordinal order of the keys** (UTF-16 code units, the content hash's), each with its words and its forms: the list says it is cut.

## What each caller changes

- **The mod** — `LineWriter.Correct` becomes `Open` + `Type(clearText)` + `Finish`, the `LineEdit` kept by the panel between two calls
  (the answer carried by it, no more `EditAnswer?` as a parameter of a stateless call); the terms that grew come from `Finish`'s lines;
  `LineWorld` gives the file (`LineFile`: its format by content, `HasList`) and the lookup. `Check` and the capitals/`ToLine` shims go: the
  refusal is `Finish`'s. `LineWriter.Apply` and `Retranslated` do not move.
- **The site** — `line-edit.js` ports `LineEdit` (state `{key, line, fileFormat, original, state, question}`) and `finish`'s five kinds; its
  `tag: null` for the sentence becomes the socle's; `termLineOf` goes (one lookup); `termChoices` uses the Names fold and the ordinal order.
