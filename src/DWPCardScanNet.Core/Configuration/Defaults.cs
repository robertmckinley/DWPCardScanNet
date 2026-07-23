namespace DWPCardScanNet.Core.Configuration
{
    /// <summary>
    /// Application-wide default settings ([DefaultSettings] section). Ported from VB6 clsDefaults.
    /// Property names, types and initial values mirror the original class exactly.
    /// </summary>
    public sealed class Defaults
    {
        public string Source_ORI { get; set; } = "";
        public string NISTOutputDir { get; set; } = "";
        public string DefaultCard { get; set; } = "";
        public int ScannerType { get; set; } = 0;
        public string GroupsToProcessFolder { get; set; } = "";
        public string ToProcessQueue { get; set; } = "";
        public string ProcessedQueue { get; set; } = "";
        public int SaveProcessedCards { get; set; } = 1;
        public string INIFileName { get; set; } = "";
        public string KeyFieldTag { get; set; } = "";
        public string ProblemQueue { get; set; } = "";
        public string GroupStatusReportFolder { get; set; } = "";
        public string LogFileDir { get; set; } = "";

        /// <summary>"8X8" or "8.5X11".</summary>
        public string DefaultCardSize { get; set; } = "8X8";

        /// <summary>"Duplex" or "Single".</summary>
        public string DefaultDuplex { get; set; } = "Single";

        public int DupKeyEntry { get; set; } = 0;
        public int UseADF { get; set; } = 1;
        public int DisplayGroupStatusReport { get; set; } = 1;

        /// <summary>If populated, the app shells out to this scanning executable instead of scanning in-process.</summary>
        public string ScanShellFilePath { get; set; } = "";

        public string BannerText { get; set; } = "";
        public string BannerFontColor { get; set; } = "";
        public string FormCaption { get; set; } = "DWP Card Scan";
        public string ProcessMessage { get; set; } = "";

        // Save all scanned cards in a group to a single PDF.
        public string SaveGroupPDF { get; set; } = "N"; // "Y" or "N"
        public string GroupPDFFolder { get; set; } = "";
        public string PDFFileNameKeyTag { get; set; } = "";

        // Remember the last card-type selection and pre-select it for the next card.
        public string RememberCardType { get; set; } = "N"; // "Y" or "N"
    }
}
