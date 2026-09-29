namespace UnityGameTranslator.Common
{
    /// <summary>
    /// Which one-shot migrations a game's config.json has been through — spec/config/schema.json,
    /// key <c>config_version</c>, and its <c>x-migrations</c>.
    ///
    /// 🔴 **A file with no number is read as written before the migrations existed**, and they run on
    /// it: an explicit <c>translate_mod_ui: false</c> goes back to undecided, <c>enable_ai: false</c>
    /// on Google or DeepL comes back on. That is right for an old file the mod serialised itself, and
    /// wrong for a file UGT Manager has just created with today's meaning of each key — the person's
    /// « no » from Mod defaults was undone at the game's first launch, and the Manager then offered
    /// to write it again.
    ///
    /// So both writers stamp it: the mod on every file it saves, the Manager on a file it creates.
    /// ⚠ Never on a file that exists: an older number there is a file still to migrate, and only the
    /// mod knows how.
    /// </summary>
    public static class ConfigVersion
    {
        /// <summary>Bumped with each migration added to the mod's ModConfig and to the schema.</summary>
        public const int Current = 3;

        /// <summary>The key, as the file spells it.</summary>
        public const string Key = "config_version";
    }
}
