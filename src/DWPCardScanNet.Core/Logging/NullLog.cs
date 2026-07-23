namespace DWPCardScanNet.Core.Logging
{
    /// <summary>An <see cref="ILog"/> that discards everything. Useful in tests and headless code paths.</summary>
    public sealed class NullLog : ILog
    {
        public static readonly NullLog Instance = new NullLog();

        private NullLog() { }

        public void Write(string message) { }
    }
}
