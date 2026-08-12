using System;
using System.Collections.Generic;
using UnityGameTranslator.Common;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// ⚠ The vectors below are FROZEN, and they do not come from this code.
    ///
    /// They were produced by a reference implementation of Translation::computeHash — the website's
    /// — and that implementation was itself checked against reality first: run over a real 690-line
    /// published translation sitting on disk, it reproduced the file_hash the server had issued for
    /// that file. So these are not "what our code does", they are what the server does.
    ///
    /// That distinction is the whole value of this file. A hash that only agrees with itself would
    /// pass every check here and make every translation in existence look permanently out of sync.
    /// </summary>
    public static class SyncChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            Console.WriteLine();
            Console.WriteLine("Sync");
            Console.WriteLine("----");

            // The shape itself: an empty file is still a document, and it still carries its uuid.
            check(Hash(new (string, string, string)[0], "u")
                  == "22a4518cef1703e1bf2a64be7e4f37f6a525a7bb497e437652020cf8499a295c",
                "an empty translation still hashes", "the uuid is content too");

            check(Hash(new[] { ("Hello", "Bonjour", "H") }, "abc-123")
                  == "4716ae6801436c85be8347fc0f13d852f96014f02d0c0a2e9efc9fd325957a7e",
                "one line, one tag", "the tag is part of the content, not a decoration");

            // ⚠ ORDINAL, which puts "A" before "_uuid" before "a". A culture-aware sort puts "a"
            // first and produces a different document — on one machine and not another.
            check(Hash(new[] { ("b", "2", "A"), ("A", "1", "A"), ("a", "3", "V") }, "u")
                  == "2c96775b49359e571fd2559699e80ded94dc5190bff42826399e78caa5fc3f7f",
                "keys sort by byte, not by culture", "and the uuid sorts among them");

            // ⚠ Literal, not escaped. Both are valid JSON; only one is the right bytes.
            check(Hash(new[] { ("Cafe", "Café où", "H") }, "u")
                  == "374295d505da97c1893854218116fd3e0dd7ee4686fa1c53e7e586b554ee0c95",
                "accents stay as themselves", "JSON_UNESCAPED_UNICODE, and the mod agrees");

            check(Hash(new[] { ("Play", "遊ぶ", "A") }, "u")
                  == "993eb1ead3a9fa19ebb6b66276948efa348a684698206bf471422c79e5bf579e",
                "and so does anything else", "the games that most need translating are these ones");

            check(Hash(new[] { ("a/b", "c/d", "A") }, "u")
                  == "4f062cf92528d172be2a8b0db6028349a5ebc46730327b72b89a97ef34c52810",
                "a solidus is not escaped", "PHP escapes it unless told not to; we tell it not to");

            // The escapes that ARE required, and their exact spelling.
            check(Hash(new[] { ("say \"hi\"", "dit \"salut\"", "A") }, "u")
                  == "dda0ecb643d94b807b83945281630a94e7c31d70c193597821f933f6fad64ac8",
                "quotes are escaped", "in keys as well as in values");

            check(Hash(new[] { ("a\\b", "c\\d", "A") }, "u")
                  == "164e61b783b2374222ad4574170e414232efd862b57fdb19e6ca01288bfb89ee",
                "and so are backslashes", "a game full of \\n literals would break otherwise");

            check(Hash(new[] { ("a\tb", "c\td", "A") }, "u")
                  == "dcea12ef3525bf6aca2b5790ff1b86705c47c5a9640e26c77b11c24caa90f4e9",
                "a tab uses its short form", "not \\u0009");

            check(Hash(new[] { ("a", "c\nd", "A") }, "u")
                  == "6d7b36e2e20d8334c3b0ba2dc70a9a74a35bcadf90dbe89f7bc4721b1605b7d2",
                "a newline too", "game text is full of them");

            check(Hash(new[] { ("a", "cd", "A") }, "u")
                  == "067e75fd952bd70a422ccebdbdd0ec058b46ef120b2ebb4a615ff645123bc556",
                "and the rest go to lower-case \\u00xx", "four digits, whatever the character");

            // What must NOT change the answer.
            check(Hash(new[] { ("Hello", "Bonjour", "H") }, "abc-123")
                  == Hash(new[] { ("Hello", "Bonjour", "H"), ("_local_changes", "9", "A") }, "abc-123"),
                "metadata keys are excluded", "a counter must not change a file's identity");

            check(Hash(new[] { ("a", "1", "A"), ("b", "2", "A") }, "u")
                  == Hash(new[] { ("b", "2", "A"), ("a", "1", "A") }, "u"),
                "the order they arrive in is irrelevant", "two machines meet lines in different orders");

            // ⚠ The file is hashed AS WRITTEN, and these three cases were found by running the mod's
            // own implementation against this one rather than by reading either. hashableEntry
            // keeps only what an entry holds among v and t, in the file's order, and hands back a
            // pre-tag bare string untouched — so tidying any of it produces a different hash for a
            // file nobody edited.
            check(Hash(new[] { ("a", "1", (string)null) }, "u")
                  == "e554b432e93283de2b870573a7ef23e458a32399ff746bd2cc8a21d488410338",
                "an absent tag stays absent", "it is NOT filled in with the default");

            check(Hash(new[] { ("a", (string)null, "A") }, "u")
                  == "6127fc7d1af8abd3ecea62fb043d7ec47d6fe13d452a09fb50754f94ac124ba3",
                "a null value stays null", "not an empty string; they are different documents");

            check(Bare("a", "just text", "u")
                  == "ddb6a47d6edc8e2ee2442e11de8124f6d6cc1e1b9371f4523af047bc36a5e78e",
                "a pre-tag entry stays a bare string", "an old published file must still match");

            check(Mixed() == "564754a6f7d13ce8454cf5b8b9fc01571ff8197f9fb4699328752b5c932c4106",
                "and the two forms coexist in one file", "which is what an old file looks like");

            // ⚠ Which direction, and the two that are easy to get backwards.
            check(Sync.Decide("same", "same", null, true) == SyncDirection.InSync,
                "identical content is in sync", "whatever the change counter says");

            check(Sync.Decide("mine", "theirs", "theirs", true) == SyncDirection.Upload,
                "my changes on an unmoved server is an upload", "nothing of theirs is at risk");

            check(Sync.Decide("mine", "theirs", "older", false) == SyncDirection.Download,
                "their changes on an untouched file is a download", "nothing of mine is at risk");

            check(Sync.Decide("mine", "theirs", "older", true) == SyncDirection.Merge,
                "both moved is a merge", "and only the mod can settle it line by line");

            // ⚠ The case that decides whether somebody's own work survives.
            check(Sync.Decide("mine", "theirs", null, true) == SyncDirection.Merge,
                "no ancestor plus local work is a conflict, not a download",
                "that is a translation somebody built themselves and never published");

            check(Sync.Decide("mine", "theirs", null, false) == SyncDirection.Download,
                "no ancestor and nothing of mine is still a download", "there is nothing to lose");

            check(Sync.Decide("mine", null, null, true) == SyncDirection.InSync,
                "nothing published means nothing to reconcile", "not an upload nobody asked for");
        }

        private static string Bare(string key, string value, string uuid) =>
            ContentHash.Of(new[]
            {
                new KeyValuePair<string, TranslationLine>(key, TranslationLine.Bare(value)),
            }, uuid);

        private static string Mixed() =>
            ContentHash.Of(new[]
            {
                new KeyValuePair<string, TranslationLine>("a", TranslationLine.Bare("old")),
                new KeyValuePair<string, TranslationLine>("b", new TranslationLine("new", "H")),
            }, "u");

        private static string Hash((string Key, string Value, string Tag)[] lines, string uuid)
        {
            var map = new List<KeyValuePair<string, TranslationLine>>();
            foreach (var line in lines)
                map.Add(new KeyValuePair<string, TranslationLine>(
                    line.Key, new TranslationLine(line.Value, line.Tag)));

            return ContentHash.Of(map, uuid);
        }
    }
}
