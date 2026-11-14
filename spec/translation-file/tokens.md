# The game's tokens: `_lists.tokens`

A game fills some texts itself before it shows them: `#PlayerName#你是第一次来吧！` is shown `张三你是第一次来吧！`. The
piece the code replaces (`#PlayerName#`) is a **token**. A text that holds one is only found again, and only translated whole,
if the token is known — without the list no line that shows a name is ever found by its source. Held by
`corpus/rules/game_tokens.json` (`GameTokenList`) and by the cases `translation-file/format2/tokens-*`.

## What it is

- A **literal text**, never a pattern or an expression: `#PlayerName#`, `{p3}`. A numbered family (`{p1}`, `{p2}`, `{p3}`…) is
  written as the tokens it holds, one each.
- **It has no translation**: it is not a line of the file and no sentence links to it. The game puts a text there; whatever the
  translation says around it, the token comes back as it was (the mod and the cold translation protect it as a slot `[!STR*N]`).
- **It is not `_variables`.** `_variables` (a settings section) declares the values the MOD reads in a running game, one by one, by
  a path it can follow (`GameManager.PlayerName`): they are the translator's to edit. A token is what the game's CODE replaces; it
  is found by reading the code, imposed by the game, edited by nobody. The two meet on a text (a token whose replacement is a
  declared variable is shown by the variable), never in the file.

## Where it lives, who writes it, who reads it

```json
"_lists": { "lists": { }, "tokens": ["#PlayerName#", "{p3}"] }
```

- **A sibling of `lists` in `_lists`**, with the same nature: imposed by the game, read by the extraction, edited by nobody; the most
  recent extraction replaces it, it is never merged line by line. `lists` stays required (it may be empty): a file that knows tokens
  and no list writes `"lists": {}`.
- **Written by the extraction** (the Manager's C5, `tokens.json` of the prototype), once per extraction. **Read by the mod** (the
  display finds a text whose tokens the game has filled) **and by the Manager** (a cold translation protects them as slots).
- **Not in the content hash**, like the rest of `_lists`: `_lists` is a metadata name (`TranslationFileKeys`), and
  `Sync.ContentHash.Of` is given the lines and the uuid, nothing else. Nothing changes for a file that has none.
- **A reader that does not know the key ignores it** (an unknown key of an object that does not forbid it). A file that holds
  `_lists` — tokens alone included — is a format 2 file (`TranslationFormat.OfContent`, `holdsGameLists`).
- **The site and the editors** do not read it: no port.

## What a writer owes (`GameTokenList.Refusal`)

Each token is a text that is not empty, holds no carriage return (the texts of the format 2 are LF alone) and no marker `[!` (a
token is the game's text, never a slot or a link: `token-invalid`); the list is in **strictly ascending ordinal order** of the
UTF-16 code units, which also says each once (`tokens-not-in-order`) — so that two extractions of one game write the same bytes
and `_lists` can enter the hash as one block. `GameTokenList.Canonical` makes such a list. A **reader** is tolerant
(`GameTokenList.Read`): it keeps what may be a token, each once, in the order given, and says nothing.

## Shape in the prototype, and what was not kept

`tokens.json` is a JSON array of strings (`["#PlayerName#", …]`), ordinal-sorted by the extraction (`Tokens.cs`, the Manager's
`GameTokens.Find`); the mod's prototype reads it as a list of strings (`DisplayMatch.GameTokens`), and the cold translation orders
them by length itself. The file holds exactly that array, nothing more: no pattern, no expression, no origin (the code's
`tokens-sources.txt`, where each replacement comes from, stays with the extraction's notes).
