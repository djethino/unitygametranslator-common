#!/usr/bin/env python3
"""Writes FontLayouts.Tables.g.cs: the shape of Unity's Font object (class 128) in every Unity version.

Why: a game's data files usually carry no type tree (stripped in release builds), so reading a Font
— and the font file it embeds (m_FontData) — needs its field layout for the game's Unity version.
That layout is a fact of the engine, published per version by AssetRipper/Tpk (MIT) as a "type
tree" tpk. Only the Font class is taken from it, written as a table the git diff can be read in,
with the SHA-256 of the tpk it came from. The common strings (Unity's built-in string buffer, which
an embedded type tree points into) are written too.

The tpk is not kept here: it is fetched (or given) at generation time. The release chain refreshes
it when Unity ships a version whose layout this table does not have yet.

Format of the tpk, as AssetsTools.NET 3.0.5 reads it (ClassPackageFile): "TPK*", file version 1 or
2, compression (0 none, 1 LZ4, 2 LZMA: 5 property bytes then the data), then a type tree: creation
time, versions, classes (per version: release / editor root node), common strings, nodes, strings.

Usage:
    python generate-font-layouts.py                  # fetch the latest type_tree tpk, regenerate
    python generate-font-layouts.py --tpk lzma.tpk   # from a tpk on disk
    python generate-font-layouts.py --tpk lzma.tpk --check   # exit 1 when the table is not this tpk's
"""

import argparse
import hashlib
import io
import lzma
import os
import struct
import sys
import tempfile
import urllib.request
import zipfile

SOURCE_URL = "https://nightly.link/AssetRipper/Tpk/workflows/type_tree_tpk/master/lzma_file.zip"
HERE = os.path.dirname(os.path.abspath(__file__))
TARGET = os.path.join(HERE, "..", "src", "UnityGameTranslator.Common", "UnityFiles", "FontLayouts.Tables.g.cs")
FONT_CLASS = 128
TYPE_LETTERS = "abcfpx"   # UnityVersion type byte: alpha, beta, china, final, patch, experimental


def fetch():
    folder = tempfile.mkdtemp(prefix="ugt-tpk-")
    archive = os.path.join(folder, "lzma_file.zip")
    urllib.request.urlretrieve(SOURCE_URL, archive)
    with zipfile.ZipFile(archive) as z:
        names = [n for n in z.namelist() if n.endswith(".tpk")]
        if len(names) != 1:
            sys.exit(f"expected one .tpk in {SOURCE_URL}, found {names}")
        z.extract(names[0], folder)
        return os.path.join(folder, names[0])


def version_text(v):
    kind = (v >> 8) & 0xFF
    letter = TYPE_LETTERS[kind] if kind < len(TYPE_LETTERS) else "?"
    return f"{(v >> 48) & 0xFFFF}.{(v >> 32) & 0xFFFF}.{(v >> 16) & 0xFFFF}{letter}{v & 0xFF}"


def read_tpk(path):
    data = open(path, "rb").read()
    magic, file_version, compression, _data_type, _, _, compressed, decompressed = struct.unpack_from("<4sBBBBIII", data, 0)
    if magic != b"TPK*" or file_version not in (1, 2):
        sys.exit(f"{path}: not a type tree tpk this generator knows (magic {magic!r}, version {file_version})")
    body = data[20:20 + compressed]
    if compression == 2:
        raw = lzma.LZMADecompressor(format=lzma.FORMAT_ALONE).decompress(body[:5] + struct.pack("<Q", decompressed) + body[5:])
    elif compression == 0:
        raw = body
    else:
        sys.exit(f"{path}: compression {compression} not handled (take the LZMA tpk)")
    if len(raw) != decompressed:
        sys.exit(f"{path}: {len(raw)} bytes decompressed, {decompressed} announced")

    r = io.BytesIO(raw)

    def rd(fmt):
        return struct.unpack(fmt, r.read(struct.calcsize(fmt)))

    def read_7bit():
        n = shift = 0
        while True:
            b = r.read(1)[0]
            n |= (b & 0x7F) << shift
            shift += 7
            if b < 0x80:
                return n

    rd("<q")
    (nv,) = rd("<i")
    versions = [rd("<Q")[0] for _ in range(nv)]
    (nc,) = rd("<i")
    classes = {}
    for _ in range(nc):
        cid, n = rd("<ii")
        entries = []
        for _ in range(n):
            (v,) = rd("<Q")
            (has,) = rd("<?")
            release = None
            if has:
                _name, _base, flags = rd("<HHB")
                if flags & 0x40:
                    rd("<H")
                if flags & 0x80:
                    (release,) = rd("<H")
            entries.append((v, release))
        classes[cid] = entries
    if file_version != 2:
        sys.exit("tpk file version 1: its common strings need the v1 reader, not written here")
    (ncs,) = rd("<i")
    common = []
    for _ in range(ncs):
        (v,) = rd("<Q")
        (k,) = rd("<i")
        common.append((v, [rd("<HH") for _ in range(k)]))
    (nn,) = rd("<i")
    nodes = []
    for _ in range(nn):
        type_name, field_name, byte_size, _node_version, type_flags, meta_flags, nsub = rd("<HHiHBIH")
        subs = rd("<" + "H" * nsub) if nsub else ()
        nodes.append((type_name, field_name, byte_size, type_flags, meta_flags, subs))
    (ns,) = rd("<i")
    strings = []
    for _ in range(ns):
        length = read_7bit()
        strings.append(r.read(length).decode("utf-8"))
    if r.tell() != len(raw):
        sys.exit(f"{path}: {len(raw) - r.tell()} bytes left after the string table")
    return max(versions), classes, common, nodes, strings


def flatten(nodes, strings, index, depth, out):
    type_name, field_name, byte_size, type_flags, meta_flags, subs = nodes[index]
    out.append((depth, strings[type_name], strings[field_name], byte_size, type_flags, meta_flags))
    for s in subs:
        flatten(nodes, strings, s, depth + 1, out)
    return out


def cs(s):
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'


def render(tpk, sha):
    newest, classes, common, nodes, strings = read_tpk(tpk)
    if FONT_CLASS not in classes:
        sys.exit("no Font class in this tpk")
    layouts, keys = [], []
    for v, release in classes[FONT_CLASS]:
        tree = tuple(flatten(nodes, strings, release, 0, [])) if release is not None else None
        index = -1
        if tree is not None:
            if tree not in layouts:
                layouts.append(tree)
            index = layouts.index(tree)
        if keys and keys[-1][1] == index:
            continue   # same layout as the version before: one entry per change
        keys.append((v, index))
    last_common = [strings[i] for _, i in common[-1][1]]

    out = io.StringIO()
    w = out.write
    w("// <auto-generated>\n")
    w("//   By common/tools/generate-font-layouts.py from AssetRipper/Tpk's type tree tpk (MIT),\n")
    w(f"//   SHA-256 {sha}. Do not edit: rerun the generator.\n")
    w("// </auto-generated>\n")
    w("namespace UnityGameTranslator.Common.UnityFiles\n{\n")
    w("    public static partial class FontLayouts\n    {\n")
    w("        /// <summary>SHA-256 of the tpk these tables were written from.</summary>\n")
    w(f"        public const string SourceSha256 = \"{sha}\";\n\n")
    w(f"        /// <summary>The newest Unity version the source knew: {version_text(newest)} — a newer one may have changed Font.</summary>\n")
    w(f"        internal const ulong NewestKnown = 0x{newest:016X};\n\n")
    w("        /// <summary>From which Unity version (UnityVersions.Key) each layout applies; -1 = no Font then.</summary>\n")
    w("        private static readonly ulong[] Since =\n        {\n")
    for v, _ in keys:
        w(f"            0x{v:016X}, // {version_text(v)}\n")
    w("        };\n\n")
    w("        private static readonly int[] LayoutAt = { " + ", ".join(str(i) for _, i in keys) + " };\n\n")
    w("        /// <summary>Each distinct release layout of Font, in node order (depth first).</summary>\n")
    w("        private static readonly TypeNode[][] Layouts =\n        {\n")
    for i, tree in enumerate(layouts):
        first = next(version_text(v) for v, j in keys if j == i)
        w(f"            new[] // since {first}\n            {{\n")
        for depth, type_name, field_name, byte_size, type_flags, meta_flags in tree:
            w(f"                new TypeNode({depth}, {cs(type_name)}, {cs(field_name)}, {byte_size}, {type_flags}, 0x{meta_flags:X}),\n")
        w("            },\n")
    w("        };\n\n")
    w("        /// <summary>Unity's common strings, in buffer order: an embedded type tree names them by offset.</summary>\n")
    w("        internal static readonly string[] CommonStrings =\n        {\n")
    for k in range(0, len(last_common), 6):
        w("            " + ", ".join(cs(s) for s in last_common[k:k + 6]) + ",\n")
    w("        };\n    }\n}\n")
    return out.getvalue().replace("\r\n", "\n")


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    p = argparse.ArgumentParser()
    p.add_argument("--tpk")
    p.add_argument("--check", action="store_true")
    a = p.parse_args()
    tpk = a.tpk or fetch()
    sha = hashlib.sha256(open(tpk, "rb").read()).hexdigest()
    text = render(tpk, sha)
    current = open(TARGET, "rb").read().decode("utf-8") if os.path.exists(TARGET) else None
    if a.check:
        if current != text:
            print(f"{TARGET}: not this tpk's table ({sha})")
            sys.exit(1)
        print("up to date")
        return
    os.makedirs(os.path.dirname(TARGET), exist_ok=True)
    with open(TARGET, "wb") as f:
        f.write(text.encode("utf-8"))
    print(f"written from {tpk} ({sha})" + ("" if current != text else " — unchanged"))


if __name__ == "__main__":
    main()
