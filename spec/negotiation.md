# Negotiating a request with a provider: `Negotiation`

What the mod and the Manager do when an OpenAI-compatible server refuses the request they sent — decided once, held by `corpus/rules/negotiation.json`
(`run`: a script of refusals on one negotiation, the state after each step). No JSON, no HTTP: this decides the SHAPE of the next request; each program
writes it in its own JSON and sends it with its own client.

## What it knows, and where each word comes from

The decision reads the refusal's text for a word. **Those words are observed, not designed**:

| word in the body | what is given up | source |
|---|---|---|
| `max_completion_tokens` | the cap's field becomes `max_completion_tokens` (the other is taken off) | the wording OpenAI's reasoning models refuse `max_tokens` with, quoted by the first checks of this rule; not re-measured since |
| `seed` | no seed (before the temperature: dropping the seed costs only reproducibility) | « Unrecognized request argument supplied: seed », same origin |
| `temperature` | no temperature, the provider's own applies | « temperature does not support 0.0 with this model », same origin |
| (nothing known named) | the reasoning ladder: `none`, `low`, no `reasoning_effort` field | the default when a 400/422 says nothing we know |
| `reasoning` | the same ladder, named | the field's own name |
| `response_format`, `json_schema` | **nothing** — see below | **only the Manager's stand-in server** (« Unsupported parameter: response_format »). No real server's refusal of a response format has been captured yet: a new spelling is added to `Negotiation.ResponseFormatWords` when one is, with the server that said it |

Only 400 and 422 are about our request (`IsAboutOurRequest`): a wrong key, a wrong address, a rate limit and an outage give up nothing.

## A refusal of the response format is said, never conceded

A structured question carries a response format (`response_format: json_schema`). `Concede(body, structured, out reason)` answers `Made`, `ResponseFormat` or `NothingLeft`:

1. A field the server NAMED and we may still give up (the cap's name, the seed, the temperature) goes first, even when the format is named too — one change per attempt.
2. Then, **when the request was structured** and the body names the response format and does not name the reasoning (or names it with no reasoning rung left): `ResponseFormat`. Nothing is given up and the state is not touched — walking the reasoning ladder over a question about the shape of the answer
   costs requests and quality for nothing. The caller SAYS it (« the server refuses the request: … »); a reply that only looks like the shape asked for would be read as a model that holds the action.
3. Then the ladder, as before; then `NothingLeft`.

A request with no response format never gets `ResponseFormat`: it sent none, so a body naming one is read as it always was. `Concede(body, out reason)` stays, and is `structured: false`.

## What the class keeps, and what resets it

It keeps four things: the cap's field, a dropped temperature, a dropped seed, the reasoning rung. **`ForgetIfChanged(provider|model)` forgets all four** when the provider or the model changes — what one server accepts
says nothing about the next. **It keeps nothing about a refusal of the response format**, on purpose: no later question is spared the request by it, so no event has to reopen it (another model, another server, an
update of the server: the next structured question simply asks, and is refused again, in one request).
