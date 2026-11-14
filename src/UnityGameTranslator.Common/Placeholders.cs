using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityGameTranslator.Common
{
    /// <summary>A label of the game's own in a text — « [攻] », « {Hard} » —: where it stands and what it says.</summary>
    public struct PlaceholderLabel
    {
        /// <summary>The index of its opening bracket, in UTF-16 units of the text.</summary>
        public int Start;

        /// <summary>Its length with both brackets.</summary>
        public int Length;

        /// <summary>The label as written, brackets included.</summary>
        public string Text;

        /// <summary>The words between the brackets.</summary>
        public string Inside;
    }

    public enum PlaceholderFaultKind
    {
        /// <summary>A frozen sequence — a placeholder with the delimiters the game wrapped round it — is missing or altered (<see cref="PlaceholderFault.Sequence"/>).</summary>
        Sequence,

        /// <summary>A placeholder appears a number of times other than the source's (<see cref="PlaceholderFault.Token"/>, found, expected).</summary>
        Count,

        /// <summary>A placeholder the source never had (<see cref="PlaceholderFault.Token"/>).</summary>
        Invented,
    }

    /// <summary>
    /// One thing wrong with a text against its source, as a FACT a screen words in its own language: what a person is told is the product's
    /// wording (spec/wording.json, <c>placeholder.*</c>), what a model is told is <see cref="Placeholders.ModelSentence"/>.
    /// </summary>
    public struct PlaceholderFault
    {
        public PlaceholderFaultKind Kind;

        /// <summary>The sequence, for <see cref="PlaceholderFaultKind.Sequence"/>.</summary>
        public string? Sequence;

        /// <summary>The placeholder, for <see cref="PlaceholderFaultKind.Count"/> and <see cref="PlaceholderFaultKind.Invented"/>.</summary>
        public string? Token;

        public int Found;
        public int Expected;
    }

    /// <summary>
    /// The rules a machine translation has to obey to be usable in a game, and what to say to a
    /// model that broke them.
    ///
    /// A game's text carries technical placeholders — [!v*0] for a value, [!t*0] for a tag,
    /// [!STR*0] for a nested string, [!nl] for a line break. They are not decoration: the game
    /// substitutes them at runtime, so one lost, duplicated or invented token is a line that
    /// breaks, and no amount of good prose makes up for it.
    ///
    /// ⚠ This is the mod's own logic, and it was reproduced in the manager to measure how well
    /// models comply with it. A reproduction is exactly what must not exist here: what the tests
    /// score has to be what a game actually enforces, down to the wording sent back to the model
    /// on a second attempt, or the score describes the test bench rather than the model.
    ///
    /// The two copies were compared character by character before being merged — same regex, same
    /// expansion over brackets, same checks in the same order, same sentences. What follows is
    /// that agreement, written once.
    /// </summary>
    public static class Placeholders
    {
        /// <summary>
        /// How many times a text is sent before it is left untranslated.
        ///
        /// Three, and the shape matters as much as the number: a first plain attempt, a second
        /// carrying <see cref="Correction"/> as targeted feedback, a third starting fresh with
        /// <see cref="MandatorySequences"/> added to the instructions.
        /// </summary>
        public const int MaxAttempts = 3;

        /// <summary>
        /// The grammar of a token, written once: [!v*N], [!t*N], [!STR*N], [!nl] — and the three markers of
        /// the format 2, whose patterns are LineMarkers' own: [!g*N] (a link), [!L*name…] (a closed slot),
        /// [!T*N] (an open slot).
        ///
        /// 🔴 **A marker of the format 2 never goes to a model and never comes back from one without
        /// a rule.** The model is shown the sentence in clear (a link is its words, a template is
        /// asked as the text the game shows); the program, not the model, finds the links again in what
        /// comes back. So a marker in a reply is a token the source never had — « invented », refused
        /// and sent back with the usual correction — and one in a text a person types is refused the
        /// same way. A text that really holds one in its SOURCE keeps it, like any other token.
        /// </summary>
        private const string TokenGrammar = @"(?:\[!(?:v\*[0-9]+|t\*[0-9]+|STR\*[0-9]+|nl)\]|"
            + LineMarkers.LinkPattern + "|" + LineMarkers.ClosedSlotPattern + "|" + LineMarkers.OpenSlotPattern + ")";

        /// <summary>Every frozen token: [!v*N], [!t*N], [!STR*N], [!nl].</summary>
        private static readonly Regex TokenPattern = new Regex(TokenGrammar, RegexOptions.Compiled);

        /// <summary>The same grammar, matched only where a scan stands.</summary>
        private static readonly Regex TokenHere = new Regex(@"\G" + TokenGrammar, RegexOptions.Compiled);

        /// <summary>
        /// The length of the token starting at <paramref name="index"/>, or 0 when none does — for
        /// a scanner walking a text character by character (the right-to-left composer and field).
        ///
        /// ⚠ They used to accept anything from "[!" to the next "]" within 32 characters, a bound
        /// nobody derived: "[!note]" in a game's text was protected as a placeholder. Only what
        /// this grammar names is one — the rule <see cref="Tokens"/> already applies.
        /// </summary>
        public static int LengthAt(string text, int index)
        {
            if (string.IsNullOrEmpty(text) || index < 0 || index >= text.Length || text[index] != '[') return 0;
            Match match = TokenHere.Match(text, index);
            return match.Success ? match.Length : 0;
        }

        /// <summary>
        /// Each placeholder together with the delimiters the game wrapped around it, e.g. "({[!v*0]})".
        ///
        /// Expanding left over opening characters only and right over closing ones cannot swallow a
        /// neighbour, since every token itself starts with '[' and ends with ']'. These sequences
        /// have to come back verbatim: an answer that keeps the token but drops the bracket the
        /// game put around it still breaks the line.
        /// </summary>
        public static List<string> FrozenSequences(string source)
        {
            var sequences = new List<string>();
            if (string.IsNullOrEmpty(source)) return sequences;

            foreach (Match match in TokenPattern.Matches(source))
            {
                int start = match.Index;
                int end = match.Index + match.Length; // exclusive

                // Outward one PAIR at a time: a bracket the game wrapped around the token sits on
                // both sides of it — "({[!v*0]})". A bracket on one side only belongs to the
                // sentence, not to the token: "boltcutters ([!v*0] off)" wraps a phrase, and the
                // phrase moves with the language — « coupe-boulons (Promo [!v*0]) » is right.
                // Freezing the single side refused every such translation (seen 2026-09-12).
                while (start > 0 && end < source.Length && Wraps(source[start - 1], source[end]))
                {
                    start--;
                    end++;
                }

                string sequence = source.Substring(start, end - start);
                if (!sequences.Contains(sequence)) sequences.Add(sequence);
            }

            return sequences;
        }

        /// <summary>A pair of square brackets or braces with no bracket inside.</summary>
        private static readonly Regex BracketedPattern = new Regex(@"\[[^\[\]{}]*\]|\{[^\[\]{}]*\}", RegexOptions.Compiled);

        /// <summary>
        /// The game's own bracketed labels in a text — "[攻]", "{Hard}" — once each, in order:
        /// a pair of the brackets <see cref="Accepts"/> counts, holding a word, that is not a
        /// placeholder.
        ///
        /// ⚠ **Not frozen**: the words inside are the game's text and get translated ("[攻]" →
        /// "[Att]"). Only the brackets have to come back, which is what the count checks; this
        /// list exists to NAME them — to the model, before and after it answers.
        /// </summary>
        public static List<string> Labels(string text)
        {
            var labels = new List<string>();
            foreach (PlaceholderLabel label in LabelsOf(text))
                if (!labels.Contains(label.Text)) labels.Add(label.Text);
            return labels;
        }

        /// <summary>
        /// EVERY occurrence of a label — where it stands and what it says — the same pairs <see cref="Labels"/> names once each: a pair of the
        /// brackets <see cref="Accepts"/> counts (<c>[ ]</c> and <c>{ }</c>) with no bracket of those inside, holding a letter, that is not a
        /// placeholder. For a caller that must put something in the label's place and bring it back (a name the game fills: the Manager's
        /// repair of a line the model would not keep it in), so that none keeps a copy of the pattern.
        ///
        /// ⚠ What this pattern does NOT exclude, and a caller may filter on <see cref="PlaceholderLabel.Inside"/>: a line break, or a
        /// <c>&lt;</c> / <c>&gt;</c>, inside the pair. Only <c>[</c> <c>]</c> <c>{</c> <c>}</c> are brackets here — see spec/placeholders.md for
        /// where that list comes from.
        /// </summary>
        public static List<PlaceholderLabel> LabelsOf(string text)
        {
            var labels = new List<PlaceholderLabel>();
            if (string.IsNullOrEmpty(text)) return labels;

            foreach (Match match in BracketedPattern.Matches(text))
            {
                if (TokenPattern.IsMatch(match.Value)) continue;
                if (!HasLetter(match.Value)) continue;
                labels.Add(new PlaceholderLabel { Start = match.Index, Length = match.Length, Text = match.Value, Inside = match.Value.Substring(1, match.Length - 2) });
            }

            return labels;
        }

        /// <summary>An opening bracket and the closing one that answers it.</summary>
        private static bool Wraps(char before, char after) =>
            (before == '(' && after == ')') || (before == '{' && after == '}') || (before == '[' && after == ']');

        /// <summary>
        /// Whether a game would accept this answer.
        ///
        /// ⚠ Containment is not enough — "[{[!v*0]}]" contains "[!v*0]" and is still wrong. So:
        /// the frozen sequences verbatim, then the tokens as the same multiset (nothing missing,
        /// duplicated or invented), then the brackets unchanged, which catches the answer that
        /// wrapped a placeholder in a pair of its own.
        ///
        /// The error lines are not for a log: they are handed to the model on the next attempt,
        /// which is why they name the token and the counts rather than saying "invalid".
        /// </summary>
        public static bool Accepts(string source, string translation, List<string> frozen, out List<string> errors)
        {
            KeepsEveryPlaceholder(source, translation, frozen, out errors);

            // ⚠ The one check a PERSON is not held to — see AcceptsEdit. It exists for a model that
            // wraps a placeholder in a pair of its own, which the frozen sequences catch for the
            // placeholders themselves; over the whole text it also refuses a bracket somebody added
            // on purpose, and a model cannot be asked what it meant.
            bool bracketsMoved = false;
            foreach (char bracket in new[] { '{', '}', '[', ']' })
            {
                int expected = source.Count(c => c == bracket);
                int found = translation.Count(c => c == bracket);
                if (expected != found)
                {
                    errors.Add($"character '{bracket}' appears {found} time(s) instead of {expected}");
                    bracketsMoved = true;
                }
            }

            // 🔴 A count says THAT a bracket went missing, never WHICH. When the source carries a
            // label of the game's own — "<color=orange>[攻]</color>" — a model that turned it into
            // prose was told "'[' appears 3 times instead of 4" and nothing else, and failed the
            // same way three times. Naming the label is what a second attempt can act on.
            List<string> labels = Labels(source);
            if (bracketsMoved && labels.Count > 0)
                errors.Add("keep the brackets of " + string.Join(", ", labels.Select(label => $"\"{label}\""))
                           + " and translate the words inside them");

            // ⚠ Also a model-only check, for the same reason: a person who adds a letter meant it.
            foreach (string error in LettersAddedBetween(source, translation)) errors.Add(error);
            foreach (string error in ValuesRunTogether(source, translation)) errors.Add(error);

            return errors.Count == 0;
        }

        /// <summary>
        /// Two placeholders the source separates with a WORD, which the answer leaves side by side with nothing but
        /// spaces between them: the values can no longer be told apart. The mirror of <see cref="LettersAddedBetween"/>.
        ///
        /// 🔴 **Seen in a real file**: "[!v*0]年[!v*1]月[!v*2]日" (year, month, day) came back as "[!v*2] [!v*1] [!v*0]",
        /// and the game showed a save's date as "3 1 1" (2026-10-19).
        ///
        /// ⚠ **Narrow on purpose**: only an answer with no letter left at all (a word moved elsewhere still names the
        /// values); only two placeholders that appear once on each side and follow each other in the
        /// answer (in either order); only when the source has a letter between them; only when the answer has NOTHING
        /// between them but white space and the signs the source already had there (its braces around a value) — a
        /// sign of the answer's own ("[!v*0]x[!v*1]" → "[!v*0]×[!v*1]", a "/" or a ":") still tells the values apart
        /// and is the language's choice.
        /// </summary>
        private static List<string> ValuesRunTogether(string source, string translation)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(translation)) return errors;

            List<Match> inSource = TokenPattern.Matches(source).Cast<Match>().ToList();
            if (inSource.Count < 2) return errors;
            // only an answer that kept no word at all: a word that MOVED ("第[!v*0]章 [!v*1]" → "Chapitre [!v*0] [!v*1]",
            // the second value a title) still says what the values are — refused, a right translation was lost (2026-10-19)
            if (HasLetter(TokenPattern.Replace(translation, " "))) return errors;
            List<Match> inAnswer = TokenPattern.Matches(translation).Cast<Match>().ToList();
            Dictionary<string, int> sourceTally = Tally(source);
            Dictionary<string, int> answerTally = Tally(translation);

            for (int i = 0; i + 1 < inSource.Count; i++)
            {
                string first = inSource[i].Value, second = inSource[i + 1].Value;
                if (!OnceEach(first, sourceTally, answerTally) || !OnceEach(second, sourceTally, answerTally)) continue;

                string joint = Between(source, inSource[i], inSource[i + 1]);
                if (!HasLetter(joint)) continue;

                int a = inAnswer.FindIndex(m => m.Value == first), b = inAnswer.FindIndex(m => m.Value == second);
                if (a < 0 || b < 0 || Math.Abs(a - b) != 1) continue;

                string answered = a < b ? Between(translation, inAnswer[a], inAnswer[b]) : Between(translation, inAnswer[b], inAnswer[a]);
                // what the answer added there, beyond white space and the signs the source already had around its word
                // ("[!v*0]年{[!v*1]}" → "[!v*0] {[!v*1]}": the brace is the source's, the word is gone)
                if (answered.All(c => KeyChars.IsSpace(c) || (!UnicodeCategories.IsLetterOrDigit(c) && joint.IndexOf(c) >= 0)))
                    errors.Add($"between {first} and {second} the source has the word \"{joint.TrimSpaces()}\": \"{answered}\" leaves the two values side by side — keep a word between them");
            }

            return errors;
        }

        /// <summary>
        /// In a source that holds no word, the letters a model slipped in between two placeholders
        /// the source separates with no letter at all.
        ///
        /// 🔴 **Seen in a real file**: "v[!v*0].[!v*1]f[!v*2]", a version number, came back as
        /// "v[!v*0].v[!v*1]f[!v*2]". Every placeholder was there once, so nothing refused it, and
        /// the game showed a version that does not exist.
        ///
        /// ⚠ **Narrow on purpose, because each wider rule refuses real translations**:
        /// · only a source with NO word — no two word characters side by side outside the
        ///   placeholders. With a word, a language puts words between values ("[!v*0]-[!v*1]
        ///   damage" → « de [!v*0] à [!v*1] dégâts »). One ideograph is a word, so a CJK source with
        ///   single-character words falls under this; that is why the check stays this narrow;
        /// · only BETWEEN two placeholders, never at the ends: a unit beside a value is translated
        ///   ("[!v*0]m" → "[!v*0] мин" is right), and it sits at an end;
        /// · only where the source has no letter between them: a letter there ("[!v*0]x[!v*1]")
        ///   may be a word of its own, and may be translated;
        /// · only LETTERS count: punctuation and spacing change with the language ("、" → ", ");
        /// · only when both placeholders appear once on each side and follow each other in the
        ///   answer too. Reordered, the answer says something the rule has no opinion about.
        /// </summary>
        private static List<string> LettersAddedBetween(string source, string translation)
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(translation)) return errors;

            List<Match> inSource = TokenPattern.Matches(source).Cast<Match>().ToList();
            if (inSource.Count < 2 || HasWord(TokenPattern.Replace(source, "\n"))) return errors;

            List<Match> inAnswer = TokenPattern.Matches(translation).Cast<Match>().ToList();
            Dictionary<string, int> sourceTally = Tally(source);
            Dictionary<string, int> answerTally = Tally(translation);

            for (int i = 0; i + 1 < inSource.Count; i++)
            {
                string first = inSource[i].Value, second = inSource[i + 1].Value;
                if (!OnceEach(first, sourceTally, answerTally) || !OnceEach(second, sourceTally, answerTally)) continue;

                string joint = Between(source, inSource[i], inSource[i + 1]);
                if (HasLetter(joint)) continue;

                int at = inAnswer.FindIndex(m => m.Value == first);
                if (at < 0 || at + 1 >= inAnswer.Count || inAnswer[at + 1].Value != second) continue;

                string answered = Between(translation, inAnswer[at], inAnswer[at + 1]);
                if (HasLetter(answered))
                    errors.Add($"between {first} and {second} the source has \"{joint}\" and no letter: \"{answered}\" adds some");
            }

            return errors;
        }

        private static bool OnceEach(string token, Dictionary<string, int> source, Dictionary<string, int> answer)
        {
            int inSource, inAnswer;
            source.TryGetValue(token, out inSource);
            answer.TryGetValue(token, out inAnswer);
            return inSource == 1 && inAnswer == 1;
        }

        private static string Between(string text, Match before, Match after)
        {
            int start = before.Index + before.Length;
            return text.Substring(start, after.Index - start);
        }

        /// <summary>Two word characters side by side — what makes a text carry a word.</summary>
        private static bool HasWord(string text)
        {
            int run = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (IsWordCharacter(text, i)) { if (++run >= 2) return true; }
                else run = 0;
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) i++;
            }
            return false;
        }

        private static bool HasLetter(string text)
        {
            for (int i = 0; i < text.Length; i++)
                if (IsWordCharacter(text, i)) return true;
            return false;
        }

        /// <summary>
        /// A letter, or what rides on one (a vowel sign, a tone mark), judged by Unicode category
        /// and never by script. ⚠ The same categories as the mod's TextNormalization.IsWordCategory,
        /// which the socle cannot reach: the two answer the same question about a code point — and, from the same
        /// Unicode version, now the same answer.
        /// </summary>
        private static bool IsWordCharacter(string text, int i)
        {
            // The project's Unicode (UnicodeCategories), all planes — not the runtime's, which does not know the letters
            // added since it was built, nor those of a game's older Mono.
            string category = UnicodeCategories.Of(text, i);
            return category[0] == 'L' || category == "Mn" || category == "Mc" || category == "Co";
        }

        /// <summary>
        /// Whether a game would accept a translation somebody typed HERE, in an editor, with the
        /// result in front of them.
        ///
        /// 🔴 **The same rule about placeholders as a model is held to, and that is the point.** A
        /// dropped, duplicated or invented token breaks the line whoever wrote it: the game
        /// substitutes at runtime and does not care that a human was at the keyboard. This used to
        /// be a looser rule of the mod's own — missing and unknown only — so a person could
        /// duplicate a placeholder where a model would have been refused, and save it.
        ///
        /// ⚠ **One check is deliberately left out: the count of brackets over the whole text.** It
        /// guards against a model wrapping a placeholder in a pair of its own, and the frozen
        /// sequences already cover that for the placeholders. Applied to a person it would refuse
        /// an edit like "Save" → "Save [F5]", which is theirs to make and breaks nothing.
        ///
        /// ⚠ The lines it returns are read on a screen rather than sent to a model, so they name
        /// the token and the count and stop there — <see cref="Correction"/> is the other audience.
        ///
        /// ⚠ **An empty edit is accepted, whatever the source holds.** It is not a translation but
        /// a capture: the game shows its source and substitutes nothing, so there is no placeholder
        /// to keep. The mod never stores one; the site keeps the line as a capture. Held to the
        /// rule, an untranslated line opened on every marker "missing" before a word was typed.
        /// A model's empty answer is another matter and stays with <see cref="Accepts"/>.
        /// </summary>
        public static bool AcceptsEdit(string source, string edited, List<string> frozen, out List<string> errors)
        {
            if (string.IsNullOrEmpty(edited))
            {
                errors = new List<string>();
                return true;
            }

            KeepsEveryPlaceholder(source, edited, frozen, out errors);
            return errors.Count == 0;
        }

        /// <summary>
        /// What both audiences are held to: the frozen sequences verbatim, then the tokens as the
        /// same multiset — nothing missing, duplicated or invented.
        /// </summary>
        private static void KeepsEveryPlaceholder(string source, string translation,
                                                  List<string> frozen, out List<string> errors)
        {
            errors = new List<string>();
            foreach (var fault in Faults(source, translation, frozen)) errors.Add(ModelSentence(fault));
        }

        /// <summary>
        /// What is wrong with a text against its source, as facts: the frozen sequences verbatim, then the placeholders as the same multiset
        /// (nothing missing, duplicated or invented). The one computation behind <see cref="Accepts"/>, <see cref="AcceptsEdit"/> and
        /// <see cref="EditFaults"/>: only the WORDING differs between who reads it.
        /// </summary>
        internal static List<PlaceholderFault> Faults(string source, string translation, List<string> frozen)
        {
            var faults = new List<PlaceholderFault>();

            foreach (string sequence in frozen)
            {
                if (Occurrences(translation, sequence) < Occurrences(source, sequence))
                    faults.Add(new PlaceholderFault { Kind = PlaceholderFaultKind.Sequence, Sequence = sequence });
            }

            Dictionary<string, int> inSource = Tally(source);
            Dictionary<string, int> inAnswer = Tally(translation);

            foreach (var entry in inSource)
            {
                int found;
                inAnswer.TryGetValue(entry.Key, out found);
                if (found != entry.Value)
                    faults.Add(new PlaceholderFault { Kind = PlaceholderFaultKind.Count, Token = entry.Key, Found = found, Expected = entry.Value });
            }

            // ⚠ Through Invented, so this clause and the belt the mod puts after the backends are one implementation.
            foreach (string token in Invented(source, translation))
                faults.Add(new PlaceholderFault { Kind = PlaceholderFaultKind.Invented, Token = token });

            return faults;
        }

        /// <summary>
        /// A fault in the words the model is sent on its next attempt: these lines go to the model, which is why they name the token and the
        /// counts. Not what a person reads — a person reads the product's own wording (spec/wording.json). Public so that a screen showing a stored
        /// error (a failed attempt keeps the sentences it was refused with) can tell which of them are THESE and say them in its own words.
        /// </summary>
        public static string ModelSentence(PlaceholderFault fault)
        {
            switch (fault.Kind)
            {
                case PlaceholderFaultKind.Sequence: return $"the exact sequence \"{fault.Sequence}\" is missing or altered";
                case PlaceholderFaultKind.Count: return $"token {fault.Token} appears {fault.Found} time(s) instead of {fault.Expected}";
                default: return $"token {fault.Token} does not exist in the source";
            }
        }

        /// <summary>
        /// What is wrong with a translation somebody typed, as facts a screen words in its own language — the list <see cref="AcceptsEdit"/> turns into
        /// its English lines. Empty when the edit is acceptable, and for an EMPTY edit whatever the source holds: it is a capture, not a translation
        /// (the game shows its source and substitutes nothing, so there is no placeholder to keep).
        ///
        /// ⚠ The same list the site's editors compute (<c>editProblems</c>, held to the same cases): a person is told the same fact by the mod and by
        /// the site, in the product's own wording.
        /// </summary>
        public static List<PlaceholderFault> EditFaults(string? source, string? edited)
        {
            source = source ?? string.Empty;
            if (string.IsNullOrEmpty(edited)) return new List<PlaceholderFault>();
            return Faults(source, edited!, FrozenSequences(source));
        }

        /// <summary>
        /// The one repair worth making without asking again: line breaks trimmed off the very end.
        ///
        /// Models love tidying trailing newlines. Seen in the field on a credits roll of 51 tokens
        /// that came back with 50 every single time, burning three calls on every launch, forever.
        ///
        /// ⚠ Only ever APPENDS, and only when the trailing deficit explains the whole difference.
        /// Every other mismatch keeps the strict refusal: guessing at what a model meant elsewhere
        /// would put words in a game that nobody wrote. Null when it does not apply.
        /// </summary>
        public static string? RepairTrailingBreaks(string source, string translation)
        {
            const string token = "[!nl]";
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(translation)) return null;

            int deficit = Occurrences(source, token) - Occurrences(translation, token);
            if (deficit <= 0) return null;

            if (Trailing(source, token) - Trailing(translation, token) != deficit) return null;

            var repaired = new StringBuilder(translation.TrimSpacesEnd());
            for (int i = 0; i < deficit; i++) repaired.Append(token);
            return repaired.ToString();
        }

        /// <summary>
        /// The second attempt's message: what exactly was wrong, and what has to reappear.
        ///
        /// Targeted feedback corrects far better than "try again" — which is the whole reason a
        /// second attempt is worth its cost.
        /// </summary>
        public static string Correction(List<string> errors, List<string> frozen)
        {
            // ⚠ Line breaks are "\n", never the platform's: the message is frozen verbatim in the
            // corpus (rules/placeholders.json) and a port on another system must produce it byte
            // for byte. A model reads either the same.
            var message = new StringBuilder();
            message.Append("Your translation is INVALID:").Append('\n');

            foreach (string error in errors) message.Append($"- {error}").Append('\n');

            if (frozen.Count > 0)
            {
                message.Append("These exact character sequences from the source must appear unchanged in your translation:").Append('\n');
                message.Append(string.Join(", ", frozen.Select(sequence => $"\"{sequence}\""))).Append('\n');
            }

            message.Append("Reply with ONLY the corrected translation, nothing else.");
            return message.ToString();
        }

        /// <summary>
        /// The last attempt's extra instructions: a fresh start, with the sequences spelt out.
        /// </summary>
        public static string MandatorySequences(List<string> frozen)
        {
            // Same "\n" as Correction, for the same reason.
            var section = new StringBuilder();
            section.Append("=== MANDATORY EXACT SEQUENCES ===").Append('\n');
            section.Append("The text contains technical placeholders. Your output MUST contain these exact character sequences, copied character-for-character, unmodified:").Append('\n');

            foreach (string sequence in frozen) section.Append($"\"{sequence}\"").Append('\n');

            return section.ToString();
        }

        /// <summary>
        /// Every placeholder in a text, in order, repeats included.
        ///
        /// Exposed so that nothing else has to carry the pattern: it was written twice — here and
        /// in the mod, for uses that have nothing to do with retrying — and two literals of the
        /// same regular expression are two chances to add a token form to one of them only.
        /// </summary>
        public static IEnumerable<string> Tokens(string text)
        {
            if (string.IsNullOrEmpty(text)) yield break;

            foreach (Match match in TokenPattern.Matches(text))
                yield return match.Value;
        }

        /// <summary>
        /// The placeholders an answer carries that its source never had, in the order they appear,
        /// each named once. Empty when nothing was invented.
        ///
        /// 🔴 **Its own question because it is asked where <see cref="Accepts"/> is not.** That one
        /// runs only when the source HAS placeholders — no placeholder, nothing to keep, single
        /// attempt, no validation. So the one answer nobody was checking is the answer to a source
        /// with none: a small model replying with a bare "[!STR*0]", or appending one to an
        /// otherwise correct sentence. Such an entry replaces the text on screen AND is shared with
        /// everyone on upload.
        ///
        /// ⚠ It was a loop of its own in the mod, testing tokens against the raw source with a
        /// substring search. Same answers, but written twice — and this is exactly the file whose
        /// own comment says two literals of one pattern are two chances to teach only one of them a
        /// new token form.
        /// </summary>
        public static List<string> Invented(string source, string translation)
        {
            var invented = new List<string>();
            if (string.IsNullOrEmpty(translation)) return invented;

            Dictionary<string, int> inSource = Tally(source);

            foreach (string token in Tokens(translation))
            {
                if (inSource.ContainsKey(token)) continue;
                if (!invented.Contains(token)) invented.Add(token);
            }

            return invented;
        }

        /// <summary>How many times each placeholder appears.</summary>
        public static Dictionary<string, int> Tally(string text)
        {
            var tally = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (Match match in TokenPattern.Matches(text))
            {
                int count;
                tally.TryGetValue(match.Value, out count);
                tally[match.Value] = count + 1;
            }

            return tally;
        }

        /// <summary>
        /// Counted by walking, not by splitting: overlapping is impossible here and a split would
        /// allocate a whole array to answer a question about a number.
        /// </summary>
        private static int Occurrences(string text, string token)
        {
            if (string.IsNullOrEmpty(token)) return 0;

            int count = 0;
            int index = 0;

            while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }

        private static int Trailing(string text, string token)
        {
            int count = 0;
            string trimmed = text.TrimSpacesEnd();

            while (trimmed.EndsWith(token, StringComparison.Ordinal))
            {
                count++;
                trimmed = trimmed.Substring(0, trimmed.Length - token.Length).TrimSpacesEnd();
            }

            return count;
        }
    }
}
