using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// What a `.ugtpack` may put into a game folder, checked against the rule rather than the code.
    ///
    /// ⚠ The stake: a pack comes from somebody else and is unpacked next to code a game loads. An
    /// entry name let through here is a file written wherever that name points.
    /// </summary>
    internal static class AssetPacksChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            check(AssetPacks.TryEntry("fonts/NotoSans.ttf", out var fontKind, out var fontName)
                  && fontKind == AssetKind.Font && fontName == "NotoSans.ttf"
                  && AssetPacks.TryEntry("images/title_en.png", out var imageKind, out var imageName)
                  && imageKind == AssetKind.Image && imageName == "title_en.png",
                "a font and an image in their folders are read, with their bare name",
                "the two things a pack exists to carry");

            check(AssetPacks.TryEntry(@"fonts\Noto.otf", out var backKind, out _) && backKind == AssetKind.Font,
                "a backslash separator is read like a slash",
                "some Windows tools write zip entries that way, and the pack would arrive empty");

            check(!AssetPacks.TryEntry("fonts/../../BepInEx/plugins/evil.ttf", out _, out _)
                  && !AssetPacks.TryEntry("../fonts/a.ttf", out _, out _)
                  && !AssetPacks.TryEntry("fonts/sub/a.ttf", out _, out _)
                  && !AssetPacks.TryEntry("/fonts/a.ttf", out _, out _)
                  && !AssetPacks.TryEntry("fonts/C:a.ttf", out _, out _),
                "no entry reaches outside its folder",
                "a name walking up with '..' or naming a drive writes where nobody checked");

            check(!AssetPacks.TryEntry("images/font.ttf", out _, out _)
                  && !AssetPacks.TryEntry("fonts/picture.png", out _, out _)
                  && !AssetPacks.TryEntry("fonts/plugin.dll", out _, out _)
                  && !AssetPacks.TryEntry("images/readme.txt", out _, out _),
                "a file is read only in the folder of its own kind",
                "a .dll carried in a pack must never reach the game, whatever folder it claims");

            check(!AssetPacks.TryEntry(AssetPacks.ManifestName, out _, out _)
                  && !AssetPacks.TryEntry("other/a.png", out _, out _)
                  && !AssetPacks.TryEntry("", out _, out _)
                  && !AssetPacks.TryEntry(null, out _, out _),
                "the manifest and anything outside the two folders are not assets",
                "they are read, or ignored, but never written into the game");

            check(!AssetPacks.IsSafeFileName("con.ttf") && !AssetPacks.IsSafeFileName("NUL.png")
                  && !AssetPacks.IsSafeFileName("a.ttf.") && !AssetPacks.IsSafeFileName("a.ttf ")
                  && !AssetPacks.IsSafeFileName("a|b.png") && !AssetPacks.IsSafeFileName("..")
                  && AssetPacks.IsSafeFileName("Noto Sans CJK.ttc") && AssetPacks.IsSafeFileName("title (en).png"),
                "a name Windows would rewrite is refused, an ordinary one is kept",
                "a trailing dot or a device name lands somewhere other than where it was checked");

            check(AssetPacks.KindOfFile("A.TTF") == AssetKind.Font
                  && AssetPacks.KindOfFile("b.Png") == AssetKind.Image
                  && AssetPacks.KindOfFile("c.zip") == null
                  && AssetPacks.KindOfFile("d.ugtpack") == null
                  && AssetPacks.IsPack("Game assets.UGTPACK") && !AssetPacks.IsPack("x.zip"),
                "a dropped file is sorted by its extension, whatever its case",
                "a font called FONT.TTF is the same font");

            check(!AssetPacks.IsFontFile("NotoSans.gen.png") && !AssetPacks.IsFontFile("NotoSans.atlas.json"),
                "a generated atlas is not a font",
                "exporting atlases would multiply a pack's size for something rebuilt on demand");

            check(AssetPacks.FontFileStem("[Custom] NotoSans") == "NotoSans"
                  && AssetPacks.FontFileStem("NotoSans") == "NotoSans"
                  && AssetPacks.FontFileStem("[Game] LiberationSans SDF") == null
                  && AssetPacks.FontFileStem("") == null && AssetPacks.FontFileStem(null) == null,
                "a font reference names a file of fonts/, unless it names one of the game's own",
                "an export carries the fonts the translation uses, and only those");

            check(AssetPacks.IsFontFileFor("NotoSans.ttf", "NotoSans") && AssetPacks.IsFontFileFor("NotoSans.otf", "NotoSans")
                  && !AssetPacks.IsFontFileFor("notosans.ttf", "NotoSans")
                  && !AssetPacks.IsFontFileFor("NotoSans Bold.ttf", "NotoSans")
                  && !AssetPacks.IsFontFileFor("NotoSans.png", "NotoSans"),
                "a file matches a reference by its exact name, case included",
                "the mod looks the name up exactly: a looser match would export a font it never loads");

            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            var ttf = new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00, 0x10, 0x01, 0x00 };
            var otf = new byte[] { (byte)'O', (byte)'T', (byte)'T', (byte)'O', 0, 0, 0, 0 };
            var exe = new byte[] { (byte)'M', (byte)'Z', 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };

            check(AssetPacks.ContentMatches("a.png", png, 8) && AssetPacks.ContentMatches("a.ttf", ttf, 8)
                  && AssetPacks.ContentMatches("a.otf", otf, 8) && AssetPacks.ContentMatches("a.otf", ttf, 8),
                "a real picture or font is recognised by its first bytes",
                "an .otf may hold TrueType outlines: refusing it would refuse a working font");

            check(!AssetPacks.ContentMatches("a.ttf", exe, 8) && !AssetPacks.ContentMatches("a.png", exe, 8)
                  && !AssetPacks.ContentMatches("a.png", ttf, 8) && !AssetPacks.ContentMatches("a.ttf", png, 8)
                  && !AssetPacks.ContentMatches("a.dll", exe, 8) && !AssetPacks.ContentMatches("a.png", png, 3),
                "a program, or another kind of file, renamed to a font or a picture is refused",
                "the name is a claim; a renamed program would be handed to a reader that trusts it");

            check(!AssetPacks.IsFontFile("NotoSansCJK.ttc"),
                "a font collection is not accepted",
                "the mod's loader never reads one: it would be copied into the game and never offered");
        }
    }
}
