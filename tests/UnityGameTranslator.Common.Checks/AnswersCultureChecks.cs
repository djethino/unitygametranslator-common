using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The cleaning of a model's answer must not depend on the CULTURE of the machine it runs on. A regular expression
    /// reads the culture of the thread WHEN IT IS BUILT, and under a Turkish one « I » and « i » are different letters:
    /// « TRANSLATION: » was not recognised there, and the model's preamble was written into the game. Players' machines
    /// have any culture and the game's thread carries it from its first line.
    ///
    /// ⚠ So this cannot be replayed in the running process — the patterns of <see cref="Answers"/> are built once, under
    /// whatever culture the first case met. The checks run a CHILD of this program that sets the culture before it
    /// touches the library (<c>dotnet run -- answers-under-culture tr-TR</c>), and read its verdict.
    /// </summary>
    internal static class AnswersCultureChecks
    {
        public const string Verb = "answers-under-culture";

        /// <summary>The parent: one child per culture with a dotless i.</summary>
        public static void Run(Action<bool, string, string> check)
        {
            foreach (string culture in new[] { "tr-TR", "az-Latn-AZ" })
            {
                var (code, output) = Child(culture);
                check(code == 0, "the cleaning gives the same answers under " + culture, code == 0 ? "child replayed " + Cases + " answers" : output.Trim().Replace("\n", " | "));
            }
        }

        private const int Cases = 6;

        /// <summary>The child: the culture first, then the library.</summary>
        public static int RunUnder(string cultureName)
        {
            var culture = new CultureInfo(cultureName);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.CurrentCulture = culture;
            if (culture.TextInfo.ToLower("I") != "\u0131")
            {
                Console.WriteLine(cultureName + " does not lower « I » to the dotless i on this machine: the check would prove nothing");
                return 2;
            }

            int failed = 0;
            failed += One("a capital announcing prefix", "TRANSLATION: Démarrer", "Start", "Démarrer");
            failed += One("the French one in capitals", "TRADUCTION : Démarrer", "Start", "Démarrer");
            failed += One("reasoning in capitals", "<THINK>hmm</THINK>Démarrer", "Start", "Démarrer");
            failed += One("an explanation in capitals", "Démarrer\n\nNOTE: I kept the name", "Start", "Démarrer");
            failed += One("a lower-case prefix", "translation: Démarrer", "Start", "Démarrer");
            failed += One("a prefix the game's own text starts with stays", "TRANSLATION: Démarrer", "TRANSLATION: Start", "TRANSLATION: Démarrer");
            return failed == 0 ? 0 : 1;
        }

        private static int One(string what, string answer, string source, string expected)
        {
            string got = Answers.Clean(answer, source);
            if (got == expected) return 0;
            Console.WriteLine(what + ": « " + got.Replace("\n", "\n") + " » for « " + answer.Replace("\n", "\n") + " »");
            return 1;
        }

        private static (int Code, string Output) Child(string culture)
        {
            string self = Environment.ProcessPath ?? "";
            string file = System.IO.Path.GetFileNameWithoutExtension(self);
            string arguments = Verb + " " + culture;
            if (file.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                arguments = "\"" + Assembly.GetExecutingAssembly().Location + "\" " + arguments;

            var start = new ProcessStartInfo(self, arguments) { RedirectStandardOutput = true, UseShellExecute = false };
            using var child = Process.Start(start)!;
            string output = child.StandardOutput.ReadToEnd();
            child.WaitForExit();
            return (child.ExitCode, output);
        }
    }
}
