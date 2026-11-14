# The shape of a text's KEY — the written rule

The key of a translation line is the game's text, put in the shape the mod looks it up in. The same in the mod, the Manager
and every file written from a game's texts: otherwise a file extracted cold never meets the text the game shows. Held by
`corpus/rules/key_shape.json` and `key_chars.json` (hand cases) and `corpus/data/key-vectors.json` (3 000 synthetic texts and
what the MOD's own function made of them). Only the mod and the Manager use it (`manifest.json`, `held_by`): the site and its
editors make no key — they receive keys already made (`rules/placeholders.js` only validates tokens) — so no port.

## The steps

1. **Line ends** made LF (`LineEndings`).
2. *(the mod only)* the game's live variables lifted out as `[!STR*N]` (`IVariableSubstitution`): state of a running game,
   not a thing of a file. The Manager's chain has none.
3. **Numbers** lifted out as `[!v*N]`, when `normalize_numbers` is on.

`KeyShape.Of(text, normalizeNumbers)` is 1 then 3 (and returns the numbers seen); `KeyShape.Stored(text)` is 1 and 3, then trimmed
(`KeyChars.Trim`) — what a cold translation writes. The mod's lookup is a SEARCH, not a key: the text as shown, then its key,
then its trimmed key, then the display's forms; storing the trimmed key is what that third rung finds. `KeyShape.Restore` puts
the numbers seen back in the slots. A text with nothing to change comes back as the very same string, allocating nothing.

## What a number is — exactly, as the key of every file already written holds it

`-?` digits, then optionally `.` or `,` and digits, then optionally `%`; scanned left to right, greedy, never overlapping the
one before. A number is **not** lifted when it starts right after `[!v*`, when it is the tail of a hexadecimal colour (up to
eight hexadecimal characters then `#` just before it), or when it is the digits of a `[!v*N]` / `[!STR*N]` slot already there.
The slot of a lifted number is its RANK among the lifted ones, whatever slots the text holds.

These are FACTS of the key, reproduced and not to be corrected without a migration of files:

- « 1,000,000 » is **two** slots, `1,000` then `000` (the comma between them stays); « 1.2.3 » is `1.2` and `3`; « 2-5 » is `2` and `-5`
  (a sign is taken before ANY digit);
- the decimal and thousands marks are the ASCII « . » and « , » — no culture, no other mark;
- **a digit outside the Basic Multilingual Plane is no number**: the key is read by UTF-16 unit, and a surrogate is no digit.

## Digits and white space — Unicode's, at the project's version

A digit is a character of general category Nd of the Basic Multilingual Plane (370 at Unicode 18.0.0); white space is
White_Space of the same plane (25). Generated into `KeyChars.Tables.g.cs` by `tools/generate-key-chars.py` from
`UnicodeData.txt` and `PropList.txt` of the pinned version (`KeyChars.UnicodeVersion`, equal to the mod's, to `Casing`'s and
`WordGlue`'s; the sources' SHA-256 are in the header; `--check` says whether the committed table is the one they give). Equal
to the mod's own `UnicodeInfo` tables (checked at the time of writing).

**What this changes for a mod that runs on an old Mono or on IL2CPP.** Until now the digits of the number pass were the
regular expression's `\d`, i.e. the Unicode of the runtime the game ships; and `Trim()` the runtime's white space. Measured on
.NET 8.0.34 both are, over the whole plane, exactly the tables above — so nothing changes there. A runtime older than the
pinned Unicode may lack a decimal digit added since (they are almost all outside the plane today, hence none for the key); a
file written by such a game could then hold a key in which that digit stayed a digit, where the table lifts it. The mod's raw
search and its trimmed search would find the line or not depending on the character; the key stops depending on the game's
runtime, which is the goal, and the rare character concerned is the only price.

## The braces

`KeyShape.Unbrace(key)` turns a key's braced number slots into plain ones (`{[!v*0]}` → `[!v*0]`, a sign before and a `%` after
going with it): the shape of the same text once the game has formatted it. `KeyShape.Rebrace(translation, bracedKey)` is its inverse
(null when a slot is not once in the translation). Both are pure: they read nothing of the game.

## Candidates, not in this rule

`TextNormalization.IsNumericOrSymbol`, `IsPrivateUseOnly`, `IsNaturalIdentity`, `IsCertainlyNotALetter` — « nothing to
translate here »: the mod's four doors call them; the Manager's extraction will want `IsNumericOrSymbol` to decide what to
extract. A task of its own.
