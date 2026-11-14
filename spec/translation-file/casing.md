# The case of the first letter — the written rule

What a link's `case` (`upper-first` / `lower-first`) means, the same in the mod, the Manager and the site's editor.
Held by `corpus/rules/casing.json` (36 cases) and by the table file `corpus/data/first-letter.json`.

## Definition

- **The unit is the first code point of the text** (a surrogate pair is one). Nothing else is looked at: the
  function never skips ahead to « the first letter ». A text that opens with a digit, a quotation mark, a space, a
  combining mark, an ideograph — a first character that has no case — comes back **as it is**. Empty or null: as it is.
- **The mapping is the language-independent one**, and only that: Unicode's simple mapping (`UnicodeData.txt`:
  uppercase, lowercase, titlecase) with the **unconditional** multi-letter mappings of `SpecialCasing.txt` laid over it
  (`ß` → `Ss`, `ﬁ` → `Fi`, `İ` → `i` + a combining dot above). Not the conditional ones: no Turkish or Lithuanian
  rule, no final sigma. No locale, no culture, no `toUpperCase()` of a runtime — each of them answers with the
  Unicode of whatever runs it.
- **A capital is the titlecase**, else the uppercase: `ǆ` → `ǅ` (not `Ǆ`), `ǉ` → `ǈ`. A small letter is the
  lowercase.
- A mapping that changes nothing is not in the tables: the very same string is returned.

## The version

The Unicode version is **the project's**, written once in `tools/generate-casing.py` and exposed as
`Casing.UnicodeVersion` and as `unicode_version` of the JSON. It MUST equal the version the mod pins in its own
generators (`generate-shaping-tables.py`, `generate-indic-tables.py`, `generate-bidi-trie`, `generate-case-tables.py`):
one Unicode for everything, moved to in all of them at once, never « latest ». The mod checks the equality against its
own tables.

## Where a port reads it

`corpus/data/first-letter.json` — `capital` and `small`, code point (upper-case hexadecimal) → text; `sources_sha256`
names the two UCD files it was made from. A port loads it and does the lookup on the first code point; it never calls
the platform's casing. `CasingChecks` holds the JSON and the C# tables equal entry by entry. The tables are generated
(never written by hand): `python tools/generate-casing.py [dir]`, and `--check` says whether the committed tables are the
ones these sources give. Nothing is downloaded at build time.

## The whole text: `Casing.Lower`, and the capital is `Names.Fold`

`UpperFirst` / `LowerFirst` write ONE letter. A whole text in small letters is `Casing.Lower(text)`; in capitals it is `Names.Fold(text)` — the table the
names are compared and sorted by, so there is no `Casing.Upper` (a second door to the same rule). Both read UnicodeData's SIMPLE mapping, one code point to
one, the project's Unicode version, never the runtime's, no rule of any language; the very same string when nothing changes; the length in UTF-16 units kept
(a position in the text is a position in its other form).

What « simple » decides where the full mapping differs, written out (`Casing.Lower`; corpus `casing/lower/*`):

| text | `Lower` | why |
|---|---|---|
| `ΟΔΟΣ` | `οδοσ` | no final sigma: it depends on the neighbours, a language's rule; `Σ` is always `σ` (a JavaScript `toLowerCase` writes `οδος`) |
| `İ` (U+0130) | `İ` | its small letter is an ASCII `i`: nothing crosses ASCII, in either direction — as for `Names` (`ı`, `ſ`) |
| `K` (Kelvin, U+212A) | itself | same rule (the runtime writes `k`); `Å` (U+212B) gives `å`, both are beyond ASCII |
| `ẞ` (U+1E9E) | `ß` | its small letter; `ß` stays (already small), its capital being two letters keeps it out of `Names.Fold` |
| `ǅ` | `ǆ` | a digraph in title case |
| a letter Unicode added after the runtime's tables (Garay, Unicode 16) | lowered | the table is the project's version; 76 code points differ from .NET 8.0.34 today (`Common.Checks` prints the number) |

`Casing.Lower` is for the Manager's cold chain (term ids, grouping by case, the « in small letters » question, the lexicon's words); no port in another language
reads it, so there is no data file for it (`corpus/data/simple-capital.json` is the capital table the site reads; the small one stays in C#). Held by
`CasingChecks.Whole` over every code point (length kept, nothing crosses ASCII, idempotent).
