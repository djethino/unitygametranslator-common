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

        /// <summary>
        /// This colour laid over <paramref name="under"/> at the given strength, flattened into an
        /// opaque one.
        ///
        /// The website expresses a tinted state — a selected row, a callout — as a translucent
        /// layer, and it can afford to: what shows through is the page. Neither consumer can. The
        /// mod's panel floats over a RUNNING GAME, so a translucent row shows scenery rather than
        /// the card; the Manager would show the desktop. Both therefore have to compute the result
        /// and paint it opaque.
        ///
        /// Shared rather than written twice, for the reason the whole file exists: two products
        /// blending "the same" tint by slightly different arithmetic drift exactly like two copies
        /// of a palette do, and a selected row that is not quite the same purple in the two windows
        /// is the kind of thing nobody can name and everybody sees.
        ///
        /// <paramref name="strength"/> is how much of THIS colour ends up in the result: 0 gives
        /// <paramref name="under"/> untouched, 1 gives this one. Values outside 0-1 are clamped —
        /// a tint stronger than the colour itself means nothing.
        /// </summary>
        public Rgb Over(Rgb under, double strength)
        {
            if (strength < 0) strength = 0;
            if (strength > 1) strength = 1;

            return new Rgb(
                Mix(under.R, R, strength),
                Mix(under.G, G, strength),
                Mix(under.B, B, strength));
        }

        /// <summary>
        /// Relative luminance, as WCAG defines it: 0 for black, 1 for white.
        ///
        /// ⚠ NOT the average of the channels, and not <c>(R+G+B)/3</c>: the eye is roughly seven
        /// times more sensitive to green than to blue, so a saturated blue and a saturated green of
        /// the same "value" are nowhere near the same brightness. Getting this wrong is how a
        /// palette ends up with a colour that measures fine and reads as invisible.
        /// </summary>
        public double Luminance
        {
            get { return 0.2126 * Channel(R) + 0.7152 * Channel(G) + 0.0722 * Channel(B); }
        }

        /// <summary>
        /// How strongly this colour reads against another: 1 when they are the same, 21 for black
        /// on white. Text wants 4.5; something read as a picture — a mark, an icon, a rule — wants
        /// 3.0.
        ///
        /// ⚠ Here rather than in each product because it settles SHARED questions: which of two
        /// greys a dimmed mark takes (<see cref="Theme.MarkDim"/>) has to be answered the same way
        /// in the game and in the window, and a check that re-derives it proves nothing if each
        /// consumer measures differently.
        /// </summary>
        public double Contrast(Rgb other)
        {
            double a = Luminance;
            double b = other.Luminance;
            double high = a > b ? a : b;
            double low = a > b ? b : a;

            return (high + 0.05) / (low + 0.05);
        }

        private static double Channel(byte value)
        {
            double c = value / 255.0;

            return c <= 0.03928 ? c / 12.92 : System.Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        private static byte Mix(byte under, byte over, double strength)
        {
            // Rounded, not truncated: truncation biases every channel downwards, which darkens a
            // blend by up to a unit per channel — visible once a dozen of them sit side by side.
            double value = under + (over - under) * strength;
            return (byte)(value + 0.5);
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

        // ── The lit scope mark ────────────────────────────────────────────────────────────────

        /// <summary>
        /// The one of the three scope marks an action aims at. cyan-200.
        ///
        /// 🔴 **It may never be a colour a button is filled with, and that is the whole reason it
        /// exists.** The lit mark was AccentSoft, a light purple — on a purple-600 button it scored
        /// 1.98 against the background while the two DIMMED marks scored 2.13. The control said the
        /// opposite of what it meant, and worse on hover (1.48 against 1.58). Measured, not judged.
        ///
        /// ⚠ The failure was structural, not a bad shade: a fixed hue taken from the accent family
        /// is guaranteed to collapse the day it lands on an accent fill, and buttons are exactly
        /// where the compact form lives. So the requirement is stated rather than the colour: this
        /// value must stay outside every family a button is filled with — the purples (primary),
        /// the greys (secondary and the surfaces), and the status ramp (warning, danger, play).
        ///
        /// ⚠ Cyan is what is left, and it is free: the site uses blue for links and information
        /// (forty-eight times), green/amber/red for status, and cyan nowhere a user can reach.
        /// At this step it clears 3.0 — the threshold for something read as a picture rather than
        /// as text — on every one of those fills, and stays about twice <see cref="TextMuted"/>,
        /// which is what makes the lit one legible AS the lit one.
        ///
        /// ⚠ **Opacity was the other candidate and is deliberately NOT used**: fading already means
        /// "you cannot press this". Saying "not chosen" the same way would leave one signal doing
        /// two jobs, and a disabled button would become unreadable in both.
        /// </summary>
        public static readonly Rgb MarkLit = new Rgb(0xA5F3FC);

        /// <summary>The dimmed scope marks on a dark fill — the ordinary case. gray-400.</summary>
        public static readonly Rgb MarkDimOnDark = TextMuted;

        /// <summary>The same, on a fill too light to carry a pale grey. gray-900.</summary>
        public static readonly Rgb MarkDimOnLight = SurfaceDeep;

        /// <summary>
        /// The two scope marks an action is NOT aiming at, on a given fill.
        ///
        /// 🔴 **A dimmed mark is quiet, not absent.** It was a fixed gray-400 whatever it sat on,
        /// which reads 3.96 on a grey button and **2.13 on the primary purple** — under the 3.0 a
        /// small picture needs. And it cannot be fixed by lightening: on that fill the LIT mark
        /// itself tops out at 4.44, so pushing the dimmed ones to 3.76 closes the gap between them
        /// from x2.08 to x1.18 and the control stops saying which side it aims at. Measured.
        ///
        /// ⚠ **Answered by direction, not by strength.** On a light fill the dimmed marks go DARKER
        /// than the fill while the lit one stays lighter: the two then differ in which way they
        /// depart from the background, which no amount of crowding can flatten. Same trap
        /// <see cref="MarkLit"/> was moved out of the accent family for, one step further.
        ///
        /// ⚠ No lightness threshold anywhere: this simply keeps whichever of the two reads better
        /// against the fill it is given. A number would have to be re-judged for every fill added
        /// later — and the status fills (amber .50, green .42) are lighter than the purple that
        /// started this.
        /// </summary>
        public static Rgb MarkDim(Rgb fill)
        {
            return fill.Contrast(MarkDimOnDark) >= fill.Contrast(MarkDimOnLight)
                ? MarkDimOnDark
                : MarkDimOnLight;
        }

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

        // ── Tag chips: the letter on its coloured square ───────────────────────────────────────
        //
        // 🔴 **A DIFFERENT RAMP FROM THE BANDS ABOVE, and that is on purpose.** The bands are wide
        // filled areas where 500 reads well; a chip is six pixels of white type on a square, and
        // 500 behind it is thin. The website has always used 600 there — measured in Chrome on the
        // rendered chips, not read off a class name: #16A34A, #2563EB, #EA580C, #9333EA, #0D9488.
        //
        // ⚠ **Here so that nobody has to go looking for them.** The same five letters are named in
        // the mod, in the Manager and on the website, and until now only the website drew them.
        // Anything wanting to change how a tag looks changes it here, once, and the three follow.

        /// <summary>Written by a person. green-600, the chip ramp.</summary>
        public static readonly Rgb ChipHuman = new Rgb(0x16A34A);

        /// <summary>Read back and confirmed. blue-600.</summary>
        public static readonly Rgb ChipValidated = new Rgb(0x2563EB);

        /// <summary>Machine-translated, nobody has read it. orange-600.</summary>
        public static readonly Rgb ChipAi = new Rgb(0xEA580C);

        /// <summary>Kept as is on purpose. purple-600.</summary>
        public static readonly Rgb ChipKept = new Rgb(0x9333EA);

        /// <summary>The mod's own interface. teal-600.</summary>
        public static readonly Rgb ChipModUi = new Rgb(0x0D9488);

        /// <summary>The letters, always white: every one of the five is dark enough to carry it.</summary>
        public static readonly Rgb ChipLetter = new Rgb(0xFFFFFF);

        /// <summary>Corner radius of a chip, in pixels — 0.25rem on the website.</summary>
        public const int ChipRadius = 4;

        /// <summary>
        /// The background a tag is drawn on. Anything not one of the five gets the capture grey,
        /// which is what an unclassified line already is everywhere else.
        /// </summary>
        public static Rgb ChipBackground(string tag)
        {
            switch (tag)
            {
                case "H": return ChipHuman;
                case "V": return ChipValidated;
                case "A": return ChipAi;
                case "S": return ChipKept;
                case "M": return ChipModUi;
                default: return QualityCapture;
            }
        }

        // ── States, computed once ─────────────────────────────────────────────────────────────
        //
        // The website draws these as a translucent layer; neither consumer can (see Rgb.Over). So
        // they are flattened here rather than in each product, because two windows disagreeing
        // about what "selected" looks like is the same failure as two palettes disagreeing.
        //
        // ⚠ These initialise from the values above, so they must stay BELOW them: a static readonly
        // field runs in the order it is written, and moving one of these up would silently blend
        // against black.

        /// <summary>A chosen row: the deep purple over the card, dark enough to keep text legible.</summary>
        public static readonly Rgb RowSelected = AccentDim.Over(SurfaceCard, 0.65);

        /// <summary>Related to the chosen one — same family, said more quietly.</summary>
        public static readonly Rgb RowRelated = AccentDim.Over(SurfaceCard, 0.45);

        /// <summary>How strongly a callout is tinted. Named because it is a judgement, not a fact.</summary>
        private const double CalloutTint = 0.16;

        public static readonly Rgb CalloutSuccess = StatusSuccess.Over(SurfaceDeep, CalloutTint);
        public static readonly Rgb CalloutWarning = StatusWarning.Over(SurfaceDeep, CalloutTint);
        public static readonly Rgb CalloutError = StatusError.Over(SurfaceDeep, CalloutTint);
        public static readonly Rgb CalloutInfo = StatusInfo.Over(SurfaceDeep, CalloutTint);
    }
}
