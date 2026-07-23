using System.Collections.Generic;

namespace DWPCardScanNet.Core.Scanning
{
    /// <summary>
    /// Outcome of a scan operation. The VB6/COM code returned an int (0/1) plus a LastError
    /// string and left the caller to re-enumerate the folder; here we return success plus the
    /// list of files produced (still written to <see cref="ScanRequest.WorkingDirectory"/>).
    /// </summary>
    public sealed class ScanResult
    {
        public bool Success { get; }

        /// <summary>Error text when <see cref="Success"/> is false; otherwise null.</summary>
        public string ErrorMessage { get; }

        /// <summary>Full paths of the BMP files produced, in scan order.</summary>
        public IReadOnlyList<string> BmpFilePaths { get; }

        private ScanResult(bool success, string errorMessage, IReadOnlyList<string> bmpFilePaths)
        {
            Success = success;
            ErrorMessage = errorMessage;
            BmpFilePaths = bmpFilePaths ?? new List<string>();
        }

        public static ScanResult Ok(IReadOnlyList<string> bmpFilePaths) =>
            new ScanResult(true, null, bmpFilePaths);

        public static ScanResult Fail(string errorMessage, IReadOnlyList<string> partial = null) =>
            new ScanResult(false, errorMessage, partial);
    }
}
