namespace UnityGameTranslator.Common
{
    /// <summary>
    /// What contributions are holding, said the same way wherever it is read.
    ///
    /// 🔴 **Asked for on 2026-07-25 and answered on 2026-08-20**: *"on n'a jamais l'info de si une
    /// branche a commit quelque chose et combien de branches […] le nombre de branches avec des
    /// écarts positifs (pas un mec dans une ancienne version)"*. What shipped in between was a raw
    /// count of branches — including the ones holding nothing, which is the very figure that was
    /// ruled out.
    ///
    /// ⚠ **The numbers are the server's, the words are here.** Comparing files is the site's job
    /// (it holds them); saying what the result means belongs to the socle, or the mod and the
    /// Manager would each phrase the same fact their own way — and a player moving between them
    /// would have to work out that two sentences describe one thing.
    ///
    /// ⚠ Plain international English: whatever is written here is what a Polish, Brazilian or
    /// Korean player reads, and there is nothing else. See CLAUDE.md.
    /// </summary>
    public static class Contributions
    {
        /// <summary>
        /// What is waiting on a Main, in one sentence — for a tooltip, a status line, a signal row.
        ///
        /// Empty when nothing waits: a screen with nothing to say says nothing rather than
        /// announcing a zero.
        ///
        /// ⚠ <paramref name="lines"/> is null on a server too old to count them. The sentence then
        /// says how many contributions without claiming what they carry, because an unknown amount
        /// of work is not the same as none.
        /// </summary>
        public static string WhatIsWaiting(int branches, int? lines)
        {
            if (branches <= 0) return "";

            string who = branches == 1
                ? "1 contribution you have not been through"
                : branches + " contributions you have not been through";

            if (!lines.HasValue || lines.Value <= 0) return who + ".";

            string what = lines.Value == 1 ? "1 line to take" : lines.Value + " lines to take";

            return who + ", holding " + what + ".";
        }

        /// <summary>
        /// WHAT those lines are, in three words and two separators: "21 new · 17 validated".
        ///
        /// 🔴 **Because a total cannot answer "is this worth an evening".** Lines nobody has, lines
        /// somebody retranslated and lines the Main already had that somebody read and stood behind
        /// are three different propositions. The last one changes no text at all — it is the work
        /// this site asks for, and it is precisely the kind a single number hides.
        ///
        /// ⚠ **A zero is left out, never printed.** "0 reworded" is a word asking to be read for
        /// nothing, in the reader's fourth language. The order never changes, so what is shown is
        /// recognised by its place as much as by its label.
        ///
        /// ⚠ Empty when nothing is known — a server too old to break the total down sends nulls,
        /// and the caller then shows the total alone, exactly as it did before.
        /// </summary>
        public static string WhatKindOfWork(int? newLines, int? rewordedLines, int? validatedLines)
        {
            string said = "";

            said = Add(said, newLines, "new");
            said = Add(said, rewordedLines, "reworded");
            said = Add(said, validatedLines, "validated");

            return said;
        }

        private static string Add(string said, int? count, string label)
        {
            if (!count.HasValue || count.Value <= 0) return said;

            string part = count.Value + " " + label;

            return said.Length == 0 ? part : said + " · " + part;
        }

        /// <summary>
        /// The same fact for the person who sent one: what their own contribution is still holding
        /// for its Main.
        ///
        /// 🔴 **Their own, and nobody else's.** What the other contributions carry is not a
        /// contributor's business — the site refuses them the content, and a count would describe
        /// its shape all the same.
        ///
        /// ⚠ Zero has something to say here, unlike the Main's side: it means the work has arrived
        /// — either taken in, or already held by the Main. Silence there would read as a screen
        /// that failed to load.
        /// </summary>
        public static string WhatYouAreOffering(int lines)
        {
            if (lines <= 0) return "Everything you sent is in the Main.";

            return lines == 1
                ? "1 line you sent is not in the Main yet."
                : lines + " lines you sent are not in the Main yet.";
        }
    }
}
