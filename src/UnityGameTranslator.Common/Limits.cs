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
        /// </summary>
        public const long TranslationFileBytes = 100L * 1024 * 1024;
    }
}
