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
  api-v1/openapi.json      OpenAPI 3.1 of the site's API: routes, bodies, headers, closed words
  api-v1/cases.json        contract cases: a setup, a request, the response — replayed and read
  sse-events/sse-events.json  the four streams: wire format, channels, stored replays, events and their data
  sse-events/cases.json    what the site publishes, what a parser yields from the wire, what a reader derives
```

## `sse-events` — the streams

What the relay (`website/sse-server/server.js`) writes on its four streams and how the site feeds
it through Redis: channels, the messages stored for a client that was not listening (delivered
once, or replayed), the wire grammar (`retry: 3000`, `id:` per connection, `: heartbeat` every
15 s), the refusals, and the client's own contract (backoff, heartbeat timeout, what is final,
`code: revoked`). Three kinds of case:

| kind | who runs it | what it proves |
|---|---|---|
| `publish` | `tests/Unit/SsePublisherContractTest.php` | for each `SsePublisher` method, the channel, the message and what is stored, with which TTL |
| `frame` | `SseEventsChecks` over `Engine/SseStream` | bytes in the relay's own format yield these events, this retry, this last id, and stop for this reason |
| `read` | `SseEventsChecks` over `Engine/ApiReaders` | an event's data derives these values — including `state` read OVER a previous state, the sequence rule the stream imposes |

⚠ The relay itself has no executor: it is one file without exports. Its format is described from
a reading of `server.js` and proved from the other end, by the frames the parser is held to.

## `api-v1` — the contract over HTTP

The 29 routes of `website/routes/api.php`, each with its parameters, bodies, headers, refusals
and throttle, and the four rules every client applies (in the document's own description: absent
means unknown, errors carry a sentence, names never codes, declarations never proofs). Its cases
are **contract cases** rather than documents:

```json
{ "id": "api-v1/check-uuid/somebody-elses-main",
  "setup": "lineage",
  "request": { "method": "GET", "path": "/translations/check-uuid", "as": "carol", "query": { "uuid": "…" } },
  "response": { "status": 200, "body": { "exists": true, "role": "none", "main": { "uploader": "alice" } } },
  "read": { "reader": "check_uuid", "expects": { "IsOwner": false, "MainUsername": "alice" } },
  "why": "told BEFORE the click, to the one person it is for" }
```

| side | what it runs | what it proves |
|---|---|---|
| the spec itself | `check-spec.py` | every case's request and response body fits the operation's schema, and every case names a route the document has |
| the site | `tests/Feature/ApiContractTest.php` | built from `setup`, the site answers `request` with `response` — and the whole answer fits the schema (`required`, types, closed words) |
| the mod | `ApiContractChecks` over `Engine/ApiReaders` | the mod's reader derives from `response.body` what `read.expects` says |
| the routes | `ApiContractTest::the_document_names_every_route_and_no_other` | `Route::getRoutes()` under `api/v1` and the document's paths are the same set |

Bodies match **partially** (the keys a case names must match, the rest is not judged) so that an
additive field never breaks a case; `{"$absent": true}` says a key must NOT be there. The markers
(`$is`, `$ref`, `$contains`, `$json`) and the setup shape are described at the top of the file.

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
