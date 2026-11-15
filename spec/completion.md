# Completing a translation somebody made: `Completion` and `TermEvidence`

Decided by the user on 2026-11-15 (« ce qui est déjà validé reste, ce qui n'a jamais été validé peut sauter »). Pure rules, no file, no JSON, no model;
the cases are `corpus/rules/completion.json` and `term_evidence.json`, every line invented.

## `Completion.Complete(mine, pass, termKeys, holdsGameLists)`

Identity is the KEY, byte for byte. « Left Click » and « Left click » are two lines; nothing is close, merged or de-duplicated.

| his line | the pass | verdict | result |
|---|---|---|---|
| validated: H with words, V, S (`Merge.IsByHand`) | any | `Kept` | untouched; only the mark of a term may be added (text and tag stay) |
| M (the mod's interface, not a line of the game) | any | `Kept` | untouched, never marked, not counted among the validated |
| A, no tag, or an H with nothing in it (a capture) | the same words | `Same` | untouched; the mark of a term may be added |
| A, no tag, or an empty H | other words | `Replaced` | the pass's line, tag A, his rank kept; his refusals of a link (`unlinked`) kept beside the pass's |
| A, no tag, or an empty H | no such key | `KeptUnvalidated` | stays |
| no such key | a line | `New` | the pass's line, tag A, rank after the largest of his, in the pass's order |

⚠ Replacing an A by an A is **not** `Merge.CanReplace` (at equal priority nobody takes another's place): here A means « never validated, may go ».
The result is every line of his file in its order, then the new ones. `ValidatedKept` counts all his H/V/S lines (the pass has the key or not).
`LocalChanges` (what to add to `_local_changes`) = new + replaced + marked. `Format` = `TranslationFormat.OfContent` of what the result contains.
`Announcement` is « N new lines · M validated lines kept · K unvalidated lines replaced ».

## `TermEvidence.Of(term, validatedLines)` and `OfAll`

How a person's own lines (H or V, with words) write a term, with no cutting into words, so the same for any script.

1. Evidence E: the validated lines whose KEY holds the term as a word (`LinkFinding.WrittenIn`: word edges, first letter in either case).
2. A candidate: a stretch of a line of E from a place a word may begin to a place a word may end (`WordGlue.EdgeBefore` / `EdgeAfter`; no white space
   at its ends; **no length in a constant**: in a script with no spaces every place is an edge, and a word of one ideograph is found), held (the same
   way) by at least 2 lines of E, and **confined**: more of the lines of the whole file that hold it are lines of E than are not.
3. The candidates the very same lines hold are one writing: the longest, spelled the way most of its lines spell it.
4. Most lines wins; at the same count, more lines by hand (H); at the same again `Tied` (the pass's word is written and marked to read again, by the caller).
5. The rival: the same search among the lines of E that do not carry the winner. A contradiction is told, never repaired: a person's line is not rewritten.
6. `Thin`: lines name the term and no writing is held by two of them. `None`: no line names it. `OfAll` marks `Ambiguous` two terms that receive the very same writing.

⚠ Limits, said as they are (measured by smart-manager on three real files, targets French and Japanese, source English):
- the **agreement** of a word: « disponible » in one line and « disponibles » in another are two writings at word edges, so no stretch is held by both;
  the answer is the question of today, or the `forms` of the format 2 (to try);
- no source in a non-Latin script with a human translation in number has been measured;
- two terms that receive the very same writing are `Ambiguous` (11 of 18 in one Japanese game, 2 of 2 in another, no false one in French);
- a candidate that contains another term's word is not told apart; the measured curve of the confinement is flat from a third to two thirds of the lines.
Word edges between ideographs and kana: none (`WordGlue` glues letters, digits and combining marks only), so a Japanese writing is found inside a sentence.
