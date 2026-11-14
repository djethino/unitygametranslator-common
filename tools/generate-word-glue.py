#!/usr/bin/env python3
"""Writes WordGlue.Tables.g.cs: the characters that are WRITTEN STUCK TOGETHER INTO WORDS, from the Unicode Character Database.

What it is for: where a term is found in a text (LinkFinding), an occurrence counts only if it does not cut a
word — « Coin » is not in « Coins » nor in « Coin2 », but it is in « Coin-op », in « d'Coin », in « *Coin* » and in
« <b>Coin</b> ». Whether two neighbouring characters are one word is not a fact of a language written in this code: it is
Unicode's.

A character is glued when BOTH hold:

  · it is a CONSTITUENT OF A WORD — its Word_Break property (UAX #29, WordBreakProperty.txt) is ALetter, Hebrew_Letter,
    Numeric, Extend, Format, ExtendNumLet or ZWJ: the letters, the digits, the marks that belong to a letter, the low
    line. UAX #29 is the standard's answer to « where does a word end »;
  · its Line_Break class (UAX #14, LineBreak.txt) is one of CLASSES: the letters of a script that spaces its words,
    digits, the marks and viramas that belong to a letter. This keeps the PROJECT's decisions over UAX #29, which glues
    where the project wants an edge: ideographs, kana (Katakana is a word in UAX #29), Thai, Lao, Khmer and Myanmar
    (SA) write no space, so every position between them is an edge; the apostrophe and the full stop between two
    letters (MidNumLet) are edges (« Coin » is in « d'Coin »).

🔴 Line breaking ALONE was the former definition and it was the wrong question: UAX #14 says where a LINE may break, and
files every symbol it has no other use for with the letters (AL, AI), so that a line never breaks between a letter
and `#`, `<`, `*`, `☆`: 6 400 code points that are no part of a word. A name a game draws between `☆…☆`, `#…#`,
`*…*` or `<b>…</b>` was never found. (Measured 2026-11-14 on a real English game: +19 % places found.)

🔴 The Unicode version is the PROJECT's: the one the mod pins in its generators (generate-shaping-tables.py reads
the same LineBreak.txt for its own LineBreakClasses), moved to in all of them at once — never « latest ». The
SHA-256 of BOTH source files is in the header, and `--check` regenerates in memory and says whether the committed
table is the one these sources give.

It also writes corpus/data/word-glue.json — the SAME ranges, as a flat [first, last, first, last, …] array — for a port in
another language (the site's editors): a port that has to find where a word ends reads this table, never its own engine's
idea of a letter (spec/README.md, « One Unicode »). Same version, same SHAs, same `--check`.

Usage:  python tools/generate-word-glue.py [--check] [dir-with-LineBreak.txt-and-WordBreakProperty.txt]
"""
import hashlib
import io
import json
import os
import sys
import urllib.request

# The project's one Unicode version, written ONCE in common/UNICODE_VERSION (read by every generator of common; the
# mod's own generators are to read it too). Never "latest": moved to here, then every table regenerated.
VERSION = io.open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "UNICODE_VERSION"), encoding="utf-8").read().strip()
UCD = "https://www.unicode.org/Public/%s/ucd/" % VERSION
CODE_SPACE = 0x110000

# AI (ambiguous) resolves to AL (UAX #14, LB1). The rest: AK AS AP VF VI (the aksara classes of the Brahmic
# scripts), CM and ZWJ (the marks and the joiner that belong to the letter before), HL (Hebrew), JL JV JT H2 H3
# (Hangul), NU (digits). SA (Thai, Lao, Khmer, Myanmar), ID, CJ, kana, emoji, punctuation, spaces: NOT glued.
CLASSES = ["AI", "AK", "AL", "AP", "AS", "CM", "H2", "H3", "HL", "JL", "JT", "JV", "NU", "VF", "VI", "ZWJ"]

# The Word_Break values (UAX #29) of what a word is made of. Everything else is an edge: spaces, punctuation, symbols, the
# apostrophe and the full stop (Single_Quote, MidNumLet…: the project's edge), Katakana (the project's edge), Newline…
WORD_BREAK = ["ALetter", "Extend", "ExtendNumLet", "Format", "Hebrew_Letter", "Numeric", "ZWJ"]

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "src", "UnityGameTranslator.Common", "WordGlue.Tables.g.cs")
OUT_JSON = os.path.join(HERE, "..", "corpus", "data", "word-glue.json")


def load(name, src_dir):
    if src_dir:
        path = os.path.join(src_dir, name)
        if os.path.exists(path):
            return io.open(path, "rb").read()
    folder = "auxiliary/" if name == "WordBreakProperty.txt" else ""
    with urllib.request.urlopen(UCD + folder + name) as r:
        return r.read()


def line_break_of_every_code_point(text):
    """The same reading as the mod's generator (parse_with_defaults): the '# @missing' defaults first, the
    explicit lines over them."""
    values = [None] * CODE_SPACE

    def fill(rng, value):
        parts = rng.strip().split("..")
        a = int(parts[0], 16)
        b = int(parts[1], 16) if len(parts) > 1 else a
        for cp in range(a, b + 1):
            values[cp] = value

    for line in text.splitlines():
        if line.startswith("# @missing:"):
            fields = [x.strip() for x in line[len("# @missing:"):].split(";")]
            fill(fields[0], fields[1])
    for line in text.splitlines():
        body = line.split("#")[0].strip()
        if body:
            fields = [x.strip() for x in body.split(";")]
            fill(fields[0], fields[1])
    return values


def word_break_of_every_code_point(text):
    """The Word_Break value of each code point (UAX #29, WordBreakProperty.txt); None for « Other » (not listed)."""
    values = [None] * CODE_SPACE
    for line in text.splitlines():
        body = line.split("#")[0].strip()
        if not body:
            continue
        rng, value = [x.strip() for x in body.split(";")]
        parts = rng.split("..")
        first = int(parts[0], 16)
        last = int(parts[1], 16) if len(parts) > 1 else first
        for cp in range(first, last + 1):
            values[cp] = value
    return values


def render(data, word_break_data):
    classes = line_break_of_every_code_point(data.decode("utf-8"))
    words = word_break_of_every_code_point(word_break_data.decode("utf-8"))
    glued = [cp for cp in range(CODE_SPACE)
             if not 0xD800 <= cp <= 0xDFFF and classes[cp] in CLASSES and words[cp] in WORD_BREAK]
    ranges = []
    for cp in glued:
        if ranges and ranges[-1][1] == cp - 1:
            ranges[-1][1] = cp
        else:
            ranges.append([cp, cp])
    out = []
    w = out.append
    w("// <auto-generated>")
    w("// Generated by tools/generate-word-glue.py from the Unicode Character Database %s (LineBreak.txt, WordBreakProperty.txt)" % VERSION)
    w("// Do not edit: change the generator.")
    w("// LineBreak-sha256: %s" % hashlib.sha256(data).hexdigest())
    w("// WordBreakProperty-sha256: %s" % hashlib.sha256(word_break_data).hexdigest())
    w("// </auto-generated>")
    w("namespace UnityGameTranslator.Common")
    w("{")
    w("    public static partial class WordGlue")
    w("    {")
    w('        /// <summary>The Unicode version of the table: the project\'s one, equal to the mod\'s.</summary>')
    w('        public const string UnicodeVersion = "%s";' % VERSION)
    w("")
    w("        /// <summary>The Line_Break classes (UAX #14) a glued character is one of: the project's own decisions (no ideograph, kana or SA).</summary>")
    w("        public static readonly string[] Classes = { %s };" % ", ".join('"%s"' % c for c in CLASSES))
    w("")
    w("        /// <summary>The Word_Break values (UAX #29) a glued character has: what a word is made of.</summary>")
    w("        public static readonly string[] WordBreakValues = { %s };" % ", ".join('"%s"' % c for c in WORD_BREAK))
    w("")
    w("        /// <summary>Inclusive ranges of those characters, sorted, as (first, last) pairs.</summary>")
    w("        private static readonly int[] GluedRanges =")
    w("        {")
    for i in range(0, len(ranges), 4):
        w("            " + ", ".join("0x%X, 0x%X" % (a, b) for a, b in ranges[i:i + 4]) + ",")
    w("        };")
    w("    }")
    w("}")
    flat = [x for r in ranges for x in r]
    doc = {
        "unicode_version": VERSION,
        "note": "The characters written stuck together into words (spec/translation-file/finding.md §5), as inclusive ranges: [first, last, first, last, …], code points, sorted. Generated by tools/generate-word-glue.py: do not edit. A character is glued when its Line_Break class (UAX #14) is one of `classes` AND its Word_Break value (UAX #29) is one of `word_break`: a constituent of a word. Everything else is an edge: spaces, punctuation, the signs and symbols a game draws round a name (`#`, `*`, `<`, `>`, `=`, `☆`).",
        "line_break_sha256": hashlib.sha256(data).hexdigest(),
        "word_break_sha256": hashlib.sha256(word_break_data).hexdigest(),
        "classes": CLASSES,
        "word_break": WORD_BREAK,
        "glued": flat,
    }
    js = json.dumps(doc, ensure_ascii=False, separators=(",", ":")) + "\n"
    return "\n".join(out) + "\n", js, len(ranges), len(glued)


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    data = load("LineBreak.txt", args[0] if args else None)
    word_break_data = load("WordBreakProperty.txt", args[0] if args else None)
    text, js, n_ranges, n_cp = render(data, word_break_data)
    if "--check" in sys.argv:
        stale = []
        for path, want in ((OUT, text), (OUT_JSON, js)):
            have = io.open(path, "rb").read().decode("utf-8") if os.path.exists(path) else None
            if have != want:
                stale.append(os.path.normpath(path))
        if stale:
            print("STALE: %s — run tools/generate-word-glue.py" % ", ".join(stale))
            sys.exit(1)
        print("up to date (Unicode %s, %d ranges, %d characters)" % (VERSION, n_ranges, n_cp))
        return
    with io.open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    os.makedirs(os.path.dirname(OUT_JSON), exist_ok=True)
    with io.open(OUT_JSON, "w", encoding="utf-8", newline="\n") as f:
        f.write(js)
    print("wrote %d ranges, %d characters (Unicode %s)" % (n_ranges, n_cp, VERSION))


if __name__ == "__main__":
    main()
