using System.Collections.Generic;
using System.Linq;

namespace UnityGameTranslator.Common
{
    public enum AnswerFaultKind
    {
        /// <summary>A placeholder lost, doubled or invented (<see cref="AnswerFault.Placeholder"/> says which).</summary>
        Placeholder,

        /// <summary>A bracket of <c>{ } [ ]</c> that appears a number of times other than the source's.</summary>
        Brackets,

        /// <summary>The game's own labels (« [攻] ») the answer did not keep the brackets of: said once, after the counts.</summary>
        Labels,

        /// <summary>Letters the answer put between two values the source separates with none.</summary>
        LettersAdded,

        /// <summary>Two values the source separates with a word, which the answer leaves side by side.</summary>
        ValuesRunTogether,

        /// <summary>A closing tag the answer puts before the opening one it closes.</summary>
        TagOutOfOrder,

        /// <summary>A pair of tags that styled words in the source and styles none in the answer.</summary>
        TagEmptied,

        /// <summary>A tag in the answer that was never sent.</summary>
        TagInvented,
    }

    /// <summary>
    /// One reason an answer of the AI was refused, as a FACT. What the model is told on the next attempt is
    /// <see cref="AnswerFaults.ModelSentence"/>, derived from it letter for letter from the sentences the checks used to build
    /// inline (the prompts measured on the AI do not move); what a person reads on the Failures tab is the product's wording
    /// (spec/wording.json, <c>answer.*</c> and <c>placeholder.*</c>), said from the same fact.
    ///
    /// ⚠ The answer judged is the one the model SAW: a tag is lifted into <c>[!t*N]</c> before a text is sent, so the
    /// model's sentences name those tokens. A person never sees them: the fact keeps the game's own tag beside the token
    /// (<see cref="OpeningTag"/>, <see cref="ClosingTag"/>), and the wording of a person speaks of that.
    /// </summary>
    public sealed class AnswerFault
    {
        public AnswerFaultKind Kind;

        /// <summary>The placeholder fault, for <see cref="AnswerFaultKind.Placeholder"/>.</summary>
        public PlaceholderFault Placeholder;

        /// <summary>The bracket, for <see cref="AnswerFaultKind.Brackets"/>, with how many of it the answer has and the source had.</summary>
        public char Bracket;

        public int Found;
        public int Expected;

        /// <summary>The labels of the source, brackets included, for <see cref="AnswerFaultKind.Labels"/>.</summary>
        public List<string> Labels = new List<string>();

        /// <summary>The two values, for <see cref="AnswerFaultKind.LettersAdded"/> and <see cref="AnswerFaultKind.ValuesRunTogether"/>.</summary>
        public string First = "";

        public string Second = "";

        /// <summary>What the source has between the two values (white space trimmed for <see cref="AnswerFaultKind.ValuesRunTogether"/>).</summary>
        public string Joint = "";

        /// <summary>What the answer has between them.</summary>
        public string Answered = "";

        /// <summary>The tokens of a pair, for <see cref="AnswerFaultKind.TagOutOfOrder"/> and <see cref="AnswerFaultKind.TagEmptied"/>.</summary>
        public string Opening = "";

        public string Closing = "";

        /// <summary>The game's own tags the tokens stand for (empty when the caller did not know them).</summary>
        public string OpeningTag = "";

        public string ClosingTag = "";

        /// <summary>The tag as the answer wrote it, for <see cref="AnswerFaultKind.TagInvented"/>.</summary>
        public string Tag = "";
    }

    /// <summary>
    /// The refusals of an answer of the AI, as facts: what <see cref="Placeholders.Accepts"/> and the markup checks used to say in
    /// sentences, said once as data. One judge for the model (the sentences it is sent back) and for a person reading why a
    /// proposal was refused (a sentence of the product's own).
    ///
    /// ⚠ This is the list of the MODEL, never the list of a person's edit (<see cref="Placeholders.EditFaults"/>): a person is not
    /// held to the count of brackets over the whole text, nor to the letters between two values, and the facts do not unify.
    /// What the two lists share is the fact itself, said by one wording when it is the same fact.
    /// </summary>
    public static class AnswerFaults
    {
        /// <summary>
        /// What is wrong with an answer to a text, in the order the model has always been told: placeholders, counts of brackets,
        /// the labels, letters added, values run together, then the markup (order, emptied, invented). The text is the one the
        /// line keeps (its markup is lifted here as it is before it is sent). Empty when the answer would be kept.
        /// </summary>
        public static List<AnswerFault> Of(string source, string answer)
        {
            PreparedText prepared = Backends.Prepare(source ?? "");
            return Of(prepared, answer ?? "", Placeholders.FrozenSequences(prepared.ToSend));
        }

        /// <summary>The same judgement for a text already prepared (the line loop's own).</summary>
        internal static List<AnswerFault> Of(PreparedText prepared, string answer, List<string> frozen)
        {
            List<AnswerFault> faults = Placeholders.ModelFaults(prepared.ToSend, answer, frozen);
            faults.AddRange(Markup.OutOfOrderFaults(answer, prepared.Tags));
            faults.AddRange(Markup.EmptiedFaults(prepared.ToSend, answer, prepared.Tags));
            faults.AddRange(Markup.InventedFaults(answer));
            return faults;
        }

        /// <summary>What the model is told of a fault: the sentence the checks have always built, word for word.</summary>
        public static string ModelSentence(AnswerFault fault)
        {
            switch (fault.Kind)
            {
                case AnswerFaultKind.Placeholder:
                    return Placeholders.ModelSentence(fault.Placeholder);
                case AnswerFaultKind.Brackets:
                    return $"character '{fault.Bracket}' appears {fault.Found} time(s) instead of {fault.Expected}";
                case AnswerFaultKind.Labels:
                    return "keep the brackets of " + string.Join(", ", fault.Labels.Select(label => $"\"{label}\""))
                           + " and translate the words inside them";
                case AnswerFaultKind.LettersAdded:
                    return $"between {fault.First} and {fault.Second} the source has \"{fault.Joint}\" and no letter: \"{fault.Answered}\" adds some";
                case AnswerFaultKind.ValuesRunTogether:
                    return $"between {fault.First} and {fault.Second} the source has the word \"{fault.Joint}\": \"{fault.Answered}\" leaves the two values side by side — keep a word between them";
                case AnswerFaultKind.TagOutOfOrder:
                    return $"{fault.Closing} closes {fault.Opening}, so it must come after it";
                case AnswerFaultKind.TagEmptied:
                    return $"{fault.Opening} and {fault.Closing} surround words in the source: put the translation of those words between them";
                default:
                    return $"{fault.Tag} is not in the source: write no tag of your own, only the placeholders";
            }
        }

        /// <summary>The sentences of a list of faults, in order — what <see cref="Placeholders.Accepts"/> hands back as its errors.</summary>
        internal static List<string> ModelSentences(IEnumerable<AnswerFault> faults) => faults.Select(ModelSentence).ToList();
    }
}
