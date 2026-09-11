# The corpus — what the shared rules must answer, in a language no program owns

`src/` holds the rules in C#. This folder holds **what they must answer**, as JSON cases, so that
a second implementation — in another language, for another engine — can be checked against the
same questions without reading a line of C#. `tests/UnityGameTranslator.Common.Checks` is the C#
executor; a port writes its own, one dispatch line per operation.

The corpus is what the **specification** demands, never what the C# happens to answer. A case is
written from the rule as stated; its `why` is the rule in one sentence. Hash vectors are frozen
from the website's reference implementation, which was itself checked against a real published
file — see the `note` at the top of `rules/sync.json`.

## Layout

```
corpus/
  manifest.json      schema_version, the rules the executor must load
  rules/<rule>.json  one file per rule, named like the C# class it holds
```

A rule file:

```json
{
  "rule": "versions",
  "classes": ["Versions"],
  "operations": {
    "compare":  { "in": { "a": "string?", "b": "string?" }, "out": "sign" }
  },
  "cases": [
    { "id": "versions/compare/ten-after-nine", "op": "compare",
      "in": { "a": "0.9.9", "b": "0.9.10" }, "out": -1, "symmetric": true,
      "why": "ten comes after nine" }
  ]
}
```

- `classes` — the C# classes this file covers. The executor's coverage check demands a case for
  every public method of each, so a rule added to `src/` without a case fails the run.
- `operations` — documentation for a port: what each op takes and gives. The executor dispatches
  on `rule/op`, not on this block.
- `id` — `rule/op/slug`, unique, stable: what a failure prints.
- `op` — the operation, in `snake_case`, never the C# name.
- `in` — an object, keys named after the C# parameters in `snake_case`. Never positional. An
  absent key is the parameter's default; JSON `null` is C# `null` ("not asked", "no such line").
- `out` — the exact answer, or a relation (below).
- `symmetric` — for a signed comparison: the first two inputs swapped must give `-out`.
- `why` — the rule in one sentence.

## Answers

| C# | JSON |
|---|---|
| a sign (`Compare`) | `-1`, `0`, `1` |
| an enum | its C# name as a string: `"InSync"`, `"TakeRemote"` |
| a double | `{"approx": 0.8, "tol": 1e-9}` (`tol` optional) |
| `MergeDecision` | `{"verdict", "conflict", "kind", "reason"}` — `kind` is `null` unless `conflict` |
| `Badge` | `{"kind", "text", "tone", "tip"}`, in a list, in order |
| a gate's verdict (`Placeholders.Accepts`, `AcceptsEdit`) | `{"accepted": bool, "errors": [the lines, in order]}` — the lines are frozen: a model receives them verbatim |
| a `Dictionary<string, int>` (`Tally`) | an object of numbers |
| a message with line breaks | a string with `\n` — never the platform's line ending |

Objects are compared **partially**: the keys a case names must match, keys it does not name are
not judged. Lists are exact, in length and in order.

Relations, when a value would only restate the implementation:

- `{"same_as": "<case id>"}` — the same answer as that case (order-independence, metadata
  exclusion).
- `{"above": in}`, `{"below": in}`, `{"level": in}` — this input ranks above / below / level with
  another input of the same operation (the priority ladder of a merge).

## Inputs

| C# | JSON |
|---|---|
| `TranslationLine` | `{"v": "Bonjour", "t": "H"}` — no `t` = no tag; `"v": null` = a null value |
| pre-tag line | a bare string: `"Bonjour"` |
| no line on this side | `null`, or the key absent |
| the lines of a file (`content_hash`) | `{"lines": {"Hello": {"v": "Bonjour", "t": "H"}}, "uuid": "…"}` — exactly a `translations.json` excerpt; metadata keys may appear, the rule excludes them |

## Sides — which operations a product other than the C# consumers must hold

The C# executor covers every rule in `rules`. A product that cannot consume C# — the website —
re-implements some of them in its own language, and the manifest says which:

```json
"sides": { "site": { "sync": ["content_hash"], "settings": ["all", "json_key", "section_of"] } }
```

🔴 **This is the coverage rule for that side, and it exists because a rule ABSENT from one side
is the defect no case can see.** The site's executor (`website/tests/Unit/CorpusTest.php`) fails
when an operation listed here has no dispatch, and when a dispatch exists that is not listed here
— the manifest is the one place that says what the site holds. Adding a rule the site must hold
means adding it here first, then writing the site's thirty lines, then watching it go green.

Two sides today, because the site is two languages: `site` is the PHP (the door, the merges),
`editor` the JavaScript that runs in the browser while somebody types
(`website/resources/js/rules/`, executed by `node --test`, no framework). A rule the editor
enforces while typing must be listed for BOTH, or the door and the screen disagree — which is how
the placeholder rule came to warn on one and block on the other.

The site reads a **copy**, `website/resources/corpus/`, put there by `sync-common.ps1` and refused
by `check-spec.py` when it diverges — the same road as the catalogue and the spec.

## Running

```
cd tests/UnityGameTranslator.Common.Checks && dotnet run
```

Each case prints `ok` or `FAIL` with its id and its `why`; the exit code is non-zero on any
failure. Line endings are LF, as everywhere in this repository: the corpus is compared in bytes.
