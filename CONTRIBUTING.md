# Contributing to UnityGameTranslator Common

Thank you for your interest in contributing!

## Contributor License Agreement (CLA)

By submitting a pull request or contribution, you agree that:

1. **You own the rights** to the code you are contributing, or have permission to contribute it.

2. **You grant us a perpetual, worldwide, non-exclusive, royalty-free license** to use, modify, and distribute your contribution under:
   - The AGPL-3.0 license (for the open source version)
   - Any commercial license we may offer

3. **You understand** that your contribution may be used in both the open source and commercial versions of UnityGameTranslator.

This allows us to maintain the dual licensing model while keeping the project sustainable.

## Before anything: what this library is for

It holds the rules the [mod](https://github.com/djethino/unitygametranslator) and the
[installer](https://github.com/djethino/unitygametranslator-installer) must reach the *same answer*
on. A change here changes both programs at once, which is the point — and the reason the bar for
what goes in is higher than "both happen to need it".

## The three rules that decide what belongs here

### 1. It targets netstandard2.0, and that is a hard floor

The mod runs on Unity — Mono and IL2CPP — which is the narrower of the two runtimes. Everything
here is written to that limit so the wider side can consume it:

- no `GetValueOrDefault`, use `TryGetValue`;
- no index-from-end (`^1`) and no ranges (`[0..5]`);
- nothing that needs a runtime newer than what Unity ships.

The other direction does not work: code written for the installer's .NET cannot be dropped into the
mod, which is exactly how a "shared" file would quietly become two files again.

### 2. No dependencies

The mod bundles Newtonsoft.Json; the installer uses `System.Text.Json`. Whichever this library
picked would be imposed on the other. So everything here works on plain types and hands serialising
back to the caller.

If a change seems to need a package, that is the signal it belongs on one side rather than in the
middle.

### 3. Rules, not renderings

The quality bar exists three times over — uGUI in the game, Avalonia in the installer, HTML on the
site — and almost all of it is arithmetic. The arithmetic belongs here; the drawing does not.

⚠ And the site is written in PHP. It will never consume this library, so a rule it also has to obey
cannot have its source of truth here — that needs data all three read. Putting such a rule here and
calling it settled would leave the site drifting while everyone assumes it is covered.

## Changing something here

A change lands in two programs that ship separately, so:

1. say in the commit which of them the change is *for*, and what the other one should now expect;
2. if the behaviour changes rather than the code, both need re-testing — the mod inside a real
   game, the installer against a real library of games;
3. when the change comes from one side's needs, keep the other side's name for things. A value that
   travels between two programs is written in the spelling of whichever one *reads* it, and this
   library is where that agreement is supposed to live.

## Submitting Code

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/my-feature`)
3. Make your changes
4. Build for netstandard2.0 and check both consumers still compile against it
5. Commit with clear messages
6. Push and open a Pull Request

## Code Style

- **C#:** follow existing patterns, use meaningful names
- **No dead code:** remove unused imports and functions
- **Say why, not what:** a comment that repeats the line above it costs a reader time; one that
  explains why the obvious approach was wrong saves them a day
