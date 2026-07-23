namespace DWPCardScanNet.Core.Logging
{
    /// <summary>
    /// Minimal diagnostic logging abstraction. Replaces the VB6 frmMain.WriteToLog helper.
    /// Kept tiny on purpose: one call site pattern (a formatted message line), swappable for
    /// tests via <see cref="NullLog"/>. Implementations must never throw from <see cref="Write"/> -
    /// a logging failure must not break the scanning/processing workflow.
    /// </summary>
    public interface ILog
    {
        /// <summary>Appends a single timestamped line to the log.</summary>
        void Write(string message);
    }
}
