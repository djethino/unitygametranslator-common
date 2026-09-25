using System;
using System.Linq;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The words of texts-seen.json and the line a person reads for them. The expected lines are
    /// written out here rather than composed from the table, so a label changed in the socle has
    /// to be changed here on purpose.
    /// </summary>
    internal static class TextSystemsChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            foreach (TextSystem s in Enum.GetValues(typeof(TextSystem)))
            {
                check(TextSystems.TryParse(TextSystems.Word(s), out var back) && back == s,
                    $"'{TextSystems.Word(s)}' reads back as {s}",
                    "a word the mod writes and the Manager cannot read back is a system that vanishes on the way");
            }

            check(!TextSystems.TryParse("TMP", out _),
                "words are compared exactly: 'TMP' is not 'tmp'",
                "the file is written by code, one spelling; tolerating two would let the writers drift");

            check(TextSystems.Describe(new[] { TextSystem.InputTmp, TextSystem.Tmp }) == "TextMesh Pro · input fields: TextMesh Pro",
                "texts first, input fields after, whatever the order given",
                "the line has to read the same for every game to be compared at a glance");

            check(TextSystems.Describe(new[] { TextSystem.UiText, TextSystem.Tmp }, new[] { "SuperTextMesh" }, new[] { "rich-3d" })
                  == "TextMesh Pro, UI Text, SuperTextMesh, rich-3d",
                "known systems in the socle's order, then other components, then words a newer mod wrote",
                "an unknown word is named, never dropped: the reader is older than the writer, not wiser");

            check(TextSystems.Describe(new[] { TextSystem.InputUiText }) == "input fields: UI Text",
                "only input fields: the line starts with them, no dangling separator",
                "a separator with nothing before it reads as a missing value");

            check(TextSystems.Describe(Enumerable.Empty<TextSystem>()) == "",
                "nothing met: an empty line, for the screen to word",
                "\"nothing yet\" and \"never ran\" are different facts, told by the caller");

            check(TextSystems.FromTypeName("UILabel") == TextSystem.Ngui && TextSystems.FromTypeName("tk2dTextMesh") == TextSystem.Tk2d
                  && TextSystems.FromTypeName("SuperTextMesh") == null,
                "generic components known by name map to their system; any other stays itself",
                "NGUI reaches the mod through its generic road, and must still be counted as NGUI");
        }
    }
}
