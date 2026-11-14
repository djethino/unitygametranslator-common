#!/usr/bin/env python3
"""Writes corpus/data/decorations-bench.json: sources of the FORM of a real game's texts — emphasis stars, hash signs, rich-text tags with
attributes, placeholders, black stars round a name — and the places the socle must find in them.

What it is for. Where a word ends (WordGlue) was UAX #14's, which glues every symbol to the letters: a name a game draws between `☆…☆`, `*…*` or
`<b>…</b>` was never found (measured on a real English game, 2026-11-14: 20 953 places found, 24 962 once fixed). The file is INVENTED —
nothing of a game's text belongs to a public repository — but it has the shape of what was measured: the same signs, the same tags (`<color=#RRGGBB>`,
`<sprite name=word>`), the same terms that are the end of a longer one (`Unit`, `Attack Unit`).

What it holds, and who holds it to it (CitesBenchChecks):
  · the places a NAIVE reference finds (every term against every source, written here only from the rules: word-glue.json, first-letter.json, and
    « a tag is not text », `<[^<>\\r\\n]+>`), as a count and a SHA-256 — the C# index must find the same places, to the position;
  · « no place is found INSIDE a tag » — a property the check recomputes from the sources;
  · the comparison with the FORMER table (UAX #14 alone, no tag rule), made HERE because only here is the former table at hand (it is rebuilt from
    LineBreak.txt of the project's Unicode): `places_lost` counts the places the former rule found that the new one does not, and every one of them
    is covered by a LONGER place of the new answer (the longest term wins: `Unit` gives way to `Attack Unit`). The generator refuses to write the file
    when one is not; the file says `lost_without_a_longer_term: 0` and the check reads it.

Usage:  python tools/generate-decorations-bench.py [--check] [dir-with-LineBreak.txt]
Deterministic: a fixed seed; the same bytes on every machine.
"""
import hashlib
import importlib.util
import io
import json
import os
import random
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "..", "corpus", "data")
OUT = os.path.join(DATA, "decorations-bench.json")
SEED = 20261115

glue = json.loads(io.open(os.path.join(DATA, "word-glue.json"), encoding="utf-8").read())
first_letter = json.loads(io.open(os.path.join(DATA, "first-letter.json"), encoding="utf-8").read())
RANGES = glue["glued"]


def glued_by(ranges):
    def is_glued(cp):
        lo, hi = 0, len(ranges) // 2 - 1
        while lo <= hi:
            mid = (lo + hi) // 2
            if cp < ranges[mid * 2]:
                hi = mid - 1
            elif cp > ranges[mid * 2 + 1]:
                lo = mid + 1
            else:
                return True
        return False
    return is_glued


def former_table():
    """The table before 2026-11-14: Line_Break classes alone."""
    spec = importlib.util.spec_from_file_location("g", os.path.join(HERE, "generate-word-glue.py"))
    g = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(g)
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    classes = g.line_break_of_every_code_point(g.load("LineBreak.txt", args[0] if args else None).decode("utf-8"))
    glued = [cp for cp in range(g.CODE_SPACE) if not 0xD800 <= cp <= 0xDFFF and classes[cp] in g.CLASSES]
    ranges = []
    for cp in glued:
        if ranges and ranges[-1][1] == cp - 1:
            ranges[-1][1] = cp
        else:
            ranges.append([cp, cp])
    return [x for r in ranges for x in r]


def upper_first(text):
    cap = first_letter["capital"].get("%X" % ord(text[0])) if text else None
    return text if cap is None else cap + text[1:]


def lower_first(text):
    small = first_letter["small"].get("%X" % ord(text[0])) if text else None
    return text if small is None else small + text[1:]


TAG = re.compile(r"<[^<>\r\n]+>")


def prepared(terms):
    return [(key, sorted({key, upper_first(key), lower_first(key)})) for key in sorted(terms)]


def cites(ordered, source, is_glued, tags_are_markup):
    """Every term against the source, from the rules: the longest first, never overlapping."""
    if not source or "[!L*" in source or any(source == key for key, _ in ordered):
        return []
    spans = [(m.start(), m.end()) for m in TAG.finditer(source)] if tags_are_markup else []

    def edge(i):
        return i <= 0 or i >= len(source) or not (is_glued(ord(source[i])) and is_glued(ord(source[i - 1])))

    found = []
    for number, (key, variants) in enumerate(ordered):
        for variant in variants:
            at = source.find(variant)
            while at >= 0:
                end = at + len(variant)
                if edge(at) and edge(end) and not any(a < end and at < b for a, b in spans):
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


def units(text, i):
    return i + sum(1 for c in text[:i] if ord(c) > 0xFFFF)


def make():
    rnd = random.Random(SEED)
    stems = ["unit", "armor", "shield", "swap", "power", "attack", "battle", "slot", "cannon", "support", "yellow", "orange", "blue", "white", "strength",
             "captain", "guard", "crusher", "bone", "bruce", "lease", "shop", "ship", "time", "activate", "destroy", "adjacent", "tooth", "welcome", "base"]
    singles = sorted({w.capitalize() for w in stems})
    terms = set(singles)
    for _ in range(60):
        a, b = rnd.sample(singles, 2)
        terms.add(a + " " + b)                          # a phrase over two terms: the longest wins, the shorter inside it has no place
    terms |= {"Attack Unit", "Support Unit", "Bone Crusher", "Captain of the Guard"}
    terms = sorted(terms)
    plain = ["the", "of", "to", "your", "when", "with", "every", "a", "and", "is", "this"]
    colours = ["#FF9D73", "#4DFF4D", "#FFFF80", "red", "orange"]

    def word():
        return rnd.choice(terms)

    def decorate(w):
        roll = rnd.random()
        if roll < 0.18: return "*" + w + "*"
        if roll < 0.30: return "<color=%s>%s</color>" % (rnd.choice(colours), w)
        if roll < 0.38: return "<b>" + w + "</b>"
        if roll < 0.46: return "☆" + w + "☆"
        if roll < 0.52: return "#" + w + "#"
        if roll < 0.57: return "[!v*0]" + w
        if roll < 0.62: return "{[!v*0]} " + w
        if roll < 0.66: return "<sprite name=%s>%s" % (rnd.choice(stems), w)
        if roll < 0.70: return "_" + w + "_"              # the low line is glued: no place
        if roll < 0.74: return "%s" + w                    # `%s` is a token ending in a letter: no place
        if roll < 0.78: return "(" + w + ")"
        if roll < 0.82: return w + "s"                     # a plural: no place
        if roll < 0.86: return "@" + w
        return w

    sources = []
    for _ in range(3000):
        tokens = []
        for _ in range(rnd.randint(3, 9)):
            tokens.append(decorate(word()) if rnd.random() < 0.5 else rnd.choice(plain))
        sources.append(" ".join(tokens) + rnd.choice([".", "!", "", ", ok", " <sprite name=cannon>"]))
    # the attributes alone: words that are terms, inside a tag, with nothing outside it
    for stem in rnd.sample(stems, 20):
        sources.append("<sprite=\"common\" name=%s>" % stem)
        sources.append("<color=%s>x</color>" % stem)
    rnd.shuffle(sources)
    return terms, sources


def canonical(places_by_source):
    lines = []
    for index, places in places_by_source:
        for at, end, key, source in places:
            lines.append("%d\t%s\t%d\t%d\n" % (index, key, units(source, at), units(source, end)))
    return "".join(lines)


def render():
    terms, sources = make()
    ordered = prepared(terms)
    new_glue, old_glue = glued_by(RANGES), glued_by(former_table())

    answer, lost, uncovered, found_old, inside = [], 0, 0, 0, 0
    for index, source in enumerate(sources):
        now = cites(ordered, source, new_glue, True)
        before = cites(ordered, source, old_glue, False)
        found_old += len(before)
        answer.append((index, [(a, b, k, source) for a, b, k in now]))
        for a, b, k in before:
            if (a, b, k) in now:
                continue
            lost += 1
            if not any(x <= a and b <= y and (y - x) > (b - a) for x, y, _ in now):
                uncovered += 1
        spans = [(m.start(), m.end()) for m in TAG.finditer(source)]
        inside += sum(1 for a, b, _ in before if any(x < a and b <= y for x, y in spans))
    if uncovered:
        raise SystemExit("%d place(s) of the former table are lost and not covered by a longer one: the rule is wrong, or the file" % uncovered)

    text = canonical(answer)
    total = text.count("\n")
    doc = {
        "note": "Invented sources of the form of a real game's texts (tools/generate-decorations-bench.py): emphasis stars, hash signs, rich-text tags with attributes, placeholders, symbols round a name. `result_sha256` is the SHA-256 of the canonical text of the places a naive reference finds — `source-index TAB key TAB start TAB end LF`, positions in UTF-16 code units — under the rules of word-glue.json and « a tag is not text ». `places_former` is what the former table (Line_Break classes alone, no tag rule) found, `places_lost` the places it found that the new rule does not, `lost_without_a_longer_term` those among them that no LONGER new place covers (the generator refuses to write the file unless it is 0), `former_inside_a_tag` how many of the former places lay inside a tag.",
        "unicode_version": glue["unicode_version"],
        "terms": terms,
        "sources": sources,
        "places": total,
        "result_sha256": hashlib.sha256(text.encode("utf-8")).hexdigest(),
        "places_former": found_old,
        "places_lost": lost,
        "lost_without_a_longer_term": uncovered,
        "former_inside_a_tag": inside,
    }
    return json.dumps(doc, ensure_ascii=False, separators=(",", ":")) + "\n", total, found_old, lost


def main():
    text, total, old, lost = render()
    if "--check" in sys.argv:
        have = io.open(OUT, "rb").read().decode("utf-8") if os.path.exists(OUT) else None
        if have != text:
            print("STALE: %s — run tools/generate-decorations-bench.py" % os.path.normpath(OUT))
            sys.exit(1)
        print("up to date (%d places; the former rule found %d, %d lost, all covered)" % (total, old, lost))
        return
    with io.open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print("wrote %d places (the former rule found %d, %d lost, all covered by a longer one), %d bytes" % (total, old, lost, len(text.encode("utf-8"))))


if __name__ == "__main__":
    main()
