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
        }
    }
}
