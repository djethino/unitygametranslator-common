using System;
using System.Linq;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>The origin a font reference names is the one that serves it (user, 2026-09-27).</summary>
    internal static class FontReferencesChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            check(FontReferences.Serving("[Game] Arial", gameHas: true, customHas: true, systemHas: true) == FontSource.Game
                  && FontReferences.Serving("[Game] Arial", gameHas: false, customHas: true, systemHas: true) == null,
                "a game font is served by the game, never by a file of the same name",
                "\"[Game] Arial\" used to be served fonts/Arial.ttf whenever one was there");

            check(FontReferences.Serving("[Custom] Noto", gameHas: true, customHas: true, systemHas: true) == FontSource.Custom
                  && FontReferences.Serving("[Custom] Noto", gameHas: true, customHas: false, systemHas: true) == null,
                "a custom font is served from fonts/, and only from there",
                "the origin chosen in the Fonts tab is the origin used");

            check(FontReferences.Serving("Candara", gameHas: false, customHas: true, systemHas: true) == FontSource.System
                  && FontReferences.Serving("Candara", gameHas: false, customHas: true, systemHas: false) == FontSource.Custom,
                "an installed font answers first; a copy of it in fonts/ only when it is not installed",
                "the author's machine keeps its own font; a player without it gets the copy an asset pack brought");

            check(FontReferences.Serving("LiberationSans SDF", gameHas: true, customHas: false, systemHas: false) == FontSource.Game,
                "a bare name that is only a game font still finds it",
                "translations written before the origin marks named game fonts bare");

            check(FontReferences.Name("[Game] Arial") == "Arial" && FontReferences.Name("[Custom] Noto") == "Noto"
                  && FontReferences.Name("Candara") == "Candara" && FontReferences.Name(null) == "",
                "the name is the reference without its mark", "what every source is looked up by");
        }
    }
}
