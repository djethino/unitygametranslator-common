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

## One entry, two lists: `use["T:LIST"]`

An entry is ONE line, whatever the number of lists it stands in (the line's key is its text): its forms are one set of ids, `use` and
`class` are read by template. The same character can be a family name in one slot of a template and a given name in the next
(`[!L*familyName][!L*GenerateRandomHeroGivenName*]`: « Bo » and « Bai »). A key of `use` per template could not say it — it had to
choose one slot's form for both. Measured on one game: 815 of its 3 185 entries stand in several lists; the template that reads two lists
that share entries has 176 of them, and the two slots want the same form for 175.

```json
"柏": { "v": "Bo", "t": "A", "forms": { "f1": "Bai" },
        "use": { "tc701839d3afe": "0", "tc701839d3afe:GenerateRandomHeroGivenName": "f1" } }
```

- A key of `use` is a template's id, or the id, `:` and a list's name. **`:` splits unambiguously**: an id is `^t[0-9a-f]{12}$`
  (`schema.json`, `$defs/templateId`) and a list's name holds none of `] [ : = *` (the grammar of a slot, `x-markers.closedSlot`).
- **A slot of list L of template T reads `use["T:L"]`, else `use["T"]`, else the entry's own `v`** (`TemplateUses.For`). A file with no key
  that has a list answers exactly as before, byte for byte (the content hash only sorts the keys: `T` before `T:L`).
- **The list must be one `_lists` holds** (`slot-list-missing`): a key for a list with no slot could never be read.
- What the change of a term does to a template (`TermChanges.Impact`) reads the forms of EVERY list of the template that holds the term;
  a form a `T:L` key names is a form a template takes (`TermEdit.Blockers`: it cannot be removed).
- **A reader that does not know it** reads `T` (or `v`): the wrong form in that one slot, never a file refused. The writer of such a key (the
  extraction) writes the whole-template key too, as the form the other slots take.
- Not measured, so not said: an entry that stands twice in the SAME list of one template with two forms. No template of the one game that
  has lists names a list twice with a `use`.
