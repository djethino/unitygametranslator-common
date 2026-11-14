using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// What the corpus cannot say about the shape of a key:
    ///
    ///  · corpus/data/key-vectors.json — 3 000 synthetic texts and what the MOD's own function (the one in the field) made
    ///    of them, produced once by a program linking the mod's TextNormalization.cs — is replayed through
    ///    <see cref="KeyShape"/>: same key, same numbers, same trimmed key, same restored text, for every row. The mod
    ///    replays the same file against its own function: the equality is held from both sides without either depending on
    ///    the other;
    ///  · a text with no number and no line end to change comes back as the SAME string and allocates nothing — this runs on
    ///    every text of every frame;
    ///  · the tables carry the same Unicode version as the others.
    /// </summary>
    internal static class KeyShapeChecks
    {
        public static void Run(Action<bool, string, string> check, string? commonRoot)
        {
            check(KeyChars.UnicodeVersion == Casing.UnicodeVersion && KeyChars.UnicodeVersion == WordGlue.UnicodeVersion,
                "the key's character tables carry the same Unicode version as the others (" + KeyChars.UnicodeVersion + ")", "one Unicode for everything");

            check(commonRoot != null, "the library's folder is found", "the vectors are read from it");
            if (commonRoot != null)
            {
                string path = Path.Combine(commonRoot, "corpus", "data", "key-vectors.json");
                check(File.Exists(path), "corpus/data/key-vectors.json is there", "the evidence the key is the one in the field");
                if (File.Exists(path))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(path));
                    int rows = 0, bad = 0;
                    string first = "";
                    foreach (var row in doc.RootElement.GetProperty("rows").EnumerateArray())
                    {
                        rows++;
                        string text = row[0].GetString()!, key = row[1].GetString()!, stored = row[3].GetString()!, restored = row[4].GetString()!;
                        var numbers = row[2].EnumerateArray().Select(x => x.GetString()!).ToList();

                        string? mine = KeyShape.Of(text, true, out var mineNumbers);
                        string? why = null;
                        if (mine != key) why = $"key [{mine}] for [{key}]";
                        else if (!mineNumbers.SequenceEqual(numbers)) why = "numbers [" + string.Join("|", mineNumbers) + "] for [" + string.Join("|", numbers) + "]";
                        else if (KeyShape.Stored(text) != stored) why = $"stored [{KeyShape.Stored(text)}] for [{stored}]";
                        else if (KeyShape.Restore(key, numbers) != restored) why = $"restored [{KeyShape.Restore(key, numbers)}] for [{restored}]";
                        if (why != null) { bad++; if (first.Length == 0) first = why + " — from [" + text.Replace("\n", "\n").Replace("\r", "\r") + "]"; }
                    }
                    check(rows >= 3000, $"{rows} rows of the mod's own answers are read", "an empty replay passes everything");
                    check(bad == 0, $"all of them are the same through the socle ({rows} rows)", bad + " differ, the first: " + first);
                }
            }

            string plain = "Une phrase ordinaire de soixante caracteres, sans chiffre ni fin de ligne.";
            KeyShape.Of(plain, true, out _);
            KeyShape.Stored(plain);
            long before = GC.GetAllocatedBytesForCurrentThread();
            string? keyBack = KeyShape.Of(plain, true, out var none);
            string? storedBack = KeyShape.Stored(plain);
            long after = GC.GetAllocatedBytesForCurrentThread();
            check(ReferenceEquals(keyBack, plain) && ReferenceEquals(storedBack, plain) && none.Count == 0 && after - before == 0,
                "a text with nothing to change comes back as the same string, allocating nothing (" + (after - before) + " bytes)",
                "every text of every frame goes through here");
        }
    }
}
