using System.Collections.Generic;
using System.IO;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// Where each system keeps its installed fonts — ONE list, read by every search for a System font
    /// in the mod and by UGT Manager (which must find the file the mod finds, to say which font is used
    /// and to export it).
    ///
    /// 🔴 There were four (audit of 2026-09-28, analyse/audit-os-2026-09-28.md): three in the mod, one in
    /// the Manager, each missing folders another had — fonts installed for one Windows user, a Linux
    /// user's own fonts, the macOS Supplemental folder where most of its fonts live. A font was found by
    /// one search and not by the next.
    ///
    /// ⚠ The OS is the one the MOD sees: a Windows build under Wine (Proton) is on Windows, in its
    /// prefix. The caller says which, and gives the folders it knows; nothing is read from the machine
    /// here, so the answer can be computed for a game's prefix from another system.
    /// </summary>
    public static class SystemFontFolders
    {
        public enum Os { Windows, Linux, MacOs }

        /// <param name="windowsDir">Windows: the Windows folder (C:\Windows, or a prefix's drive_c/windows).</param>
        /// <param name="localAppData">Windows: the user's Local AppData, where per-user fonts go.</param>
        /// <param name="home">Linux and macOS: the user's home.</param>
        /// <param name="xdgDataHome">Linux: $XDG_DATA_HOME when set.</param>
        public static List<string> For(Os os, string? windowsDir = null, string? localAppData = null,
                                       string? home = null, string? xdgDataHome = null)
        {
            var folders = new List<string>();

            switch (os)
            {
                case Os.Windows:
                    if (!string.IsNullOrEmpty(windowsDir)) folders.Add(Path.Combine(windowsDir, "Fonts"));
                    if (!string.IsNullOrEmpty(localAppData))
                        folders.Add(Path.Combine(Path.Combine(Path.Combine(localAppData, "Microsoft"), "Windows"), "Fonts"));
                    break;

                case Os.Linux:
                    folders.Add("/usr/share/fonts");
                    folders.Add("/usr/local/share/fonts");
                    var data = !string.IsNullOrEmpty(xdgDataHome) ? xdgDataHome
                        : !string.IsNullOrEmpty(home) ? home + "/.local/share" : null;
                    if (data != null) folders.Add(data + "/fonts");
                    if (!string.IsNullOrEmpty(home)) folders.Add(home + "/.fonts");
                    break;

                case Os.MacOs:
                    folders.Add("/System/Library/Fonts");
                    folders.Add("/System/Library/Fonts/Supplemental");
                    folders.Add("/Library/Fonts");
                    if (!string.IsNullOrEmpty(home)) folders.Add(home + "/Library/Fonts");
                    break;
            }

            return folders;
        }
    }
}
