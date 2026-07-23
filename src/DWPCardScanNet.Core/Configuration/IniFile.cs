using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DWPCardScanNet.Core.Configuration
{
    /// <summary>
    /// Thin wrapper over the Win32 private-profile (INI) API.
    /// <para>
    /// The original VB6 app (clsConfig) read every setting via
    /// <c>GetPrivateProfileStringA</c> / <c>WritePrivateProfileStringA</c>. We deliberately
    /// call the same Win32 functions rather than writing a managed parser so that the
    /// existing site .ini files (with their quirks - leading spaces on keys such as
    /// " Group1 = CAR", section names containing double spaces like "[CAR  - Front]",
    /// and surrounding-whitespace trimming) load byte-for-byte the way they always have.
    /// </para>
    /// </summary>
    public sealed class IniFile
    {
        // The VB6 code used a 120-char buffer (clsConfig.GetParam). A larger buffer here is
        // strictly safer - it only prevents truncation of long values (e.g. folder paths) and
        // never changes the parsed result for values that fit.
        private const int BufferSize = 1024;

        [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern int GetPrivateProfileString(
            string lpAppName, string lpKeyName, string lpDefault,
            StringBuilder lpReturnedString, int nSize, string lpFileName);

        [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern int WritePrivateProfileString(
            string lpAppName, string lpKeyName, string lpString, string lpFileName);

        /// <summary>Full path to the backing .ini file.</summary>
        public string FilePath { get; }

        public IniFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("INI file path is required.", nameof(filePath));
            FilePath = filePath;
        }

        /// <summary>
        /// Reads a single key from a section. Returns the value, or <paramref name="defaultValue"/>
        /// (default "") when the section/key is absent. Mirrors clsConfig.GetParam.
        /// </summary>
        public string GetString(string section, string key, string defaultValue = "")
        {
            var buffer = new StringBuilder(BufferSize);
            GetPrivateProfileString(section, key, defaultValue ?? string.Empty, buffer, BufferSize, FilePath);
            return buffer.ToString();
        }

        /// <summary>Writes a single key/value to a section (creating them if needed).</summary>
        public void WriteString(string section, string key, string value)
        {
            WritePrivateProfileString(section, key, value, FilePath);
        }
    }
}
