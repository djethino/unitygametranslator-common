# The game's lists: joining their entries (`join`, `join_case`)

A game draws some texts from its own lists: a hero's name is a family name from one list and a given name from another
(`[!L*familyName][!L*given*]`). When no text of the game holds the entries a screen shows, the mod writes the name itself
by **joining the entries**. Held by `corpus/rules/list_join.json` (`ListJoin`).

## What the file says

```json
"_lists": { "lists": { "given": { "values": ["土", "全"], "join": "", "join_case": "lower-first" } } }
```

- **`join`** is a TEXT: what is written between two entries (`""`, `" "`, `"-"`). It stays a string for every reader — a typed
  reader (`(string)o["join"]`, `GetString()`) throws on an object and would lose every list of the section, so the case is NOT
  folded into it.
- **`join_case`** (optional) is the case of the first letter of each entry **after the first**: `lower-first` or `upper-first`,
  the vocabulary of a link's `case` (`LinkReading`). Absent = every entry keeps its own first letter. « Lao Tuquan », not
  « Lao TuQuan »: measured on a game whose 1 018 composed given names would otherwise show a capital in the middle.
- Outside the content hash, like all of `_lists`.

## The rule (`ListJoin.Join(entries, between, joinCase)`)

- The first entry is kept as written; each following one is written after `between`, its first letter set by `join_case`.
- **The case is `Casing`'s**: the first code point only, Unicode's data, never the runtime's (`ToLowerInvariant(piece[0])` cuts a
  letter outside the basic plane in two; `ß` has no capital and takes its titlecase). A first character with no case stays.
- An **empty or absent entry gives nothing**, neither text nor separator, and does not count as the first.
- `between` absent is no text.

## Readers and writers

- A **writer** writes one of the two values or leaves the field out (`ListJoin.IsCase`).
- A **reader** never refuses: a value it does not know (a newer writer's) is read as absent, the capitals stay. An older reader
  that ignores the field writes the name with capitals: wrong, never broken.
