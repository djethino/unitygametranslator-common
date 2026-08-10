# Third-Party Licenses

This document lists the third-party components used by UnityGameTranslator Common.

## None

This library has no dependencies, and that is a rule rather than a coincidence.

It is consumed by two programs that do not agree on their own dependencies — the mod bundles
Newtonsoft.Json, the installer uses `System.Text.Json` — so anything depended on here would be
imposed on both. Everything in this library therefore works on the types the framework itself
provides, and hands serialising, drawing and talking to the network back to whoever called it.

If a change here ever seems to need a package, that is the signal it belongs on one side or the
other rather than in the middle.
