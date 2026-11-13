#!/usr/bin/env python3
"""A second reference for the content hash of a translation file, written from the specification alone.

Why: the vectors `sync/content_hash/v2-*` of corpus/rules/sync.json freeze the hash of the fields of the
format 2. They must not come from the C# they check, so they were produced here, with Python's own JSON
writer (separators (',', ':'), ensure_ascii=False, keys sorted by UTF-8 bytes). The order of the fields
is the one Sync.cs (ContentHash.WriteFormat2) documents; the website's Translation::computeHash must
produce the same bytes. `python format2-hash-reference.py` checks itself against two format 1 vectors.

Usage as a module: content_hash(lines, uuid) -> (hex digest, canonical text).
"""
# (schema.json / Sync.cs comment), NOT from the C#. Python's json module does the escaping.
import json, hashlib, sys

def dumps(x):
    return json.dumps(x, separators=(',', ':'), ensure_ascii=False)

def line_doc(line):
    if isinstance(line, str):
        return line
    out = {}
    out['v'] = line.get('v')
    if 't' in line and line['t'] is not None:
        out['t'] = line['t']
    if line.get('term') is True:
        out['term'] = True
    if line.get('forms'):
        out['forms'] = {k: line['forms'][k] for k in sorted(line['forms'], key=lambda k: k.encode('utf-8'))}
    if line.get('use'):
        u = {}
        for k in sorted(line['use'], key=lambda k: k.encode('utf-8')):
            val = line['use'][k]
            if isinstance(val, dict):
                val = {c: val[c] for c in sorted(val, key=lambda c: c.encode('utf-8'))}
            u[k] = val
        out['use'] = u
    if line.get('class'):
        out['class'] = {k: line['class'][k] for k in sorted(line['class'], key=lambda k: k.encode('utf-8'))}
    if line.get('oracle'):
        out['oracle'] = line['oracle']
    if line.get('links'):
        out['links'] = [{f: l[f] for f in ('to', 'form', 'case', 'by', 'was', 'own') if f in l} for l in line['links']]
    if line.get('unlinked'):
        out['unlinked'] = [{'to': x['to']} for x in sorted(line['unlinked'], key=lambda x: x['to'].encode('utf-8'))]
    return out

def content_hash(lines, uuid):
    doc = {k: line_doc(v) for k, v in lines.items()}
    doc['_uuid'] = uuid
    items = sorted(doc.items(), key=lambda kv: kv[0].encode('utf-8'))
    text = '{' + ','.join(dumps(k) + ':' + dumps(v) for k, v in items) + '}'
    return hashlib.sha256(text.encode('utf-8')).hexdigest(), text

if __name__ == '__main__':
    # self-check against two frozen format-1 vectors of corpus/rules/sync.json
    h, _ = content_hash({}, 'u'); assert h == '22a4518cef1703e1bf2a64be7e4f37f6a525a7bb497e437652020cf8499a295c', h
    h, _ = content_hash({'Hello': {'v': 'Bonjour', 't': 'H'}}, 'abc-123'); assert h == '4716ae6801436c85be8347fc0f13d852f96014f02d0c0a2e9efc9fd325957a7e', h
    print('reference agrees with the frozen format-1 vectors')
