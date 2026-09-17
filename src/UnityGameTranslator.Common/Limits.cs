namespace UnityGameTranslator.Common
{
    /// <summary>
    /// The sizes the ecosystem agrees on — stated once, read by the mod and the Manager.
    ///
    /// 🔴 **A limit that exists in three places is three limits.** The largest translation file
    /// the site accepts was written as 100 MB in the site's gzip middleware, again in its upload
    /// rule, again in the mod's download and upload guards, and the Manager had none. The day the
    /// site raises its cap, the mod refuses to download what the site accepts, and nobody traces
    /// that refusal back to a constant in another repository. The site cannot read C#, so its
    /// copies stay — but `check-limits.py` at the root compares them to this file and fails when
    /// they diverge, which is the one thing that keeps three numbers being one.
    ///
    /// ⚠ Pure data: no logic, no derived value. Anything that decides something from these numbers
    /// belongs beside the decision, not here.
    /// </summary>
    public static class Limits
    {
        /// <summary>
        /// The most a translation file may weigh, decompressed: what the site accepts on upload,
        /// what it serves on download, and therefore the most either program ever reads back.
        ///
        /// Sized from the largest known real file (about 40 MB of JSON) with room to spare, and
        /// mirrored by the site in <c>DecodeGzipRequest::MAX_DECOMPRESSED_SIZE</c> and the upload
        /// rules — see <c>check-limits.py</c>.
        ///
        /// 🔴 **Bounded by what the server can HOLD, not by what files weigh.** It was 100 MB, a
        /// promise the site could not keep: accepting a file costs PHP about 5.2 times its size
        /// (inflate, decode the body, parse the content — measured 2026-09-17, 60 MB → 328 MB,
        /// 90 MB → 468 MB above the framework), under a 512 MB memory limit. Past ~70 MB the
        /// answer was a 500, never the "too large" this figure lets a client say beforehand. 64 MB
        /// is 1.6× the largest known file and is held with margin. Raising it means raising the
        /// server's memory first.
        /// </summary>
        public const long TranslationFileBytes = 64L * 1024 * 1024;
    }
}
