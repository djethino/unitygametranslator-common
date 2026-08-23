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
        /// What is waiting, on the two axes a Main actually weighs a review on:
        /// "56 to review: 21 new (H 21) · 35 differing (V 17, A 18)".
        ///
        /// 🔴 **Two measures, and neither follows from the other.** How many rows need a decision
        /// (new lines plus lines both sides hold differently) is not how many are worth taking —
        /// on the lineage this was written against, 56 and 38, the 18 in between being two machine
        /// translations that differ. One answers "how long will this take", the other "is there
        /// anything here for me", and a screen showing one of them cannot answer both.
        ///
        /// 🔴 **The tags say whether it is worth the evening.** 21 new lines all written by hand is
        /// not the same proposition as 21 the machine produced, and the letters are the ones every
        /// screen of this project already shows — H, V, A, S — so nobody has to learn a word.
        ///
        /// ⚠ **A zero is left out**, and the order never changes: a kind is recognised by its place
        /// as much as by its letter. Empty when nothing is known — a server too old to answer sends
        /// nothing, and the caller then shows the total alone, exactly as it did before.
        /// </summary>
        public static string WhatKindOfWork(int? review, TagTally added, TagTally differing)
        {
            if (!review.HasValue || review.Value <= 0) return "";

            string said = ToReview(review);
            string parts = "";

            foreach (WorkKind kind in KindsOfWork(added, differing))
            {
                string part = kind.Total + " " + kind.Label;
                string letters = kind.Tally.Letters();

                if (letters.Length > 0) part += " (" + letters + ")";

                parts = parts.Length == 0 ? part : parts + " · " + part;
            }

            return parts.Length == 0 ? said : said + ": " + parts;
        }

        /// <summary>
        /// The same answer in pieces, for a screen that DRAWS rather than prints.
        ///
        /// 🔴 **One source of order and of which zeros are left out.** H V A S are coloured chips
        /// on the website, and the mod and the Manager should show that same mark rather than the
        /// same letter in grey prose. A chip cannot come out of a sentence, so the pieces are
        /// handed over — but built by the code the sentence itself uses, or the day one of them
        /// starts hiding a zero the two stop agreeing and nothing says so.
        ///
        /// ⚠ The caller decides how to draw a piece, never what the pieces are: order, labels and
        /// exclusions stay here, which is the whole reason the socle carries this at all.
        /// </summary>
        /// <summary>
        /// "21 to review", or nothing at all — the head of the sentence, on its own.
        ///
        /// ⚠ Here rather than repeated in whoever draws the pieces: a screen composing chips still
        /// has to open with these words, and two wordings for one fact is the thing this class
        /// exists to prevent.
        /// </summary>
        public static string ToReview(int? review)
        {
            if (!review.HasValue || review.Value <= 0) return "";

            return review.Value + " to review";
        }

        public static WorkKind[] KindsOfWork(TagTally added, TagTally differing)
        {
            int count = 0;
            if (added.Total > 0) count++;
            if (differing.Total > 0) count++;

            WorkKind[] kinds = new WorkKind[count];
            int at = 0;

            // ⚠ Added before differing, always. A line the Main does not hold at all is a different
            // proposition from one it holds otherwise, and the order is what tells them apart at a
            // glance — the order the sentence has always used.
            if (added.Total > 0) kinds[at++] = new WorkKind("new", added);
            if (differing.Total > 0) kinds[at++] = new WorkKind("differing", differing);

            return kinds;
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

    /// <summary>
    /// How many lines of each quality, in the four letters this whole project already uses.
    ///
    /// ⚠ A struct rather than four loose integers: they are read together, printed together, and a
    /// caller that swaps two of them produces a sentence nobody can tell is wrong.
    /// </summary>
    public struct TagTally
    {
        /// <summary>Written by a person.</summary>
        public int Human;

        /// <summary>Read by a person, who stood behind what the machine wrote.</summary>
        public int Validated;

        /// <summary>The machine's, with nobody's word on it. Also covers a line carrying no tag.</summary>
        public int Machine;

        /// <summary>A person ruled this line must not be translated.</summary>
        public int Skipped;

        public int Total
        {
            get { return Human + Validated + Machine + Skipped; }
        }

        /// <summary>
        /// "V 17, A 18" — best quality first, zeros left out.
        ///
        /// ⚠ H and S are NOT merged, although the merge ladder ranks them level. One says somebody
        /// wrote the line, the other says somebody ruled it must not be written; printing them
        /// under one word would report as translated what nobody translated.
        /// </summary>
        public string Letters()
        {
            string said = "";

            said = Append(said, Human, "H");
            said = Append(said, Validated, "V");
            said = Append(said, Machine, "A");
            said = Append(said, Skipped, "S");

            return said;
        }

        private static string Append(string said, int count, string letter)
        {
            if (count <= 0) return said;

            string part = letter + " " + count;

            return said.Length == 0 ? part : said + ", " + part;
        }

        /// <summary>
        /// The same four figures as letter/count pairs, for a caller that draws a chip per letter.
        ///
        /// ⚠ Same order and same exclusion of zeros as <see cref="Letters"/>, and deliberately
        /// beside it rather than derived from its string: parsing a sentence back into data is how
        /// a comma in a language ends up changing a count.
        /// </summary>
        public TagCount[] Counted()
        {
            int n = 0;
            if (Human > 0) n++;
            if (Validated > 0) n++;
            if (Machine > 0) n++;
            if (Skipped > 0) n++;

            TagCount[] counted = new TagCount[n];
            int at = 0;

            if (Human > 0) counted[at++] = new TagCount("H", Human);
            if (Validated > 0) counted[at++] = new TagCount("V", Validated);
            if (Machine > 0) counted[at++] = new TagCount("A", Machine);
            if (Skipped > 0) counted[at++] = new TagCount("S", Skipped);

            return counted;
        }
    }

    /// <summary>One letter and how many lines carry it.</summary>
    public struct TagCount
    {
        public readonly string Letter;
        public readonly int Count;

        public TagCount(string letter, int count)
        {
            Letter = letter;
            Count = count;
        }
    }

    /// <summary>
    /// One kind of work a contribution is holding — lines the Main does not have, or lines it has
    /// differently — with how many and of what quality.
    /// </summary>
    public struct WorkKind
    {
        /// <summary>"new" or "differing", in the words the sentence uses.</summary>
        public readonly string Label;

        public readonly TagTally Tally;

        public WorkKind(string label, TagTally tally)
        {
            Label = label;
            Tally = tally;
        }

        public int Total
        {
            get { return Tally.Total; }
        }
    }
}
