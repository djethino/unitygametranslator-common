using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The two pure rules of the planner: what an image setting may carry, and how settings merge into
    /// a translation. The rest of the plan reads files and is replayed on real ones by the Manager's
    /// GameAssetsChecks, which drive it end to end.
    /// </summary>
    internal static class AssetPlannerChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            var fields = new Dictionary<string, object?>
            {
                ["sprite_name"] = "标题",
                ["path"] = "Canvas/Title/Image",
                ["original_width"] = 926L,
                ["pivot_x"] = 0.5,
                ["pixels_per_unit"] = "100",
                ["original_file"] = "标题.png",
                ["payload"] = "anything",
            };

            var read = ImageDefinition.Read(name => fields.TryGetValue(name, out var value) ? value : null)!;
            var written = read.Fields().ToDictionary(p => p.Key, p => p.Value);

            check(written.Keys.SequenceEqual(new[] { "sprite_name", "path", "original_width", "pivot_x", "file" })
                  && (double)written["original_width"] == 926 && (string)written["file"] == "标题.png",
                "an image setting keeps the fields the mod reads, of the right kind, the file under its current name",
                "a field nobody reads, or a number written as text, never reaches a translation that travels");

            check(ImageDefinition.Read(name => name == "path" ? "x" : null) == null,
                "a setting with no sprite is not a setting",
                "the sprite is its identity in the section");

            check(read.SameAs(ImageDefinition.Read(name => name == "sprite_name" ? "标题" : fields.TryGetValue(name, out var v) ? v : null)!)
                  && !read.SameAs(ImageDefinition.Read(name => name == "pivot_x" ? 0.25 : fields.TryGetValue(name, out var v) ? v : null)!),
                "two settings are the same when every field the mod reads is",
                "a pack installed twice must read as already there, a changed pivot as a replacement");

            ImageDefinition Def(string sprite) => ImageDefinition.Read(n => n == "sprite_name" ? sprite : n == "file" ? sprite + ".png" : null)!;

            // The file holds Title, Other, TITLE (stale, the one the mod reads) — then Logo arrives too.
            var edits = AssetPlanner.Merge(new[] { "Title", "Other", "TITLE", null }, new[] { Def("title"), Def("Logo") });
            check(edits.Count == 2
                  && edits[0].ReplaceAt == 0 && edits[0].RemoveAt.SequenceEqual(new[] { 2 })
                  && edits[1].ReplaceAt == null,
                "a setting replaces the first entry for its sprite and removes the others; a new sprite is appended",
                "a stale duplicate left after ours would override it in the game (the mod keeps the last)");

            // Applied in order to a list, the result holds one entry per sprite.
            var section = new List<string?> { "Title", "Other", "TITLE", null };
            foreach (var edit in edits)
            {
                foreach (var i in edit.RemoveAt) section.RemoveAt(i);
                if (edit.ReplaceAt is int at) section[at] = edit.Definition.Sprite;
                else section.Add(edit.Definition.Sprite);
            }

            check(section.SequenceEqual(new[] { "title", "Other", null, "Logo" }),
                "applied in order, the section holds one entry per sprite and keeps every other entry where it was",
                "entries the merge does not concern are left exactly as they are");
        }
    }
}
