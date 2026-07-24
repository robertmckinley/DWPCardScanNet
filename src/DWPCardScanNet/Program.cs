using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DWPCardScanNet
{
    internal static class Program
    {
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int AddDllDirectory(string newDirectory);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool SetDefaultDllDirectories(int directoryFlags);

        private const int LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;

        [STAThread]
        private static void Main()
        {
            EnsureAwareNativeDllsResolvable();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        /// <summary>
        /// The managed Aware assemblies load native DLLs (accuscan/nistpack/wsq). accuscan is in
        /// SysWOW64 but the NISTPack natives are only under the Aware install, so add those folders
        /// to the process DLL search path. Silently ignored if the Aware install isn't present.
        /// <para>
        /// Requires Aware AccuScan version 4.2.1. The test machine threw errors until 4.2.1 was
        /// installed; earlier versions are not supported.
        /// </para>
        /// </summary>
        private static void EnsureAwareNativeDllsResolvable()
        {
            string[] candidates =
            {
                @"C:\Program Files (x86)\Aware\NISTPack\redist",
                @"C:\Program Files (x86)\Aware\AccuScan\redist",
            };

            try { SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS); } catch { }
            foreach (string dir in candidates)
            {
                if (!Directory.Exists(dir)) continue;
                try { AddDllDirectory(dir); } catch { }
                // Also prepend to PATH as a fallback for older loaders.
                Environment.SetEnvironmentVariable("PATH", dir + ";" + Environment.GetEnvironmentVariable("PATH"));
            }
        }
    }
}
