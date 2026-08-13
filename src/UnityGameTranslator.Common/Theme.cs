namespace UnityGameTranslator.Common
{
    /// <summary>
    /// A colour, with no opinion about who draws it.
    ///
    /// This library is consumed by a Unity mod and by an Avalonia application, which have
    /// incompatible colour types and no wish to learn each other's. So the shared value is three
    /// bytes, and each side converts on the way in — one line in each.
    ///
    /// No alpha, deliberately. Opacity is not a shared decision: the mod's panel is nearly opaque
    /// because it floats over a running game, a desktop window has no such worry, and a web page
    /// solves it with its own stacking. The COLOUR is what has to match.
    /// </summary>
    public readonly struct Rgb
    {
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;

        public Rgb(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        /// <summary>From the notation everyone writing a palette actually uses: <c>0x9810FA</c>.</summary>
        public Rgb(int hex)
        {
            R = (byte)((hex >> 16) & 0xFF);
            G = (byte)((hex >> 8) & 0xFF);
            B = (byte)(hex & 0xFF);
        }

        /// <summary>The same three channels as 0-1 floats, which is what Unity wants.</summary>
        public float Rf { get { return R / 255f; } }
        public float Gf { get { return G / 255f; } }
        public float Bf { get { return B / 255f; } }

        /// <summary>Upper-case, no leading hash — the form used in the comments below.</summary>
        public string ToHex()
        {
            return R.ToString("X2") + G.ToString("X2") + B.ToString("X2");
        }

        public override string ToString()
        {
            return "#" + ToHex();
        }
    }

    /// <summary>
    /// The colours of the product, in one place, for the three programs that wear them.
    ///
    /// ⚠ THE WEBSITE IS THE REFERENCE, exactly as it is for <see cref="Quality"/>. Every value here
    /// was read out of the running site's CSS custom properties and converted to sRGB — not sampled
    /// from a screenshot, not guessed from a class name. Each carries the Tailwind token it comes
    /// from; change one and this stops being the site's palette.
    ///
    /// ⚠ The site runs Tailwind **v4**, whose palette was rebuilt in oklch and is NOT v3's. The
    /// differences are large enough to see: purple-600 went #9333EA → #9810FA, green-500
    /// #22C55E → #00C950, orange-500 #F97316 → #FF6900. Both consumers quoted v3 hexes at some
    /// point; that is the mistake this file exists to make impossible to repeat.
    ///
    /// ⚠ The website is PHP and cannot consume this — same situation as Quality. It is the source,
    /// so it needs nothing from here; what matters is that nobody edits these values without
    /// looking at what the site actually renders.
    ///
    /// Why here rather than copied into each program: the two consumers are both C#, they already
    /// share the rules they must not contradict, and a palette is exactly such a rule. The proof
    /// that copying does not hold: the quality bar below was drawn amber in the mod and orange
    /// everywhere else, for months, while all three files claimed in their comments to be drawing
    /// the same measurement.
    /// </summary>
    public static class Theme
    {
        // ── Surfaces ──────────────────────────────────────────────────────────────────────────
        // A ramp of four, which is all the site uses: the page, a recess, a card, something raised
        // on a card.

        /// <summary>The page itself. #0F0F1A.</summary>
        public static readonly Rgb SurfaceBase = new Rgb(0x0F0F1A);

        /// <summary>Recessed: a viewport, a tab strip, the trough of a list. gray-900.</summary>
        public static readonly Rgb SurfaceDeep = new Rgb(0x101828);

        /// <summary>The card — `bg-gray-800`, the single most repeated surface on the site.</summary>
        public static readonly Rgb SurfaceCard = new Rgb(0x1E2939);

        /// <summary>On a card: a row, a field, a callout. gray-700.</summary>
        public static readonly Rgb SurfaceRaised = new Rgb(0x364153);

        /// <summary>The same, under the pointer. gray-600.</summary>
        public static readonly Rgb SurfaceHover = new Rgb(0x4A5565);

        // ── Edges ─────────────────────────────────────────────────────────────────────────────
        // `border-gray-700` appears 242 times in the site's templates. A surface without its edge
        // floats instead of sitting.

        /// <summary>A card's edge. gray-700.</summary>
        public static readonly Rgb BorderSubtle = new Rgb(0x364153);

        /// <summary>A field's edge, which has to read against the field's own fill. gray-600.</summary>
        public static readonly Rgb BorderStrong = new Rgb(0x4A5565);

        // ── Text ──────────────────────────────────────────────────────────────────────────────

        /// <summary>gray-100.</summary>
        public static readonly Rgb TextPrimary = new Rgb(0xF3F4F6);

        /// <summary>gray-300.</summary>
        public static readonly Rgb TextSecondary = new Rgb(0xD1D5DC);

        /// <summary>gray-400.</summary>
        public static readonly Rgb TextMuted = new Rgb(0x99A1AF);

        // ── Accent ────────────────────────────────────────────────────────────────────────────
        // FOUR purples, and the count matters. The site fills with 600, edges and highlights with
        // 500, writes with 400 and presses with 700. One purple doing all four is what made the
        // mod's accent read as flat next to the site's.

        /// <summary>Fills a primary button. purple-600.</summary>
        public static readonly Rgb Accent = new Rgb(0x9810FA);

        /// <summary>Draws an edge, a highlight, a selected state. purple-500.</summary>
        public static readonly Rgb AccentEdge = new Rgb(0xAD46FF);

        /// <summary>Carries text and links on a dark surface. purple-400.</summary>
        public static readonly Rgb AccentSoft = new Rgb(0xC27AFF);

        /// <summary>Pressed. purple-700.</summary>
        public static readonly Rgb AccentDeep = new Rgb(0x8200DB);

        /// <summary>A purple dark enough to sit under text: a selected row's fill. purple-900.</summary>
        public static readonly Rgb AccentDim = new Rgb(0x59168B);

        // ── Status ────────────────────────────────────────────────────────────────────────────
        // The 400 ramp: these are read as text or as a small mark on a dark surface, which is what
        // the site uses 400 for.

        /// <summary>green-400.</summary>
        public static readonly Rgb StatusSuccess = new Rgb(0x05DF72);

        /// <summary>amber-400.</summary>
        public static readonly Rgb StatusWarning = new Rgb(0xFFB900);

        /// <summary>red-400.</summary>
        public static readonly Rgb StatusError = new Rgb(0xFF6467);

        /// <summary>blue-400.</summary>
        public static readonly Rgb StatusInfo = new Rgb(0x50A2FF);

        /// <summary>gray-500.</summary>
        public static readonly Rgb StatusNeutral = new Rgb(0x6A7282);

        // ── What a translation is MADE OF ─────────────────────────────────────────────────────
        //
        // ⚠ FIVE KEYS OF ITS OWN, and never the status colours above. This is the point of the
        // section existing at all.
        //
        // "It went well" and "this line came from an AI" are two different registers. The mod
        // painted the second with the first — QualityAi was StatusWarning — so the AI share came
        // out AMBER in game and ORANGE on the site and in the Manager. Three implementations, each
        // citing the others in its comments, disagreeing on three bands out of five.
        //
        // Order, and it carries meaning: settled first, still-to-do last, so the length of the grey
        // at the end reads as the work left without any arithmetic. Same order in
        // quality-bar.blade.php, in the mod's QualityBar and in the Manager's.
        //
        // The 500 ramp here, not 400: these are filled bands, not text.

        /// <summary>Translated by a person. green-500.</summary>
        public static readonly Rgb QualityHuman = new Rgb(0x00C950);

        /// <summary>Read back and confirmed. blue-500.</summary>
        public static readonly Rgb QualityValidated = new Rgb(0x2B7FFF);

        /// <summary>Machine-translated, not yet read. orange-500.</summary>
        public static readonly Rgb QualityAi = new Rgb(0xFF6900);

        /// <summary>
        /// Kept as is on purpose: met, read, and settled by a decision. Its own band because it is
        /// neither translated nor missing. purple-500.
        /// </summary>
        public static readonly Rgb QualityKept = new Rgb(0xAD46FF);

        /// <summary>Seen in game, nobody has dealt with it yet. gray-500.</summary>
        public static readonly Rgb QualityCapture = new Rgb(0x6A7282);

        /// <summary>The empty bar behind the bands, where one is drawn. gray-700.</summary>
        public static readonly Rgb QualityTrack = new Rgb(0x364153);

        /// <summary>
        /// The mod's own interface (tag M): a provenance, not a degree of translation. It takes the
        /// one colour nothing else uses, and it has NO band in the quality bar on any side.
        /// teal-600.
        /// </summary>
        public static readonly Rgb TagModUi = new Rgb(0x009689);
    }
}
