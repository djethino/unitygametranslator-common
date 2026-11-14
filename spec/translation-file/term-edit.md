# The window of a term: `TermEdit`

What a person does in the window of a term — its word and its written forms, or a term made from two selections — written once, with the
cases the site's editors and the mod replay (`corpus/rules/term_edit.json`). Pure: nothing is written until `write`; every act answers a
NEW draft (`TermDraft`), as `LinkEditing.Step` answers a new `EditState`. The page draws; this decides. The lines of the term's list, and
what the term does to them, are a later rule (`term-lines`).

A draft: `{key, word, word_touched, forms:[{id, text, added}], selected, is_new, becomes, original:{v, forms}}`.

- **`open`** — a window exists for a line of a file of the format 2 only. **`open_new`** — a term made from the words selected in the source
  (the key) and in the translation (the word). 🔴 The key is very often a line the file ALREADY holds (the name of an object is its own
  line): it is not made twice — that line **becomes** a term, its translation is the word proposed, its forms come along. **`set_key`** —
  a key typed again looks the line up again; a word the person wrote stays, and so do the forms made in the window.
- **`add_form`** gives the next free number `fN` above the forms of the window AND of the line as it was: a number is given once, and never
  again while the window remembers it.
- **`blockers`** — a form a sentence cites (its link names it) or the term's own `use` of a template names cannot be removed, and the
  window says which before the click. A form made in the window is needed by nobody.
- **`changes`** / **`count`** — what changed, one entry per word or form, in this order: `term` (a term made is ONE act, its word is part of
  it unless the line was there), `word`, then the forms of the window (`form-text`, `form-added`), then the forms gone (`form-removed`). The
  N of `Apply (N)`: nothing is written until then.
- **`problem`** — why Apply cannot write: `empty-key`, `bad-key` (a name of the file, a template, a text holding a marker),
  `key-is-term` (that window is opened, not made again), `cites` (a term does not cite another term, `LineWrites`: a line that holds links
  or `[!g*` cannot become one), `empty-word`, `empty-form`. In this order.
- **`after`** / **`effect`** — the line as it would be (the other fields untouched, no `forms` member when none is left, `term` for a term
  made), and what the change does, said BEFORE: `TermChanges.Impact` on the file as it stands with that line laid over — how many texts change
  (`texts`) and how many sentences are to be read again (`review`). Never a count of the window's own.
- **`write`** — the one act: the term's own line and nothing else, or `null` when nothing changed. **A changed word is a person's writing
  (`H`)**; a change of forms alone keeps the tag the line has and the word it had. A key written is trimmed. No sentence is written: they read
  the term through their link, and a link a person made becomes « to read again » (its `was` still names the words read when it was set).
