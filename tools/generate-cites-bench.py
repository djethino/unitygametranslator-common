#!/usr/bin/env python3
"""Writes corpus/data/cites-bench.json: a file big enough to be felt by a port that tests every sentence against every term.

What it is for: LinkFinding.Cites answers « which terms does this source cite, and where » (spec/translation-file/finding.md
§8). The editors of the site must answer it for a file of 100 000 lines and 5 000 terms without testing each sentence against
each term at every rendering — an index of the keys, built once. This file holds 1 500 terms and 6 000 sources (a tenth of
that file, ten times fewer terms than a large game): enough for a port written the naive way — every term against every
source, 9 000 000 searches — to feel it, small enough to ship. The expected answer is made here, by a NAIVE reference written
only from the rules (it reads corpus/data/word-glue.json and corpus/data/first-letter.json, never the C#), and held to the C#'s
answer by the checks (CitesBenchChecks): two implementations that share nothing agree on every place.

The answer is not stored place by place (it would be 7 000 lines): its count and the SHA-256 of its canonical text are. The
canonical text is, for each source in order and each place in reading order, `source-index TAB key TAB start TAB end LF`, the
positions in UTF-16 code units, the keys as written. A port computes the same text and compares.

Usage:  python tools/generate-cites-bench.py [--check]
Deterministic: a fixed seed; the same bytes on every machine.
"""
import hashlib
import io
import json
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "corpus", "data")
OUT = os.path.join(DATA, "cites-bench.json")
SEED = 20261114

glue = json.loads(io.open(os.path.join(DATA, "word-glue.json"), encoding="utf-8").read())
first_letter = json.loads(io.open(os.path.join(DATA, "first-letter.json"), encoding="utf-8").read())
RANGES = glue["glued"]


def is_glued(cp):
    lo, hi = 0, len(RANGES) // 2 - 1
    while lo <= hi:
        mid = (lo + hi) // 2
        if cp < RANGES[mid * 2]:
            hi = mid - 1
        elif cp > RANGES[mid * 2 + 1]:
            lo = mid + 1
        else:
            return True
    return False


def upper_first(text):
    if not text:
        return text
    cap = first_letter["capital"].get("%X" % ord(text[0]))
    return text if cap is None else cap + text[1:]


def lower_first(text):
    if not text:
        return text
    small = first_letter["small"].get("%X" % ord(text[0]))
    return text if small is None else small + text[1:]


def edge(text, i):
    """May a word begin (or end) at i: not between a glued character before it and one at it."""
    if i <= 0 or i >= len(text):
        return True
    return not (is_glued(ord(text[i])) and is_glued(ord(text[i - 1])))


def units(text, i):
    """The offset i (in code points) as UTF-16 code units."""
    return i + sum(1 for c in text[:i] if ord(c) > 0xFFFF)


def prepared(terms):
    """The terms in the ordinal order of their keys, each with the writings of its key (first letter in either case)."""
    ordered = sorted(terms)
    return [(key, sorted({key, upper_first(key), lower_first(key)})) for key in ordered]


def cites_naive(ordered, source):
    """Every term against the source: the rules, with no index. `ordered` is `prepared(terms)`."""
    if not source or "[!L*" in source or any(source == key for key, _ in ordered):
        return []
    found = []
    for number, (key, variants) in enumerate(ordered):
        for variant in variants:
            if variant not in source:
                continue
            at = source.find(variant)
            while at >= 0:
                end = at + len(variant)
                if edge(source, at) and edge(source, end):
                    found.append((at, end, number))
                at = source.find(variant, at + 1)
    found = sorted(set(found), key=lambda p: (-(p[1] - p[0]), p[2], p[0]))
    taken, places = [], []
    for at, end, number in found:
        if any(at < b and end > a for a, b in taken):
            continue
        taken.append((at, end))
        places.append((at, end, ordered[number][0]))
    places.sort()
    return places


def make():
    rnd = random.Random(SEED)
    consonants = "bcdfglmnprstvz"
    vowels = "aeiou"
    accented = "éèüöáíñ"

    def latin():
        word = "".join(rnd.choice(consonants) + rnd.choice(vowels + (accented if rnd.random() < 0.08 else "")) for _ in range(rnd.randint(1, 3)))
        return word.capitalize() if rnd.random() < 0.6 else word

    def cyrillic():
        letters = "абвгдеклмнопрсту"
        word = "".join(rnd.choice(letters) for _ in range(rnd.randint(3, 7)))
        return word.capitalize() if rnd.random() < 0.5 else word

    def han():
        return "".join(chr(rnd.randint(0x4E00, 0x62FF)) for _ in range(rnd.randint(2, 3)))

    pool = set()
    while len(pool) < 1100:
        pool.add(latin())
    while len(pool) < 1300:
        pool.add(cyrillic())
    words = sorted(pool)
    terms = set(words)
    base = sorted(terms)
    while len(terms) < 1500:
        a, b = rnd.choice(base), rnd.choice(base)
        if a != b:
            terms.add(a + " " + b)             # a phrase over two terms: the longest first, the shorter inside it has no place
    hans = {han() for _ in range(120)}
    terms |= hans
    terms = sorted(terms)

    filler = [latin() for _ in range(300)] + [cyrillic() for _ in range(40)]
    sources = []
    for _ in range(5400):
        tokens = []
        for _ in range(rnd.randint(5, 13)):
            roll = rnd.random()
            if roll < 0.35:
                token = rnd.choice(terms)
                if token in hans:
                    token = rnd.choice(filler)
                elif rnd.random() < 0.15:
                    token += rnd.choice(["s", "2", "-op", "é", "'s"])
                elif rnd.random() < 0.2:
                    token = upper_first(token) if rnd.random() < 0.5 else lower_first(token)
            else:
                token = rnd.choice(filler)
            tokens.append(token)
        text = " ".join(tokens)
        sources.append(text + rnd.choice([".", "!", "", ", ok", " (x)"]))
    cjk = sorted(hans)
    for _ in range(600):
        pieces = [rnd.choice(["を", "の", "は", "と", "を拾う", "が"]) for _ in range(rnd.randint(1, 3))]
        text = "".join(rnd.choice(cjk) + p for p in pieces)
        sources.append(text)
    rnd.shuffle(sources)
    return terms, sources


def canonical(terms, sources):
    lines, total = [], 0
    ordered = prepared(terms)
    for index, source in enumerate(sources):
        for at, end, key in cites_naive(ordered, source):
            lines.append("%d\t%s\t%d\t%d\n" % (index, key, units(source, at), units(source, end)))
            total += 1
    return "".join(lines), total


def render():
    terms, sources = make()
    text, total = canonical(terms, sources)
    doc = {
        "note": "A file big enough for a port that tests every sentence against every term to feel it (tools/generate-cites-bench.py). `terms` are the keys of the terms, `sources` the sentences' sources; the expected answer of Cites over an index of the keys, with no refused term, is `places` places whose canonical text — for each source in order, each place in reading order, `source-index TAB key TAB start TAB end LF`, positions in UTF-16 code units — has the SHA-256 `result_sha256`. Made by a naive reference that never read the C#.",
        "unicode_version": glue["unicode_version"],
        "terms": terms,
        "sources": sources,
        "places": total,
        "result_sha256": hashlib.sha256(text.encode("utf-8")).hexdigest(),
    }
    return json.dumps(doc, ensure_ascii=False, separators=(",", ":")) + "\n", total, len(terms), len(sources)


def main():
    text, total, n_terms, n_sources = render()
    if "--check" in sys.argv:
        have = io.open(OUT, "rb").read().decode("utf-8") if os.path.exists(OUT) else None
        if have != text:
            print("STALE: %s — run tools/generate-cites-bench.py" % os.path.normpath(OUT))
            sys.exit(1)
        print("up to date (%d terms, %d sources, %d places)" % (n_terms, n_sources, total))
        return
    with io.open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print("wrote %d terms, %d sources, %d places, %d bytes" % (n_terms, n_sources, total, len(text.encode("utf-8"))))


if __name__ == "__main__":
    main()
