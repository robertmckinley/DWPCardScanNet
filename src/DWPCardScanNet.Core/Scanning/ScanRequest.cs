namespace DWPCardScanNet.Core.Scanning
{
    /// <summary>
    /// Inputs for a scan operation. Replaces the property-bag that VB6 frmScan pushed into
    /// DWPAwareScan_Net (ScannerType / ToProcessDir / CardHeight / CardWidth / UseADF / UseDuplex).
    /// </summary>
    public sealed class ScanRequest
    {
        /// <summary>DWPCardScan scanner-type code (see the .ini "ScannerType" table; 10 = Epson 10000XL).</summary>
        public int ScannerType { get; set; } = 10;

        /// <summary>Folder the scanned BMP (and thumbnail JPG) files are written to.</summary>
        public string WorkingDirectory { get; set; } = "";

        public float CardWidth { get; set; } = 8f;
        public float CardHeight { get; set; } = 8f;

        /// <summary>Use the automatic document feeder (only honoured for Epson 10000XL/11000XL).</summary>
        public bool UseAdf { get; set; }

        /// <summary>Scan both sides (only honoured for Epson 10000XL/11000XL).</summary>
        public bool UseDuplex { get; set; }
    }
}
