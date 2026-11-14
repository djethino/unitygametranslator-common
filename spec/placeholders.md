# Placeholders and bracketed labels: where the lists come from

Held by `corpus/rules/placeholders.json` (`Placeholders`). This file says what the rule counts and WHY — and what it has never looked at.

## Why square brackets and braces, and nothing else

`Placeholders.Accepts` counts `[`, `]`, `{` and `}` in the answer against the source, in addition to the tokens themselves. Where that comes from:

- **Written in the mod, merged here** (`d4d0c23`, 2026-08-11: « same pattern, same expansion over brackets »): the mod and the Manager's bench each had the rule; this is that agreement.
- **Measured on a real file** (`analyse/placeholder-corruption-analysis.md`): our tokens are `[!v*N]`, and the game's numbers inside `{…}` or `(…)` became `{[!v*0]}`, `([{[!v*0]}])`. The small local models added, doubled or swapped the brackets around the token
  (`[![!v*1]]`, `{[!v*0}]`), and the game then showed `([{42}])` or the raw placeholder. The count of the four characters is what catches a wrapper the token itself does not (« `[{[!v*0]}]` contains `[!v*0]` and is still wrong »).
- So the list is **the characters our own tokens and the game's usual format strings are made of** (`[!…]`, `{0}`). It is NOT a list of the brackets of any language or any game.

**Never looked at, never counted**: `「」`, `【】`, `（）`, `«»`, `<>` as a label's delimiters. A model that turned `【攻】` into prose would not be told which brackets to keep. If a game shows it, the pair is added HERE, with the game that showed it and a case; until then it is a debt, not a decision.

## Bracketed labels (`Labels`, `LabelsOf`)

`Labels` / `LabelsOf` name « the game's own bracketed labels » — a `[ ]` or `{ }` pair holding a letter, that is not a placeholder — to the model before it answers and after a refusal for brackets.

- **Born from one measured case** (`d19a657`, 2026-09-26; `analyse/balises-ia.md`): a Chinese game's `[攻]`, which a model turned into prose, being told only « '[' appears 3 times instead of 4 » and failing the same way three times. The bench carries it (`a bracketed label in a colour`).
- **`{Hard}` is an example written in the comment, not a label measured on a game.** Braces are in the pattern because `Accepts` counts them, not because a game was seen with a brace label.
- The pattern: `\[[^\[\]{}]*\]|\{[^\[\]{}]*\}`, then not a placeholder of the grammar, then a letter (`HasLetter`, the tables). It does **not** exclude a line break or `<` `>` inside, nor every `[!`: only the tokens of the grammar. A caller with a narrower idea filters on `Inside`; it does not keep a second pattern.

`LabelsOf(text)` gives every occurrence, in order: `{start, length, text, inside}` (UTF-16 units). `Labels(text)` is the same list, each text once.

## What is wrong with an edit, as facts: `Placeholders.EditFaults`

`Accepts`, `AcceptsEdit` and the editors all ask the same question — does this text keep the source's placeholders — and used to answer it in
SENTENCES (English, written for a model). A person is not a model, and two products said the same fact in two ways. The question now has one
answer made of facts, and the sentences are derived from it:

| kind | fields | meaning | the sentence a MODEL gets (`Placeholders.ModelSentence`) | what a PERSON reads (`spec/wording.json`) |
|---|---|---|---|---|
| `sequence` | `sequence` | a placeholder with the delimiters the game wrapped round it (`({[!v*0]})`) comes back fewer times | the exact sequence "…" is missing or altered | `placeholder.sequence_missing` (site: `merge.placeholder_sequence_missing`) |
| `count` | `token`, `found`, `expected` | a placeholder appears a number of times other than the source's | token … appears N time(s) instead of M | `placeholder.count` (`merge.placeholder_count`) |
| `invented` | `token` | a placeholder the source never had | token … does not exist in the source | `placeholder.invented` (`merge.placeholder_invented`) |

In this order: the sequences (in the order of the source), then the counts (in the order of first appearance in the source), then the invented
(in the order of the edit). An EMPTY edit has none, whatever the source holds: it is a capture, not a translation. `EditFaults(source, edited)` is the
list a person's edit gets; the site's `editProblems` (`resources/js/rules/placeholders.js`) is its port and answers the same shape — held by the same
cases in `corpus/rules/placeholders.json` (`edit_faults`, which the C# and the editors both replay; every case of `accepts_edit` is there, derived from
its English lines). The sentence for the model is unchanged, byte for byte (`accepts` and `accepts_edit` cases).
