using System;
using System.IO;

namespace DWPCardScanNet.Core.Logging
{
    /// <summary>
    /// File-based diagnostic log. Faithful port of VB6 frmMain.WriteToLog:
    /// <list type="bullet">
    /// <item>Each entry is written as "<c>{DateTime.Now}: {message}</c>" followed by a newline
    /// (matches the VB6 "<c>Now &amp; ": " &amp; Message</c>" + WriteLine).</item>
    /// <item>Before writing, if the file already exceeds <see cref="MaxBytes"/> it is deleted,
    /// so the log starts fresh (the VB6 used a hard 10,000,000-byte threshold).</item>
    /// <item>The file (and, unlike the VB6, its directory) is created on demand.</item>
    /// </list>
    /// <para>
    /// Deviations from the VB6, all deliberate and safe:
    /// (1) <see cref="Write"/> never throws - the VB6 WriteToLog had no error handler and would
    /// crash the app if the log file was locked or its folder missing; here IO failures are
    /// swallowed and surfaced via <see cref="LastError"/>.
    /// (2) The parent directory is created if absent, rather than failing.
    /// (3) Writes are serialised with a lock so timer/DoEvents-driven callers can't interleave.
    /// </para>
    /// </summary>
    public sealed class FileLog : ILog
    {
        /// <summary>The VB6 rotation threshold: 10,000,000 bytes.</summary>
        public const long DefaultMaxBytes = 10000000L;

        private readonly object _gate = new object();
        private readonly long _maxBytes;

        /// <summary>Full path to the log file being written.</summary>
        public string FilePath { get; }

        /// <summary>Maximum size (bytes) before the log is wiped on the next write.</summary>
        public long MaxBytes { get { return _maxBytes; } }

        /// <summary>Message from the most recent write failure, or null if the last write succeeded.</summary>
        public string LastError { get; private set; }

        public FileLog(string filePath, long maxBytes = DefaultMaxBytes)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("Log file path is required.", nameof(filePath));
            FilePath = filePath;
            _maxBytes = maxBytes;
        }

        public void Write(string message)
        {
            lock (_gate)
            {
                try
                {
                    var info = new FileInfo(FilePath);

                    // Crude rotation: over the size cap => start over (matches VB6 f.Delete()).
                    if (info.Exists && info.Length > _maxBytes)
                        info.Delete();

                    string dir = Path.GetDirectoryName(FilePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    File.AppendAllText(FilePath, DateTime.Now + ": " + message + Environment.NewLine);
                    LastError = null;
                }
                catch (Exception ex)
                {
                    // Logging must never take the app down.
                    LastError = ex.Message;
                }
            }
        }

        /// <summary>
        /// Resolves the log file path the way VB6 frmMain.Form_Load did:
        /// an empty <paramref name="logFileDir"/> logs to "<c>{baseDirectory}\DWPCardScan.log</c>";
        /// otherwise the file "DWPCardScan.log" is placed inside <paramref name="logFileDir"/>
        /// (which already carries a trailing backslash once it has passed through
        /// <c>ConfigReader.LoadDefaults</c>).
        /// </summary>
        public static string ResolveDefaultPath(string logFileDir, string baseDirectory)
        {
            const string logFileName = "DWPCardScan.log";
            if (string.IsNullOrEmpty(logFileDir))
                return Path.Combine(baseDirectory ?? string.Empty, logFileName);
            return logFileDir + logFileName;
        }
    }
}
