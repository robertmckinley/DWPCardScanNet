namespace DWPCardScanNet.Core.Scanning
{
    /// <summary>
    /// Acquires card images from a scanner into a working folder. The concrete implementation
    /// (Aware AccuScan) lives in the DWPCardScanNet.Aware project so that Core stays free of any
    /// vendor/interop dependency.
    /// </summary>
    public interface IScanner
    {
        ScanResult Scan(ScanRequest request);
    }
}
