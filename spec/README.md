# The spec — the contract between a Core and the site, written where every side can check it

`src/` holds the rules, `corpus/` what they must answer. This folder holds **the artefacts the
products exchange** — the files, the routes, the events — described so that a second Core, in
another language, can be written without reading a line of C#, and so that no side can drift from
the description without a check turning red.

🔴 **Every artefact here has a consumer on each side that fails if the description lies.** A schema
nobody validates is a document that rots; this project measured what a summary is worth against
the code. So each artefact ships with its **cases** — documents that are well formed and documents
that are not, with what a reader must derive from them — and:

| side | what it runs | what it proves |
|---|---|---|
| the spec itself | `check-spec.py` (root, Python `jsonschema`) | the schema agrees with its own cases, and the site's copy is the source |
| the mod | `tests/UnityGameTranslator.Core.Checks` | the mod's reader derives from each case what the case says |
| the site | `tests/Unit/*SpecTest.php` (PHPUnit) | the site accepts and refuses at upload what the case says |

The site cannot consume C#, so it carries a copy: `website/resources/spec/`, put there by
`sync-common.ps1` and refused by `check-spec.py` when it diverges — the same road as the catalogue.

## Layout

```
spec/
  manifest.json            schema_version, the artefacts every checker must load
  <artefact>/schema.json   JSON Schema 2020-12 of the WRITTEN form — what a Core must produce
  <artefact>/cases.json    the documents, and the verdicts
```

## Two verdicts per case, and they can differ

```json
{ "id": "translation-file/line/tag-m-is-legacy",
  "document": { "_uuid": "…", "Play": { "v": "Jouer", "t": "M" } },
  "written": false,
  "upload": "accepted",
  "read": { "lines": {}, "stranded_interface": 1 },
  "why": "M is the interface's tag; a current Core never writes it into this file, the site still accepts one written before the split" }
```

- `written` — does the document conform to the schema, i.e. is it what a **current Core must
  write**. Checked by `check-spec.py`.
- `upload` — `accepted` or `refused`: what the site does with it at `POST /api/v1/translations`.
  Checked by PHPUnit.
- `read` — what a **reader** derives, when a reader can read it at all: the mod's, the Manager's,
  a Core's. `"read": "throws"` when the document must be refused whole. Checked by the mod.
- `site` — what the **door** derives at upload (`line_count`, `tag_counts` by band), on the file
  as sent: a reader normalises and merges, the door counts what it was given. Checked by PHPUnit.

⚠ **A case with `written: false` and `upload: accepted` is a tolerance of the site**, kept for
files written by older mods. They are listed on purpose: they are the difference between the
strict form and the door, and closing one is a decision about the mods still in the field, not a
tidy-up. `grep -n '"written": false' -A1` finds them.

## Answers

Objects in `read` are compared **partially**: the keys a case names must match, keys it does not
name are not looked at. `lines` is exact: the keys named are all the lines, each `{v, t}` and `i`
when named. Absent metadata reads as the reader's default (`null`, `0`, `false`), never as the
previous file's value — that is the whole point of `LoadedFile`.
