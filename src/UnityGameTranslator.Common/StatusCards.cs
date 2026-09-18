namespace UnityGameTranslator.Common
{
    /// <summary>
    /// The line under a translation's identity on its card: what it is, in one sentence.
    ///
    /// ⚠ <see cref="Text"/> is a sentence for the interface's own translation, ending just before
    /// any name; the name travels apart in <see cref="Mention"/> and the screen appends it.
    /// </summary>
    public struct CardLine
    {
        /// <summary>The sentence, or null for no line at all.</summary>
        public string? Text;

        /// <summary>A username the screen appends, as data, or null.</summary>
        public string? Mention;

        /// <summary>
        /// Work waiting, rather than a fact. The only sentence on the card that asks for
        /// something, so it is not written in the colour of a footnote.
        /// </summary>
        public bool NeedsAttention;
    }

    /// <summary>How loud a card's notice is.</summary>
    public enum NoticeTone
    {
        /// <summary>Something to know; nothing is broken.</summary>
        Warning,
        /// <summary>A road that has ended.</summary>
        Error,
    }

    /// <summary>A notice the card shows under its lines, with the one verb it offers.</summary>
    public struct CardNotice
    {
        public string Text;
        public NoticeTone Tone;

        /// <summary>A judgement about somebody else is said once and can be put away; a fact about the file cannot.</summary>
        public bool Dismissable;

        /// <summary>The word on its button.</summary>
        public string Verb;
    }

    /// <summary>
    /// What a translation's card says about it, unasked — decided once so that a card in the
    /// game and a row in a tool describe one file the same way.
    /// </summary>
    public static class StatusCards
    {
        /// <summary>
        /// The line under the identity, from where the translation stands.
        ///
        /// 🔴 **Four sentences, one per standing, written here rather than in four methods of the
        /// card** (2026-09-16). A Main is told what waits on it, or that it is its own; a branch
        /// what it has not sent yet and whose Main it hangs from; somebody holding another's
        /// lineage whose work it is; a file never published, what to do with it.
        ///
        /// ⚠ A count stays inline — the interface's translation placeholders numbers — and a
        /// name never enters the sentence: it is appended by the screen, as data.
        /// </summary>
        public static CardLine Secondary(Standing standing, ServerFacts server, int localChanges)
        {
            if (Standings.LeadsTheLineage(standing))
            {
                int waiting = standing.BranchesWaiting ?? 0;
                if (waiting <= 0)
                    return new CardLine { Text = "You own this translation" };

                // The socle's words, so this line and the Manager's signal row say one thing —
                // and the one sentence on the card asking the owner to do something.
                return new CardLine
                {
                    Text = Contributions.WhatIsWaiting(waiting, standing.LinesAvailable),
                    NeedsAttention = true,
                };
            }

            if (Standings.OnABranch(standing))
            {
                // What is waiting before where it goes: a contributor opening this is answering
                // "have I got work nobody has seen yet".
                string state = localChanges > 0
                    ? localChanges + " changes not sent yet"
                    : "Everything sent";
                // 🔴 The Main's name comes from the Main's row and nowhere else. On one's own
                // branch, `Uploader` is the branch's own author — the reader — and falling back
                // to it named them as the owner of their own contribution the moment the Main was
                // gone: "your branch of @you" under "The Main was removed by its author"
                // (2026-09-18). With no Main to name, the state stands alone; the notice above
                // says what became of it.
                string? owner = server.MainUsername;
                bool named = !string.IsNullOrEmpty(owner);

                return new CardLine
                {
                    Text = named ? state + " · your branch of" : state,
                    Mention = named ? owner : null,
                };
            }

            if (standing.Publication == Publication.NotYours)
            {
                // Whose work this is. The three buttons offering the ways out sit immediately
                // below, each with its own label, so this names nothing they already say.
                bool named = !string.IsNullOrEmpty(standing.MainOwner);
                return new CardLine
                {
                    Text = named ? "Based on the translation of" : null,
                    Mention = named ? standing.MainOwner : null,
                };
            }

            if (standing.Publication == Publication.NeverPublished)
                return new CardLine { Text = "Upload to share with others" };

            return new CardLine();
        }

        /// <summary>
        /// The notice under the card's lines, for an author looking at their own published
        /// translation — or null when there is nothing to say.
        ///
        /// ⚠ **Only once it is PUBLISHED and theirs.** A file being built in capture mode is
        /// normal work, and warning about it would be noise; somebody else's lineage has its own
        /// wall, said by the button.
        ///
        /// 🔴 **The order is the order of what explains the most.** A Main that is gone first: it
        /// is the one nobody else can fix. Then a Main whose owner is gone — it ends like the
        /// orphan (nobody will ever merge this) and reads like the closure below (the translation
        /// is still there). Then the road that closed. Those three are facts about the file and
        /// cannot be put away. Then a Main that took nothing in — a judgement about somebody, said
        /// once, with a way to put it away for good. Last, a published translation with no
        /// translated line in it.
        /// </summary>
        /// <param name="ignoringDismissed">The person has put the "not taking it in" notice away.</param>
        /// <param name="captureOnly">The file holds captured lines and not one translated.</param>
        public static CardNotice? Notice(Standing standing, ServerFacts server, bool ignoringDismissed, bool captureOnly)
        {
            if (standing.Publication != Publication.Published || !server.IsOwner) return null;

            // The FACT half of the wall, the same words as everywhere else. The way out is said
            // beside the button that is the way out (Uploads.Button), not here: the card states
            // where the translation stands, it offers no act on it.
            if (standing.MainMissing)
                return new CardNotice { Text = Walls.MainMissing.Fact, Tone = NoticeTone.Error, Verb = "Manage online" };

            if (standing.MainAbandoned)
                return new CardNotice { Text = Walls.MainAbandoned.Fact, Tone = NoticeTone.Error, Verb = "Manage online" };

            if (standing.BranchFrozen)
                return new CardNotice { Text = Walls.BranchFrozen.Fact, Tone = NoticeTone.Error, Verb = "Manage online" };

            if (server.MainIgnoring == true && !ignoringDismissed)
            {
                return new CardNotice
                {
                    Text = "The Main does not seem to be taking the new work into account. You can publish your own version whenever you like.",
                    Tone = NoticeTone.Warning, Dismissable = true, Verb = "Manage online",
                };
            }

            if (captureOnly)
            {
                return new CardNotice
                {
                    Text = "Published with no translated line: players who download it see nothing change.",
                    Tone = NoticeTone.Warning, Verb = "Manage online",
                };
            }

            return null;
        }
    }
}
