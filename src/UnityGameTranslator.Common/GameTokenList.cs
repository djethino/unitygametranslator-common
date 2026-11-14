using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// The GAME'S TOKENS of a file: the pieces the game's own code fills with a name or a word when it shows a text
    /// (<c>#PlayerName#</c>, <c>{p3}</c>) — written in the file as <c>_lists.tokens</c> (spec/translation-file/tokens.md).
    ///
    /// A token is a literal text, never a pattern: the extraction finds it in the game's code (a text the code passes to
    /// <c>String.Replace</c> and replaces by a computed one) and writes it; the mod reads it to keep it out of a model's hands
    /// and to find the text again once the game has filled it; the Manager reads it to protect it in a cold translation.
    /// It has no translation of its own, and it is not <c>_variables</c> (the values the MOD reads in a running game, declared one
    /// by one): same effect on a text — a protected hole — another origin.
    ///
    /// Pure, and with no JSON library: the caller parses and serialises, this holds the rules.
    /// </summary>
    public static class GameTokenList
    {
        /// <summary>Whether a text may be a token: not empty, no carriage return (a text of the format 2 is LF alone), no link or slot marker.</summary>
        public static bool IsValid(string? token) => Refusal(token) == null;

        /// <summary>
        /// The tokens as a READER takes them: those that may be tokens (<see cref="IsValid"/>), each once, in the order given. A
        /// reader is tolerant — a file already on disk is read as far as it can be — and says nothing; a WRITER is held to
        /// <see cref="Refusal(IReadOnlyList{string})"/>.
        /// </summary>
        public static IReadOnlyList<string> Read(IEnumerable<string?>? tokens)
        {
            var kept = new List<string>();
            if (tokens == null) return kept;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string? token in tokens)
                if (token != null && IsValid(token) && seen.Add(token)) kept.Add(token);
            return kept;
        }

        /// <summary>
        /// The tokens as a WRITER must write them: each once, in the ordinal order of their texts (UTF-16 code units) — so that two
        /// extractions of one game write the same bytes. Nothing is dropped: an invalid token stays, to be refused.
        /// </summary>
        public static IReadOnlyList<string> Canonical(IEnumerable<string> tokens)
        {
            if (tokens == null) throw new ArgumentNullException(nameof(tokens));
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (string token in tokens) if (token != null) set.Add(token);
            var sorted = new List<string>(set);
            sorted.Sort(StringComparer.Ordinal);
            return sorted;
        }

        /// <summary>Why this text may not be a token, or null.</summary>
        public static LineRefusal? Refusal(string? token)
        {
            if (string.IsNullOrEmpty(token))
                return new LineRefusal { Code = LineWrites.TokenInvalid, Words = "A game token is empty: it would be found in every text." };
            if (token!.IndexOf('\r') >= 0)
                return new LineRefusal { Code = LineWrites.TokenInvalid, Words = "The game token \"" + token.Replace("\r", "\\r") + "\" holds a carriage return: the texts of the format 2 use LF alone." };
            if (token.IndexOf("[!", StringComparison.Ordinal) >= 0)
                return new LineRefusal { Code = LineWrites.TokenInvalid, Words = "The game token \"" + token + "\" holds a marker: a token is a text of the game, never a slot or a link." };
            return null;
        }

        /// <summary>Why this list may not be written as <c>_lists.tokens</c>, or null: every token valid, and strictly in the ordinal order (so each once).</summary>
        public static LineRefusal? Refusal(IReadOnlyList<string> tokens)
        {
            if (tokens == null) throw new ArgumentNullException(nameof(tokens));
            for (int i = 0; i < tokens.Count; i++)
            {
                LineRefusal? one = Refusal(tokens[i]);
                if (one.HasValue) return one;
                if (i > 0 && string.CompareOrdinal(tokens[i - 1], tokens[i]) >= 0)
                    return new LineRefusal
                    {
                        Code = LineWrites.TokensNotInOrder,
                        Words = "The game tokens are not in the ordinal order, each once: \"" + tokens[i - 1] + "\" is followed by \"" + tokens[i] + "\".",
                    };
            }
            return null;
        }
    }
}
