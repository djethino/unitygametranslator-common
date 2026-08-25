# UnityGameTranslator Common

The rules two programs have to agree on, written once.

The [mod](https://github.com/djethino/UnityGameTranslator) runs inside a game and captures its text;
the [Manager](https://github.com/djethino/unitygametranslator-manager) sets that mod up and
manages what it produces. They meet through files on disk and through the community site's API — so
a rule one of them gets slightly wrong is a rule they disagree about, silently.

That has happened. The Manager once wrote `"ai"` where the mod reads `"llm"`, and every AI setup
it configured did nothing at all, without a word on screen. It searched the community site by folder
name where the mod publishes under the game's own product name, so games with a translation were
reported as having none. Its model tester built a prompt the mod would never send, and the
difference was only found by reading the mod line by line.

None of those were hard bugs. They were two copies of one idea drifting apart.

## What belongs here

Logic both programs need to reach **the same answer** on:

- three-way merging of translations;
- how a prompt is built for a translation model, and what happens when the answer comes back wrong;
- how a translation's quality is apportioned for display;
- how secrets are protected at rest, how versions compare, which language codes exist, how a hotkey
  is spelled.

## What does not

**Anything that draws.** The quality bar exists three times over — uGUI in the game, Avalonia in the
Manager, HTML on the site — but almost all of it is arithmetic and only a handful of lines put
pixels anywhere. The arithmetic lives here; the three drawings stay where they are.

**Anything that needs a JSON library.** The mod uses Newtonsoft, the Manager uses
`System.Text.Json`, and whichever this library chose would be imposed on the other. Everything here
works on plain types and hands the serialising back to the caller.

**Anything the website also needs to agree with.** It is written in PHP and will never consume a C#
library, so a rule shared with the site cannot have its source of truth here — that has to be data
all three read, or it will drift while everybody assumes it is covered.

## Building

Requires the .NET SDK.

```bash
dotnet build -c Release
```

⚠ It targets **netstandard2.0**, and not by accident: the mod runs on Unity's Mono and on IL2CPP,
which is the narrower of the two runtimes. Everything here is written to that limit — no
`GetValueOrDefault`, no index-from-end, no ranges — so that the wider side can consume it. The other
direction does not work.

## Related

Five repositories, one product — [see it live][live].

- [UnityGameTranslator][mod] — the mod that translates a game while you play
- [unitygametranslator-manager][manager] — the desktop tool that finds your games and sets the mod up
- [UnityGameTranslator-website][website] — where translations are shared, reviewed and merged
- [unitygametranslator-catalogs][catalogs] — reference data: languages, AI models, mod loaders

[mod]: https://github.com/djethino/UnityGameTranslator
[manager]: https://github.com/djethino/unitygametranslator-manager
[website]: https://github.com/djethino/UnityGameTranslator-website
[catalogs]: https://github.com/djethino/unitygametranslator-catalogs
[live]: https://unitygametranslator.asymptomatikgames.com

## Licence

AGPL-3.0, as the mod and the Manager are. See [LICENSING.md](LICENSING.md).
