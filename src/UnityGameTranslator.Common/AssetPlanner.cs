using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UnityGameTranslator.Common
{
    /// <summary>What a file would do to the game: arrive, replace one that differs, or change nothing.</summary>
    public enum AssetChange
    {
        Add,
        Replace,
        Same,
    }

    /// <summary>
    /// One replacement picture's settings — the whole of what an entry of `_image_replacements` may
    /// carry, and nothing else: reading one IS the allow-list (<see cref="TranslationFiles.ImageNumberFields"/>).
    /// </summary>
    public sealed class ImageDefinition
    {
        public ImageDefinition(string sprite, string? path, double?[] numbers, string? file)
        {
            Sprite = sprite;
            Path = path;
            Numbers = numbers;
            File = file;
        }

        public string Sprite { get; }
        public string? Path { get; }

        /// <summary>In the order of <see cref="TranslationFiles.ImageNumberFields"/>; null where absent.</summary>
        public double?[] Numbers { get; }

        /// <summary>The picture, relative to images/.</summary>
        public string? File { get; }

        /// <summary>
        /// Reads an entry through the caller's JSON: <paramref name="field"/> returns a string, a number
        /// (any numeric type) or null. Text where text is expected, numbers where numbers are, the file
        /// under its current field or an older one — anything else is not read. Null without a sprite.
        /// </summary>
        public static ImageDefinition? Read(Func<string, object?> field)
        {
            if (!(field(TranslationFiles.ImageSpriteField) is string sprite) || sprite.Length == 0) return null;

            var numbers = new double?[TranslationFiles.ImageNumberFields.Length];
            for (var i = 0; i < numbers.Length; i++)
            {
                if (AsNumber(field(TranslationFiles.ImageNumberFields[i])) is double number
                    && !double.IsNaN(number) && !double.IsInfinity(number))
                {
                    numbers[i] = number;
                }
            }

            string? file = null;
            foreach (var name in new[] { TranslationFiles.ImageFileField }.Concat(TranslationFiles.ImageFileLegacyFields))
            {
                if (field(name) is string text && text.Trim().Length > 0) { file = text; break; }
            }

            return new ImageDefinition(sprite, field(TranslationFiles.ImagePathField) as string, numbers, file);
        }

        /// <summary>The fields to write, in the order the mod writes them — the file under its current name.</summary>
        public IEnumerable<KeyValuePair<string, object>> Fields()
        {
            yield return new KeyValuePair<string, object>(TranslationFiles.ImageSpriteField, Sprite);
            if (Path != null) yield return new KeyValuePair<string, object>(TranslationFiles.ImagePathField, Path);

            for (var i = 0; i < Numbers.Length; i++)
            {
                if (Numbers[i] is double number)
                    yield return new KeyValuePair<string, object>(TranslationFiles.ImageNumberFields[i], number);
            }

            if (File != null) yield return new KeyValuePair<string, object>(TranslationFiles.ImageFileField, File);
        }

        /// <summary>Whether two definitions say the same thing, field by field (the sprite as the mod compares it).</summary>
        public bool SameAs(ImageDefinition other) =>
            TranslationFiles.SpriteNames.Equals(Sprite, other.Sprite)
            && string.Equals(Path, other.Path, StringComparison.Ordinal)
            && string.Equals(File, other.File, StringComparison.OrdinalIgnoreCase)
            && Numbers.SequenceEqual(other.Numbers);

        internal ImageDefinition WithFile(string file) => new ImageDefinition(Sprite, Path, Numbers, file);

        private static double? AsNumber(object? value)
        {
            switch (value)
            {
                case double d: return d;
                case float f: return f;
                case long l: return l;
                case int i: return i;
                case decimal m: return (double)m;
                default: return null;
            }
        }
    }

    /// <summary>A pack's manifest, read by the caller's JSON into what the planner needs.</summary>
    public sealed class PackManifest
    {
        public int? Format { get; set; }
        public string? GameName { get; set; }
        public string? SteamId { get; set; }

        /// <summary>The language the pictures' text is in — the exporting translation's target.</summary>
        public string? TargetLanguage { get; set; }

        public List<ImageDefinition> Images { get; set; } = new List<ImageDefinition>();

        /// <summary>The manifest field names, written once.</summary>
        public const string FormatField = "format", GameField = "game", GameNameField = "name",
            SteamIdField = "steam_id", MadeByField = "made_by", ImagesField = "images",
            TargetLanguageField = "target_language";
    }

    /// <summary>What the game holds, as far as a plan needs it — read by the product.</summary>
    public sealed class GameAssetSide
    {
        public string GameName { get; set; } = "";
        public string? ProductName { get; set; }
        public string? SteamId { get; set; }

        public bool TranslationExists { get; set; }

        /// <summary>It exists and cannot be read safely: image settings are never written into it.</summary>
        public bool TranslationDamaged { get; set; }

        public string? TargetLanguage { get; set; }

        /// <summary>The translation's image settings, in file order.</summary>
        public List<ImageDefinition> Definitions { get; set; } = new List<ImageDefinition>();

        /// <summary>The SHA-256 of a file already in the game's fonts/ or images/, or null when it is not there.</summary>
        public Func<AssetKind, string, string?> ExistingSha256 { get; set; } = (_, __) => null;

        /// <summary>How many bytes may be read for this drop — the drive's free room.</summary>
        public long Room { get; set; } = long.MaxValue;
    }

    /// <summary>A file handed over: its name, and how to read it again.</summary>
    public sealed class DroppedFile
    {
        public DroppedFile(string name, Func<Stream> open)
        {
            Name = name;
            Open = open;
        }

        public string Name { get; }

        /// <summary>Opens the file. ⚠ A pack is read from it with seeks: it must return a seekable stream.</summary>
        public Func<Stream> Open { get; }
    }

    /// <summary>One font or image offered to a game — a file handed over on its own, or an entry of a pack.</summary>
    public sealed class IncomingAsset
    {
        public IncomingAsset(AssetKind kind, string name, string from, int source, string? entryName, long length, string sha256)
        {
            Kind = kind;
            Name = name;
            From = from;
            Source = source;
            EntryName = entryName;
            Length = length;
            Sha256 = sha256;
        }

        public AssetKind Kind { get; }
        public string Name { get; }

        /// <summary>What the person handed over, as they would recognise it: the file's name, or the pack's.</summary>
        public string From { get; }

        /// <summary>Which of the dropped files it comes from — their index in the list planned.</summary>
        public int Source { get; }

        /// <summary>The entry inside the pack; null for a file given on its own.</summary>
        public string? EntryName { get; }

        public long Length { get; }
        public string Sha256 { get; }
    }

    public sealed class PlannedAsset
    {
        public PlannedAsset(IncomingAsset asset, AssetChange change)
        {
            Asset = asset;
            Change = change;
        }

        public IncomingAsset Asset { get; }
        public AssetChange Change { get; }
    }

    /// <summary>An image setting a pack carries, measured against the game's translation.</summary>
    public sealed class PlannedDefinition
    {
        public PlannedDefinition(ImageDefinition definition, AssetChange change, string from)
        {
            Definition = definition;
            Change = change;
            From = from;
        }

        public ImageDefinition Definition { get; }
        public string SpriteName => Definition.Sprite;
        public string File => Definition.File ?? "";
        public AssetChange Change { get; }
        public string From { get; }
    }

    /// <summary>Something handed over that will not be written, and why — in the words a screen shows.</summary>
    public sealed class RefusedAsset
    {
        public RefusedAsset(string name, string reason)
        {
            Name = name;
            Reason = reason;
        }

        public string Name { get; }
        public string Reason { get; }
    }

    /// <summary>
    /// A pack whose pictures carry text in another language than this game's translation.
    ///
    /// ⚠ Said, never refused (user, 2026-09-27): a picture holds translated text, so a French pack on a
    /// game translated into German shows French — AND it tells that player exactly which pictures to
    /// redo, at which size, since the settings come with them.
    /// </summary>
    public sealed class LanguageMismatch
    {
        public LanguageMismatch(string pack, string packLanguage, string gameLanguage)
        {
            Pack = pack;
            PackLanguage = packLanguage;
            GameLanguage = gameLanguage;
        }

        public string Pack { get; }
        public string PackLanguage { get; }
        public string GameLanguage { get; }
    }

    /// <summary>One row of a plan: a font, or an image with its setting — accepted or declined as a whole.</summary>
    public sealed class AssetOffer
    {
        public AssetOffer(AssetKind kind, string name, AssetChange change, string from,
                          IReadOnlyList<PlannedAsset> files, IReadOnlyList<PlannedDefinition> definitions)
        {
            Kind = kind;
            Name = name;
            Change = change;
            From = from;
            Files = files;
            Definitions = definitions;
        }

        public AssetKind Kind { get; }

        /// <summary>The font's file name, or the image's sprite name (what the mod's inspector shows).</summary>
        public string Name { get; }

        public AssetChange Change { get; }
        public string From { get; }
        public IReadOnlyList<PlannedAsset> Files { get; }
        public IReadOnlyList<PlannedDefinition> Definitions { get; }

        /// <summary>Stable across plans of the same files, so a screen can remember a declined row.</summary>
        public string Key => Kind + ":" + Name;
    }

    /// <summary>Everything a set of dropped files would do, before anything is written.</summary>
    public sealed class AssetPlan
    {
        public AssetPlan(IReadOnlyList<PlannedAsset> files, IReadOnlyList<PlannedDefinition> definitions,
                         IReadOnlyList<RefusedAsset> refused, IReadOnlyList<string> madeFor,
                         IReadOnlyList<LanguageMismatch> otherLanguages)
        {
            Files = files;
            Definitions = definitions;
            Refused = refused;
            MadeFor = madeFor;
            OtherLanguages = otherLanguages;
            Offers = BuildOffers();
        }

        public static readonly AssetPlan Empty = new AssetPlan(
            new PlannedAsset[0], new PlannedDefinition[0], new RefusedAsset[0], new string[0], new LanguageMismatch[0]);

        public IReadOnlyList<PlannedAsset> Files { get; }
        public IReadOnlyList<PlannedDefinition> Definitions { get; }
        public IReadOnlyList<RefusedAsset> Refused { get; }

        /// <summary>Games a pack names that are not this one — said, never refused.</summary>
        public IReadOnlyList<string> MadeFor { get; }

        /// <summary>Packs whose pictures were made for another target language — said, never refused.</summary>
        public IReadOnlyList<LanguageMismatch> OtherLanguages { get; }

        /// <summary>
        /// What a person decides on, one row each: a font, or an image WITH its setting.
        ///
        /// 🔴 **An image and its setting are one choice.** Declining the replacement of a picture while
        /// its changed setting went through left the translation describing a new pivot for the old
        /// picture. Paired here, so no screen can offer them apart.
        /// </summary>
        public IReadOnlyList<AssetOffer> Offers { get; }

        /// <summary>How many offers would change something.</summary>
        public int Changes => Offers.Count(o => o.Change != AssetChange.Same);

        public AssetPlan WithRefused(RefusedAsset refusal) =>
            new AssetPlan(Files, Definitions, Refused.Concat(new[] { refusal }).ToList(), MadeFor, OtherLanguages);

        private IReadOnlyList<AssetOffer> BuildOffers()
        {
            var offers = new List<AssetOffer>();

            foreach (var font in Files.Where(f => f.Asset.Kind == AssetKind.Font))
                offers.Add(new AssetOffer(AssetKind.Font, font.Asset.Name, font.Change, font.Asset.From, new[] { font }, new PlannedDefinition[0]));

            var images = new Dictionary<string, PlannedAsset>(StringComparer.OrdinalIgnoreCase);
            foreach (var image in Files.Where(f => f.Asset.Kind == AssetKind.Image)) images[image.Asset.Name] = image;
            var paired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var definition in Definitions)
            {
                images.TryGetValue(definition.File, out var file);
                if (file != null) paired.Add(definition.File);

                var change = Strongest(definition.Change, file?.Change ?? AssetChange.Same);
                offers.Add(new AssetOffer(AssetKind.Image, definition.SpriteName, change, definition.From,
                                          file == null ? new PlannedAsset[0] : new[] { file }, new[] { definition }));
            }

            // A picture given on its own, filling a file the translation already names.
            foreach (var pair in images)
            {
                if (!paired.Contains(pair.Key))
                    offers.Add(new AssetOffer(AssetKind.Image, pair.Key, pair.Value.Change, pair.Value.Asset.From, new[] { pair.Value }, new PlannedDefinition[0]));
            }

            return offers;
        }

        private static AssetChange Strongest(AssetChange a, AssetChange b) =>
            a == AssetChange.Replace || b == AssetChange.Replace ? AssetChange.Replace
            : a == AssetChange.Add || b == AssetChange.Add ? AssetChange.Add
            : AssetChange.Same;
    }

    /// <summary>
    /// What a set of dropped files — loose fonts and pictures, or `.ugtpack` packs — would do to one
    /// game, decided the same way in the mod and in UGT Manager. Reads what is handed over; writes
    /// nothing, and touches no disk: the product opens the files, reads the game, parses the JSON.
    ///
    /// 🔴 **Every guard lives here, so both products refuse exactly the same things.** A name that is
    /// not a bare, Windows-safe file of an accepted kind; bytes that are not what the extension claims;
    /// a pack larger than the drive's room; an entry larger than it declares or whose checksum differs
    /// (the whole pack refused); a manifest out of proportion; a picture nothing uses; a setting with
    /// fields nobody reads (rebuilt from the allow-list); image settings for a translation that is
    /// absent or cannot be read safely. Design and decisions: analyse/manager-onglet-assets.md (root).
    /// </summary>
    public static class AssetPlanner
    {
        // ── What screens say ──────────────────────────────────────────────

        public const string NoTranslationYet = "This game has no translation file yet. Play it once with UGT Mod, then add the pack again.";
        public const string DamagedTranslation = "This game's translation file cannot be read safely, so it is left as it is. Its image settings cannot be changed here.";
        public const string Misleading = "Damaged, or built to mislead: a file in it is not what the pack says. Nothing from it was used.";
        public const string UnsafeName = "Its name cannot be used as a file name in every game folder.";
        public const string LooseImageUnused = "No image of this game's translation uses this file. Images come with their settings in a .ugtpack.";
        public const string NotAnAsset = "Not a font (.ttf, .otf), an image (.png) or a .ugtpack.";

        /// <summary>
        /// Room for one picture's settings in a manifest — a ratio to what the pack carries, not a size
        /// picked for manifests. A setting is a dozen short fields, a few hundred bytes written out;
        /// this leaves ten times that, and a manifest past it describes pictures the pack does not hold.
        /// </summary>
        public const int ManifestBytesPerImage = 4096;

        public static string NotWhatItsNameSays(AssetKind kind) =>
            kind == AssetKind.Font
                ? "Its content is not a font, whatever its name says."
                : "Its content is not a PNG image, whatever its name says.";

        public static string TooLarge(long size, long free) =>
            "Too large: " + Megabytes(size) + ", and the drive holding this game has " + Megabytes(Math.Max(0, free)) + " free.";

        public static string Megabytes(long bytes) =>
            (bytes / 1024d / 1024d).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " MB";

        /// <summary>What a screen says about a pack made from another game — information, never a refusal.</summary>
        public static string MadeForText(string otherGame) => "Made for " + otherGame + ". Check that it is meant for this game.";

        /// <summary>
        /// What a screen says about pictures made for another language — and the reason they are still
        /// worth having: the rows name each picture to remake, with its settings (user, 2026-09-27).
        /// </summary>
        public static string OtherLanguageText(LanguageMismatch mismatch)
        {
            var made = Languages.NameOf(mismatch.PackLanguage) ?? mismatch.PackLanguage;
            var here = Languages.NameOf(mismatch.GameLanguage) ?? mismatch.GameLanguage;
            return "Images made for " + made + ". This game's translation is " + here + ": the pictures in "
                   + mismatch.Pack + " still show " + made + " text.";
        }

        /// <summary>The line above a plan's rows: what arrives, what differs, what was left out.</summary>
        public static string Summary(AssetPlan plan)
        {
            var parts = new List<string>();
            var adds = plan.Offers.Count(o => o.Change == AssetChange.Add);
            var replaces = plan.Offers.Count(o => o.Change == AssetChange.Replace);
            var same = plan.Offers.Count(o => o.Change == AssetChange.Same);

            if (adds > 0) parts.Add(adds + " new");
            if (replaces > 0) parts.Add(replaces + " already in this game and different");
            if (same > 0) parts.Add(same + " already in this game");
            if (plan.Refused.Count > 0) parts.Add(plan.Refused.Count + " left out");
            return string.Join(" · ", parts);
        }

        /// <summary>Why image settings cannot go into this game's translation — null when they can.</summary>
        public static string? ImageSettingsRefusal(GameAssetSide game) =>
            !game.TranslationExists ? NoTranslationYet : game.TranslationDamaged ? DamagedTranslation : null;

        // ── The plan ──────────────────────────────────────────────────────

        /// <summary>
        /// Reads every file handed over and says what each would do to this game.
        ///
        /// ⚠ The same name handed over twice: the LAST one counts, as dropping a newer copy means.
        /// Refusals are said per file, never silently dropped.
        /// </summary>
        /// <param name="parseManifest">The product's JSON reader for a manifest; null when it is not JSON at all.</param>
        public static AssetPlan Plan(GameAssetSide game, IReadOnlyList<DroppedFile> dropped, Func<byte[], PackManifest?> parseManifest)
        {
            var existing = new Dictionary<string, ImageDefinition>(TranslationFiles.SpriteNames);
            foreach (var definition in game.Definitions) existing[definition.Sprite] = definition;   // the last wins, as in the mod

            // Files the translation already names — what a picture given on its own may fill in.
            var named = new HashSet<string>(existing.Values.Select(d => d.File).OfType<string>(), StringComparer.OrdinalIgnoreCase);

            // ⚠ Case ignored on both keys: a file name as Windows compares it, a sprite as the mod does.
            var files = new Dictionary<string, IncomingAsset>(StringComparer.OrdinalIgnoreCase);
            var definitions = new Dictionary<string, PlannedDefinition>(TranslationFiles.SpriteNames);
            var refused = new List<RefusedAsset>();
            var madeFor = new List<string>();
            var otherLanguages = new List<LanguageMismatch>();

            // 🔴 **Never read more than could be written.** The drive's room, shared by the whole drop.
            var budget = new[] { game.Room };

            for (var index = 0; index < dropped.Count; index++)
            {
                var file = dropped[index];
                var name = file.Name;

                try
                {
                    if (!AssetPacks.IsSafeFileName(name))
                    {
                        refused.Add(new RefusedAsset(name, UnsafeName));
                        continue;
                    }

                    if (AssetPacks.IsPack(name))
                    {
                        using (var stream = file.Open())
                            ReadPack(game, stream, index, name, files, definitions, existing, named, refused, madeFor, otherLanguages, budget, parseManifest);
                        continue;
                    }

                    var kind = AssetPacks.KindOfFile(name);
                    if (kind == null)
                    {
                        refused.Add(new RefusedAsset(name, NotAnAsset));
                        continue;
                    }

                    if (kind == AssetKind.Image && !named.Contains(name))
                    {
                        refused.Add(new RefusedAsset(name, LooseImageUnused));
                        continue;
                    }

                    using (var stream = file.Open())
                    {
                        // A file on disk states its real size: too large for the drive, it is not read.
                        var size = stream.CanSeek ? stream.Length : -1;
                        if (size > budget[0])
                        {
                            refused.Add(new RefusedAsset(name, TooLarge(size, budget[0])));
                            continue;
                        }

                        var measured = AssetPackReader.Measure(stream, budget[0]);
                        budget[0] -= measured.Length;

                        if (measured.Over)
                        {
                            refused.Add(new RefusedAsset(name, TooLarge(measured.Length, budget[0] + measured.Length)));
                            continue;
                        }

                        if (!AssetPacks.ContentMatches(name, measured.Head, measured.HeadCount))
                        {
                            refused.Add(new RefusedAsset(name, NotWhatItsNameSays(kind.Value)));
                            continue;
                        }

                        files[Key(kind.Value, name)] = new IncomingAsset(kind.Value, name, name, index, null, measured.Length, measured.Sha256);
                    }
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidDataException)
                {
                    refused.Add(new RefusedAsset(name, "Could not be read: " + e.Message));
                }
            }

            var plannedFiles = files.Values
                .Select(asset => new PlannedAsset(asset, ChangeOf(game, asset)))
                .OrderBy(p => p.Asset.Kind).ThenBy(p => p.Asset.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var plannedDefinitions = definitions.Values
                .OrderBy(d => d.SpriteName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new AssetPlan(plannedFiles, plannedDefinitions, refused, madeFor.Distinct().ToList(), otherLanguages);
        }

        private static string Key(AssetKind kind, string name) => kind + "/" + name;

        private static AssetChange ChangeOf(GameAssetSide game, IncomingAsset asset)
        {
            var here = game.ExistingSha256(asset.Kind, asset.Name);
            return here == null ? AssetChange.Add
                : string.Equals(here, asset.Sha256, StringComparison.OrdinalIgnoreCase) ? AssetChange.Same
                : AssetChange.Replace;
        }

        /// <summary>
        /// A pack's manifest alone, under the bounds a plan reads it with — null with the reason
        /// otherwise. For a pack opened before any game is chosen (UGT Manager opening a .ugtpack
        /// from the file explorer): it names the game to offer. Nothing read here is written; the
        /// plan reads the pack again, whole, once the game is known.
        /// </summary>
        public static PackManifest? ReadManifest(Stream zip, Func<byte[], PackManifest?> parseManifest, out string refusal)
        {
            var entries = AssetPackReader.Entries(zip);
            var assets = entries.Count(e => AssetPacks.TryEntry(e.Name, out _, out _));
            return ManifestOf(zip, entries, assets, parseManifest, out refusal);
        }

        /// <summary>The manifest entry, bounded, parsed and of a format this build reads.</summary>
        private static PackManifest? ManifestOf(Stream zip, List<PackEntry> entries, int assetCount,
                                                Func<byte[], PackManifest?> parseManifest, out string refusal)
        {
            refusal = "";

            var manifestEntry = entries.FirstOrDefault(e => e.Name == AssetPacks.ManifestName);
            if (manifestEntry == null)
            {
                refusal = "Not a UGT asset pack: it has no manifest.";
                return null;
            }

            // ⚠ Parsed in memory, so bounded by what it can legitimately hold.
            if (manifestEntry.Size > ManifestBytesPerImage * (assetCount + 1))
            {
                refusal = Misleading;
                return null;
            }

            byte[] manifestBytes;
            using (var stream = AssetPackReader.Open(zip, manifestEntry))
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                if (memory.Length > manifestEntry.Size)
                {
                    refusal = Misleading;
                    return null;
                }

                manifestBytes = memory.ToArray();
            }

            var manifest = parseManifest(manifestBytes);
            if (manifest == null)
            {
                refusal = "Not a UGT asset pack: its manifest cannot be read.";
                return null;
            }

            if (manifest.Format == null || manifest.Format < 1)
            {
                refusal = "Not a UGT asset pack: its manifest has no format.";
                return null;
            }

            if (manifest.Format > AssetPacks.Format)
            {
                refusal = "Made by a newer UGT Manager. Update UGT Manager to open it.";
                return null;
            }

            return manifest;
        }

        /// <summary>
        /// Reads one pack into the plan — or nothing of it at all.
        ///
        /// 🔴 **A pack that lies is refused whole.** Sizes are checked against what the pack DECLARES
        /// before a byte is unpacked; every read stops one byte past its declaration; a checksum that
        /// differs exposes a truncated or altered entry. Nothing from such a pack is kept: a file built
        /// to mislead is not sorted into its honest and dishonest halves.
        /// </summary>
        private static void ReadPack(GameAssetSide game, Stream zip, int index, string packName,
                                     Dictionary<string, IncomingAsset> files,
                                     Dictionary<string, PlannedDefinition> definitions,
                                     Dictionary<string, ImageDefinition> existing, HashSet<string> named,
                                     List<RefusedAsset> refused, List<string> madeFor,
                                     List<LanguageMismatch> otherLanguages, long[] budget,
                                     Func<byte[], PackManifest?> parseManifest)
        {
            var entries = AssetPackReader.Entries(zip);

            // What the pack declares, from its directory alone — nothing is unpacked yet.
            var assetEntries = entries.Where(e => AssetPacks.TryEntry(e.Name, out _, out _)).ToList();
            var declared = assetEntries.Sum(e => e.Size);
            if (declared > budget[0])
            {
                refused.Add(new RefusedAsset(packName, TooLarge(declared, budget[0])));
                return;
            }

            var manifest = ManifestOf(zip, entries, assetEntries.Count, parseManifest, out var manifestRefusal);
            if (manifest == null)
            {
                refused.Add(new RefusedAsset(packName, manifestRefusal));
                return;
            }

            // Into the pack's own lists first: they reach the plan only if the whole pack is honest.
            var fonts = new List<IncomingAsset>();
            var carried = new Dictionary<string, IncomingAsset>(StringComparer.OrdinalIgnoreCase);
            var packRefused = new List<RefusedAsset>();
            var ignored = entries.Count(e => e.Name != AssetPacks.ManifestName && !e.IsFolder && !AssetPacks.TryEntry(e.Name, out _, out _));

            foreach (var entry in assetEntries)
            {
                AssetPacks.TryEntry(entry.Name, out var kind, out var bare);

                PackMeasure measured;
                using (var stream = AssetPackReader.Open(zip, entry))
                    measured = AssetPackReader.Measure(stream, entry.Size);

                budget[0] -= measured.Length;

                if (measured.Over || measured.Crc32 != entry.Crc32)
                {
                    refused.Add(new RefusedAsset(packName, Misleading));
                    return;
                }

                if (!AssetPacks.ContentMatches(bare, measured.Head, measured.HeadCount))
                {
                    packRefused.Add(new RefusedAsset(bare, NotWhatItsNameSays(kind)));
                    continue;
                }

                var asset = new IncomingAsset(kind, bare, packName, index, entry.Name, measured.Length, measured.Sha256);
                if (kind == AssetKind.Font) fonts.Add(asset);
                else carried[bare] = asset;
            }

            if (ignored > 0)
            {
                packRefused.Add(new RefusedAsset(packName, ignored == 1
                    ? "1 file in it is not a font or an image, and was left out."
                    : ignored + " files in it are not fonts or images, and were left out."));
            }

            // The settings, each tied to a picture the pack carries.
            var packDefinitions = new Dictionary<string, PlannedDefinition>(TranslationFiles.SpriteNames);
            var defined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var settingsRefusal = ImageSettingsRefusal(game);

            foreach (var definition in manifest.Images)
            {
                if (definition.File == null || !carried.ContainsKey(definition.File))
                {
                    packRefused.Add(new RefusedAsset(definition.Sprite, "Its image is missing from " + packName + "."));
                    continue;
                }

                if (settingsRefusal != null)
                {
                    packRefused.Add(new RefusedAsset(definition.Sprite, settingsRefusal));
                    continue;
                }

                var change = existing.TryGetValue(definition.Sprite, out var here)
                    ? here.SameAs(definition) ? AssetChange.Same : AssetChange.Replace
                    : AssetChange.Add;

                packDefinitions[definition.Sprite] = new PlannedDefinition(definition, change, packName);
                defined.Add(definition.File);
            }

            // Honest throughout: now, and only now, what it carries joins the plan.
            if (OtherGame(game, manifest) is string other) madeFor.Add(other);

            // Only a pack bringing pictures: fonts carry no language.
            if (packDefinitions.Count > 0 && Languages.IsSettled(manifest.TargetLanguage) && Languages.IsSettled(game.TargetLanguage)
                && !Languages.Matches(manifest.TargetLanguage, Languages.CodeOf(game.TargetLanguage) ?? game.TargetLanguage))
            {
                otherLanguages.Add(new LanguageMismatch(packName, manifest.TargetLanguage!, game.TargetLanguage!));
            }

            foreach (var font in fonts) files[Key(AssetKind.Font, font.Name)] = font;
            foreach (var pair in packDefinitions) definitions[pair.Key] = pair.Value;

            foreach (var pair in carried)
            {
                if (defined.Contains(pair.Key) || named.Contains(pair.Key))
                    files[Key(AssetKind.Image, pair.Key)] = pair.Value;
                else
                    packRefused.Add(new RefusedAsset(pair.Key, "No image setting in " + packName + " or in this game's translation uses it."));
            }

            refused.AddRange(packRefused);
        }

        /// <summary>
        /// The game a pack names, when it is visibly another one — null when it matches or says nothing.
        /// ⚠ Information only (user, 2026-09-27): no identity of a game is reliable enough to refuse on.
        /// </summary>
        public static string? OtherGame(GameAssetSide game, PackManifest manifest)
        {
            if (!string.IsNullOrWhiteSpace(manifest.SteamId) && !string.IsNullOrWhiteSpace(game.SteamId))
                return string.Equals(manifest.SteamId, game.SteamId, StringComparison.Ordinal) ? null : manifest.GameName ?? manifest.SteamId;

            var name = manifest.GameName?.Trim();
            if (string.IsNullOrEmpty(name)) return null;

            var matches = string.Equals(name, game.GameName.Trim(), StringComparison.OrdinalIgnoreCase)
                          || string.Equals(name, game.ProductName?.Trim(), StringComparison.OrdinalIgnoreCase);
            return matches ? null : name;
        }

        // ── Merging settings into a translation ───────────────────────────

        /// <summary>One change to the translation's image section, by position in the file.</summary>
        public sealed class SectionEdit
        {
            public SectionEdit(int? replaceAt, ImageDefinition definition, IReadOnlyList<int> removeAt)
            {
                ReplaceAt = replaceAt;
                Definition = definition;
                RemoveAt = removeAt;
            }

            /// <summary>The entry to overwrite; null to append.</summary>
            public int? ReplaceAt { get; }

            public ImageDefinition Definition { get; }

            /// <summary>Other entries for the same sprite, to remove — highest position first.</summary>
            public IReadOnlyList<int> RemoveAt { get; }
        }

        /// <summary>
        /// How to merge settings into a translation whose image section lists these sprites, in file
        /// order (null where an entry has none).
        ///
        /// 🔴 **One entry per sprite, as the mod reads them** — case ignored, the last one winning. The
        /// first match takes the new setting; any other entry for the same sprite goes, or it would come
        /// after ours and override it in the game. Entries not touched are left exactly as they are.
        /// ⚠ Apply the edits in order: each one's positions are counted after the previous removals.
        /// </summary>
        public static List<SectionEdit> Merge(IReadOnlyList<string?> spritesInFile, IEnumerable<ImageDefinition> incoming)
        {
            var sprites = spritesInFile.ToList();
            var edits = new List<SectionEdit>();

            foreach (var definition in incoming)
            {
                var matches = new List<int>();
                for (var i = 0; i < sprites.Count; i++)
                {
                    if (sprites[i] != null && TranslationFiles.SpriteNames.Equals(sprites[i], definition.Sprite)) matches.Add(i);
                }

                if (matches.Count == 0)
                {
                    edits.Add(new SectionEdit(null, definition, new int[0]));
                    sprites.Add(definition.Sprite);
                    continue;
                }

                var removals = matches.Skip(1).OrderByDescending(i => i).ToList();
                edits.Add(new SectionEdit(matches[0], definition, removals));
                foreach (var i in removals) sprites.RemoveAt(i);
            }

            return edits;
        }
    }
}
