using System;
using System.Collections.Generic;
using UnityGameTranslator.Common;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// Runs the shared rules against the answers they are supposed to give.
    ///
    /// This library exists so that two programs cannot disagree. That guarantee is worth exactly
    /// as much as the rules being right in the first place — and the version comparison shipped
    /// wrong for a long time in both copies, in a way nobody could see: a "-beta" suffix sorted
    /// correctly while a "-beta.1" sorted backwards, and the release process happens to use the
    /// second form. Whoever installed a beta was told, forever, that they were up to date.
    ///
    /// So each rule that moves in here brings its cases with it. Run with `dotnet run` from this
    /// folder; the exit code is what a script should read.
    ///
    /// ⚠ The cases are moving OUT of C# and into corpus/rules/*.json (2026-09-08), so that a
    /// second implementation in another language can be held to the same answers. This program
    /// is the C# executor of that corpus (see Corpus/) for the rules already transcribed, and
    /// still carries the others' cases in *Checks.cs until they move.
    /// </summary>
    internal static class Program
    {
        private static int _failures;

        private static int Main()
        {
            Corpus.CorpusRunner.Run(Check);
            StoredSecrets();
            WhatSitsBesideATranslation();
            EditingSides();
            HasItBeenPublished();
            WhatOneButtonDoes();
            WhoMayRate();
            WhereSomebodyStands();
            HowMuchStripFits();
            BrowserSessions();
            LanguageLookup();
            HotkeySpelling();
            QualityMeasures();
            PlaceholderRules();
            PromptWording();
            EndpointAddresses();
            WhyItNeverArrived();
            ProviderNegotiation();
            ProductColours();

            Console.WriteLine();
            if (_failures == 0)
            {
                Console.WriteLine("All checks passed.");
                return 0;
            }

            Console.WriteLine($"{_failures} check(s) FAILED.");
            return 1;
        }

        /// <summary>The files a translation keeps beside itself, and the section two products read by name.</summary>
        private static void WhatSitsBesideATranslation()
        {
            Section("Translation files");
            TranslationFilesChecks.Run(Check);
        }

        /// <summary>Which copy an editor is about to change, and what is reachable from where.</summary>
        private static void EditingSides()
        {
            Section("Edit scope");
            EditScopeChecks.Run(Check);
        }

        /// <summary>Whether a translation has ever left this machine.</summary>
        private static void HasItBeenPublished()
        {
            Section("Publication");
            PublicationChecks.Run(Check);
        }

        /// <summary>What sending a file becomes, the word for it, and where the act is taken.</summary>
        private static void WhatOneButtonDoes()
        {
            Section("Uploads");
            UploadsChecks.Run(Check);

            Section("The language of a translation");
            TranslationLanguagesChecks.Run(Check);

            Section("Which game a publication names");
            GameCandidatesChecks.Run(Check);
        }

        /// <summary>Who may rate a translation, and why the arrows are sometimes absent.</summary>
        private static void WhoMayRate()
        {
            Section("Voting");
            VotingChecks.Run(Check);
        }

        /// <summary>The durations and the words of a browser edit session.</summary>
        private static void BrowserSessions()
        {
            Section("Edit sessions");
            EditSessionsChecks.Run(Check);
        }

        /// <summary>Which form of the scope strip fits beside a title.</summary>
        private static void HowMuchStripFits()
        {
            Section("Scope strip");
            ScopeStripChecks.Run(Check);
        }

        /// <summary>The four independent questions a screen answers, and who may write.</summary>
        private static void WhereSomebodyStands()
        {
            Section("Standing");
            StandingChecks.Run(Check);
        }

        /// <summary>The palette, against what the website actually renders.</summary>
        private static void ProductColours()
        {
            Section("Theme");
            ThemeChecks.Run(Check);
        }

        /// <summary>What a provider will accept, learned by being refused.</summary>
        private static void ProviderNegotiation()
        {
            Section("Negotiation");
            NegotiationChecks.Run(Check);
        }

        /// <summary>Why a request never arrived, in words that point at the right culprit.</summary>
        private static void WhyItNeverArrived()
        {
            Section("Connectivity");
            ConnectivityChecks.Run(Check);
        }

        /// <summary>Where a request really goes, from whatever address somebody pasted.</summary>
        private static void EndpointAddresses()
        {
            Section("Endpoints");
            EndpointsChecks.Run(Check);
        }

        /// <summary>What a model is actually told, and how a text is sorted before being asked for.</summary>
        private static void PromptWording()
        {
            Section("Prompts");
            PromptsChecks.Run(Check);
        }

        /// <summary>What a game will accept back from a model, and what it says when it will not.</summary>
        private static void PlaceholderRules()
        {
            Section("Placeholder rules");
            PlaceholdersChecks.Run(Check);
        }

        /// <summary>What a player is told about a file, checked against the website's rules.</summary>
        private static void QualityMeasures()
        {
            Section("Quality measures");
            QualityChecks.Run(Check);
        }

        /// <summary>How a keyboard shortcut is spelled — silent when wrong, hence the cases.</summary>
        private static void HotkeySpelling()
        {
            Section("Hotkeys");
            HotkeysChecks.Run(Check);
        }

        /// <summary>Codes, names, and the two inventories that must not be collapsed.</summary>
        private static void LanguageLookup()
        {
            Section("Languages");
            LanguagesChecks.Run(Check);

            Section("Flags");
            FlagChecks.Run(Check);

            Section("Origins");
            OriginsChecks.Run(Check);

            Section("Composition");
            CompositionChecks.Run(Check);

            Section("Contributions");
            ContributionsChecks.Run(Check);

            Section("People");
            PeopleChecks.Run(Check);

            Section("Backups");
            BackupsChecks.Run(Check);

            Section("Mod interface");
            ModUiChecks.Run(Check);

            Section("Game names");
            GameNamesChecks.Run(Check);
        }

        /// <summary>The stored-secret format, checked against its own specification.</summary>
        private static void StoredSecrets()
        {
            Section("Stored secrets");
            SecretsChecks.Run(Check);
        }

        private static void Check(bool passed, string what, string why)
        {
            if (!passed) _failures++;
            Console.WriteLine($"  {(passed ? "ok  " : "FAIL")}  {what,-52}  {why}");
        }

        private static void Section(string title)
        {
            Console.WriteLine();
            Console.WriteLine(title);
            Console.WriteLine(new string('-', title.Length));
        }
    }
}
