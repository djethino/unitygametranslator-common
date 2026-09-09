namespace UnityGameTranslator.Common
{
    /// <summary>What happened to one settings section since the last sync.</summary>
    public enum SettingsSectionState
    {
        /// <summary>Both sides hold the same thing. Nothing to decide.</summary>
        Same,

        /// <summary>Only the incoming side moved. Take it, silently.</summary>
        TheirsChanged,

        /// <summary>Only we moved. Keep ours, silently.</summary>
        OursChanged,

        /// <summary>Both moved since the shared baseline. Only the player can decide.</summary>
        BothChanged,

        /// <summary>
        /// They differ and there is no shared baseline to attribute the change to.
        /// Indistinguishable from <see cref="BothChanged"/> in practice — ask.
        /// </summary>
        Unknown,
    }

    /// <summary>
    /// The six settings sections that travel inside a translation file beside its lines — what they
    /// are called, what they are called in the file, what they are called on screen, and what
    /// becomes of one when both sides have moved.
    ///
    /// 🔴 **Here because the list already existed twice.** The mod held it in C# and the website
    /// holds it in PHP (<c>Translation::SETTINGS_SECTIONS</c>), and the mod's own comment said so:
    /// "Section names match the website's ... so both sides name the same things in the same
    /// order." A comment is not a mechanism — it is a request that somebody remember. The names,
    /// the ORDER (every screen that lists or compares them reads it) and the file keys are one
    /// table, and a second engine's Core will need the same one.
    ///
    /// ⚠ **The file keys are a contract**, not an implementation detail: they are what a
    /// translation carries on disk and what the website reads back out of it. Renaming one is a
    /// migration, never a tidy-up.
    ///
    /// ⚠ Names only, and one rule. Reading or comparing a section needs a JSON document, which
    /// this library deliberately has no way to hold — so the caller compares and asks
    /// <see cref="Classify"/> what its answers mean.
    /// </summary>
    public static class SettingsSections
    {
        public const string Fonts = "fonts";
        public const string FontRules = "font_rules";
        public const string Images = "images";
        public const string Exclusions = "exclusions";
        public const string Variables = "variables";
        public const string GameSettings = "game_settings";

        /// <summary>
        /// The key each section uses inside the translation file.
        ///
        /// ⚠ Constants as well as <see cref="JsonKey"/>, so another name for one of them can be
        /// written as an alias rather than as a second copy of the string — see
        /// <see cref="TranslationFiles.ImagesSection"/>, which is this table's images row under the
        /// name the backup code reads it by.
        /// </summary>
        public const string FontsKey = "_fonts";

        public const string FontRulesKey = "_font_overrides";
        public const string ImagesKey = "_image_replacements";
        public const string ExclusionsKey = "_exclusions";
        public const string VariablesKey = "_variables";
        public const string GameSettingsKey = "_settings";

        /// <summary>
        /// The sections, in display order.
        ///
        /// ⚠ The order is part of the table: every screen that lists or compares them reads it, and
        /// two products listing the same six things differently is the defect this class exists to
        /// prevent.
        /// </summary>
        public static readonly string[] All =
        {
            Fonts, FontRules, Images, Exclusions, Variables, GameSettings
        };

        /// <summary>The key this section uses inside the translation file, or null if unknown.</summary>
        public static string JsonKey(string section)
        {
            switch (section)
            {
                case Fonts: return FontsKey;
                case FontRules: return FontRulesKey;
                case Images: return ImagesKey;
                case Exclusions: return ExclusionsKey;
                case Variables: return VariablesKey;
                case GameSettings: return GameSettingsKey;
                default: return null;
            }
        }

        /// <summary>
        /// Which section a key in the translation file belongs to, or null if it belongs to none.
        ///
        /// 🔴 **The reverse of <see cref="JsonKey"/>, and it exists so that READING a file can be
        /// driven by this table the way WRITING one already is.** The mod builds a file by walking
        /// <see cref="All"/> and asking <see cref="JsonKey"/> for each name; it read one back with
        /// a hand-written branch per key. So a seventh section added here would be written by
        /// everybody and read by nobody — in silence, since nothing compares the two lists.
        ///
        /// ⚠ A key it does not know is not an error: a translation file carries other underscore
        /// keys (`_uuid`, `_source`, `_game`…) and the lines themselves. Null means "not one of
        /// mine", which the caller goes on to handle.
        /// </summary>
        public static string SectionOf(string jsonKey)
        {
            switch (jsonKey)
            {
                case FontsKey: return Fonts;
                case FontRulesKey: return FontRules;
                case ImagesKey: return Images;
                case ExclusionsKey: return Exclusions;
                case VariablesKey: return Variables;
                case GameSettingsKey: return GameSettings;
                default: return null;
            }
        }

        /// <summary>
        /// The short label shown to a player. An unknown section is written as it arrived rather
        /// than dropped: a screen listing five of six sections says nothing about the sixth.
        /// </summary>
        public static string Name(string section)
        {
            switch (section)
            {
                case Fonts: return "Fonts";
                case FontRules: return "Font rules";
                case Images: return "Image replacements";
                case Exclusions: return "Exclusions";
                case Variables: return "Variables";
                case GameSettings: return "Game settings";
                default: return section;
            }
        }

        /// <summary>
        /// One line saying what somebody loses or gains by replacing this section. Shown beside
        /// each choice — a section name alone does not let anyone decide.
        /// </summary>
        public static string Description(string section)
        {
            switch (section)
            {
                case Fonts: return "Which fonts are translated, their fallback and their size";
                case FontRules: return "Font substitutions applied by pattern";
                case Images: return "Images swapped in-game (the PNG files stay on your disk)";
                case Exclusions: return "Text left in the game's original language";
                case Variables: return "Game values inserted into translated sentences";
                case GameSettings: return "Per-game options such as typewriter detection";
                default: return string.Empty;
            }
        }

        /// <summary>
        /// What became of one section, from three comparisons the caller has made.
        ///
        /// 🔴 **The point is to ask RARELY.** A section only reaches the player when both sides
        /// moved since the last common state, or when there is no common state to compare against.
        /// Everything else is decided here: an untouched section takes the incoming value, and a
        /// section only we changed keeps ours. Without this, every download would either ask about
        /// six sections or silently overwrite them — which is what it did before.
        ///
        /// ⚠ The comparisons are the caller's because comparing needs the document; the meaning of
        /// their answers is the rule, and it is the same rule in every program that syncs a
        /// translation.
        /// </summary>
        /// <param name="oursMatchesTheirs">The two sides hold the same thing in this section.</param>
        /// <param name="hasAncestor">There is a snapshot of what both sides last agreed on.</param>
        /// <param name="oursMatchesAncestor">Ours is unchanged since that snapshot.</param>
        /// <param name="theirsMatchesAncestor">Theirs is unchanged since that snapshot.</param>
        public static SettingsSectionState Classify(bool oursMatchesTheirs, bool hasAncestor,
                                                    bool oursMatchesAncestor, bool theirsMatchesAncestor)
        {
            if (oursMatchesTheirs) return SettingsSectionState.Same;

            if (!hasAncestor) return SettingsSectionState.Unknown;

            bool weMoved = !oursMatchesAncestor;
            bool theyMoved = !theirsMatchesAncestor;

            if (weMoved && theyMoved) return SettingsSectionState.BothChanged;
            if (theyMoved) return SettingsSectionState.TheirsChanged;
            if (weMoved) return SettingsSectionState.OursChanged;

            // They differ from each other yet neither differs from the ancestor: impossible unless
            // the comparison is inconsistent. Ask rather than pick a side on a contradiction.
            return SettingsSectionState.Unknown;
        }

        /// <summary>
        /// Does this one need the player? Everything else decides itself — which is what makes the
        /// question worth asking when it is finally put.
        /// </summary>
        public static bool NeedsDecision(SettingsSectionState state) =>
            state == SettingsSectionState.BothChanged || state == SettingsSectionState.Unknown;
    }
}
