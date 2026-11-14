# What a selection in a key can do: `SelectionAction`

One question, one answer, so that the grid, the edit window and the floating button agree. Held by `corpus/rules/selection_action.json` (`for`) and
by `link_finding.json` (`cited_whole`); the site's editors replay both (`sides.editor`). Pure: the position of the button, the key T and the
selection's rectangle belong to the page.

`for(words, lineOf, fileFormat, terms)` → `{action, key, existing?}` or nothing:

1. a file of the format 1, empty words (trimmed here, nothing else is done to them), a name of the file, a template, or words that hold a marker
   (`[!`): **nothing** — a path known to end in a refusal is not offered;
2. a line of that key is a term (`term: true`): **`open-term`**, that term's window;
3. the words ARE the citation of a term — `LinkFinding.CitedWhole`: its key with the FIRST letter in the other case (the first letter only: « star Coin »
   for « Star Coin »), found once and covering all the words, no term of the line's `unlinked` —: **`open-term`** of that term. A plural, or words
   that hold more than the key, are not (no ending is allowed, `finding.md` §3);
4. a text that cites terms itself (links, or `[!g*`): **nothing** — a term does not cite another term (`LineWrites`);
5. otherwise **`new-term`**, with the line the file holds under that key when it does (it will BECOME a term, `TermEdit.OpenNew`).

`terms` is the index of the file's term keys (`TermIndex.OfKeys`), built once; a port builds its own trie once (`finding.md` §8).
