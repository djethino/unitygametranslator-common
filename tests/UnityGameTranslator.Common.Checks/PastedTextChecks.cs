using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// What a box of one line keeps of a pasted text — each case a copy somebody really makes.
    ///
    /// 🔴 The first case is the reported one (2026-10-05): a title copied with the line above it
    /// pasted nothing at all.
    /// </summary>
    internal static class PastedTextChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            check(PastedText.OneLine("\n侠影录") == "侠影录",
                "a break BEFORE the title keeps the title",
                "🔴 the reported defect: only what preceded the first break was kept, so nothing");

            check(PastedText.OneLine("侠影录\r\n") == "侠影录",
                "a break after it is dropped",
                "the usual shape of a line copied whole");

            check(PastedText.OneLine("Legacy of\nShadows") == "Legacy of Shadows",
                "a title broken across two lines is joined by a space",
                "the words are what was meant; the break came with the copy");

            check(PastedText.OneLine("  a  \n\n\n  b  ") == "a b",
                "empty lines and the blanks around each line go",
                "one space between the pieces, none at the ends");

            check(PastedText.OneLine("Doom\u2028Eternal") == "Doom Eternal"
                  && PastedText.OneLine("x\u0085y") == "x y",
                "the line breaks of every text count, not only \\n",
                "UAX #14's mandatory breaks: a copy from a document can carry U+2028 or NEL");

            check(PastedText.OneLine("Baldur's Gate 3") == "Baldur's Gate 3"
                  && PastedText.OneLine("a\tb") == "a\tb",
                "a text of one line is left exactly as it is",
                "nothing inside a line is rewritten — a tab or a double space is the person's");

            check(PastedText.OneLine("\n \r\n") == "" && PastedText.OneLine(null) == "",
                "breaks alone give nothing",
                "an empty answer, never a lone space");

            check(PastedText.NeedsOneLine("\n侠影录") && !PastedText.NeedsOneLine("侠影录"),
                "it says when a paste would lose something",
                "so a box only steps in when the copy carries a break");
        }
    }
}
