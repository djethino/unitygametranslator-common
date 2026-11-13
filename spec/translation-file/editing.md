# Editing a sentence that holds links — the written rules

Companion of `schema.json` (the file) and `cases.json`. What the four editors (the mod's panel, the site's three
grids and window) must do the same way. Every sentence below is held by a case of `corpus/rules/text_diff.json`,
`passage_border.json` or `link_editing.json`; a port answers from them. English on purpose: it is what the
products' code and corpus speak.

## 1. What a person sees, what the file holds

She edits the sentence IN CLEAR: each link (`[!g*N]` of `v`) is replaced by the words it reads today
(`LinkReading`), and those words are drawn in colour. She never sees a marker or a number. Saving rebuilds `v`
and `links` from the text she left. `Placeholders.AcceptsEdit` is given the clear text, so it never sees `[!g*N]`.
A format 1 file has no link, no passage, no step: the text goes through as the same string.

## 2. The diff — three lines

Between two successive texts `old` and `new`, in UTF-16 units, never cutting a surrogate pair:

1. the longest common **prefix**;
2. then the longest common **suffix** of what is left of both texts;
3. what lies between is **one hunk**: `old[x, y)` replaced by `new[x, y')`.

Equal texts have no hunk. This is `TextDiff.Hunk`; there is no other diff. It is meant for two **successive**
texts: one keystroke, one paste, one undo is one hunk. (It is NOT applied to « the text at opening » against « the
text at saving » — see §6.)

## 3. The state an editor holds

An editor holds one `EditState` and replaces it with what the rules return; it computes nothing itself.

- `Text` — the clear text; `Format2` — false means nothing below exists.
- one entry per **passage** (in reading order): `Start`, `Length` (UTF-16 of `Text`), its link, `Original` (the
  words it had when its link was last settled — at opening, after an answer, after an act), `OriginalLink`,
  `Pending` (changed words nobody has said what they are), `Open` (§4), `Order` (its place at opening, never reused);
- the **lost** passages: those a step removed, each with its words at that time, its link, its `Original`, its
  `Order` and the reason — `Deleted`, `Straddled`, `EntryGone`. The state REMEMBERS them (§5);
- `Unlinked`, the forms to add (`NewForms`), `Damaged`.

## 4. Step — what one change does (`LinkEditing.Step(state, newText)`)

For the hunk `[x, y)` of the passage `[a, b)`:

| the hunk | the passage |
|---|---|
| after it, or stuck to its end (and it is not open), before it, or stuck to its start | **untouched**: it follows its text (starts move by the hunk's size when the hunk is before it) |
| inside it, or overlapping part of it | **changed**: its extent follows (`b` moves by the hunk's size), the words are compared (below) |
| reaching outside it while overlapping (covers words around it too) | **lost, `Straddled`** — nothing is guessed |
| all of its words erased | **lost, `Deleted`** |

*What a changed passage's words are*, in this order: equal to `Original` → its original link is put back (no question
will ever be asked: typing then undoing leaves it as it was); a writing the term already has (exact, or with the
first letter's case — the callers' own case functions) → the link points at that writing; **absorption**: a
writing of the term, found in the text, that contains the new words (`PassageBorder.AbsorbedAt`) → the passage
widens to it, if it overlaps no other passage; otherwise → `Pending`. The term no longer existing → lost,
`EntryGone`, the words stay as ordinary text.

**Sliding.** A hunk can often be placed more than one way with no change in what the texts say (erasing the last
word of a passage with the space after it, or the space before it). The three lines place it at the right-most; the
hunk slides left or right, while the text it passes over is the text it leaves behind, to the placement that
straddles the fewest passages — the unslid one first, then one step left, one step right, two steps left… A
passage is the only thing that decides.

**Open.** Once a passage was changed and the hunk reached its end (a selection retyped, a last word erased), it
stays **open**: the next insertion exactly at its end is its own, so retyping a passage letter by letter is
retyping the passage. It closes at the first change that is not at its end. An insertion stuck to the end of a
closed passage is outside it (the border rule, below).

## 5. The memory of lost passages

A lost passage is restored — as a passage again, with its link — when a LATER step inserts text in which its exact
words occur **exactly once** and where they overlap no passage: a cut then a paste, an undo, a drag. Of several lost
passages with the same words one paste restores ONE, the one with the lowest `Order`. Never in the step that lost
it (a paste over the whole field that happens to contain a term's words does not link it: a link is known, never
guessed). An `EntryGone` passage is never restored. Typing the words letter by letter never restores anything: each
step inserts one letter.

## 6. What does NOT go through Step

- the **acts** — `Unlink`, `LinkTo`, `MarkAsRead`, `Answer` — each takes the state and returns the next; they
  settle the baseline (`Original` of the passages they touch);
- `Retranslate (AI)`, a merge, a download: they replace the text, they are not typed. The reply of a model is in
  clear; the program finds the links again in it — a link it finds is `by: A`, set only on a writing the term
  already has found ONCE (twice = no link), never on a term the line refuses (`unlinked`). A link a person set
  is kept when its exact words are found once, and named otherwise before she accepts.
- an editor that cannot call `Step` at every change (it polls, or only knows the text at saving) calls it with
  the bigger change it has: the same rules apply, and a hunk that reaches outside a passage drops its link with
  its reason (`Straddled`). That degradation is stated, never another algorithm.

## 7. Saving

`NextQuestion` asks at most ONE question about the final state: changed words that are not a known writing are
« another writing of this term for **All texts** » or « **Only this text** »; passages of the same term with the
same new words share one question. `Resume(original, current)` writes `v` with its markers (numbered in reading
order), the links, the forms to add, the refused entries. Nothing differs from what was shown → nothing is
written. A real change confirms the links (`was` = what they read now, `by: H`); `Mark as read` alone moves `was`.
A link marker typed into the clear text of a format 2 file is refused (`text-has-link-marker`).

## 8. The border rule and its companions live in one place

`PassageBorder` (touches, straddles, stays open, absorbed-at) is the only code that knows where an edge is; the
user's opinion on the default (stuck to the edge = outside) is pending, and changing it is changing those four
methods and their cases in `corpus/rules/passage_border.json`. Sliding and absorption are described here and held
by `link_editing.json` (`k5-last-word-deleted` fails without the first, `c2-a-known-writing` without the second).
