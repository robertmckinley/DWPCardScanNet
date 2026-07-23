using Aware.AccuScan;
using DWPCardScanNet.Core.Configuration;
using DWPCardScanNet.Core.Processing;

namespace DWPCardScanNet.Aware
{
    /// <summary>
    /// Loads a scanned card into Aware AccuScan memory mode and extracts crop regions.
    /// Replaces the VB6 AwScanControl usage (AwScanBmpToRaw / AwScanLoadCardMem /
    /// AwScanSetCropRegion / AwScanGetCropRegion / AwScanGetCropRegionImage).
    /// <para>
    /// The whole-card BMP is rotated (GDI+) before loading, matching VB6 RotateBMP. Each region
    /// is extracted as a BMP via GetCropRegionImage and (for the raw path) decoded to 8-bpp so the
    /// pixel dimensions come from Aware's own output rather than being recomputed from inches.
    /// Points to confirm during golden-file validation: SetPageArea ordering vs LoadPageImageFile,
    /// and that GetCropRegionImage("bmp") pixels equal the VB6 AwScanGetCropRegion raw pixels.
    /// </para>
    /// </summary>
    public sealed class AwareCardImageExtractor : ICardImageExtractor
    {
        private const int Page = 1;
        private const int CropSlot = 1; // reuse a single crop-region slot per extraction

        private readonly awAccuScan _scanner =
            new awAccuScan(awAccuScan.AwareScannerType.AW_SCAN_TYPE_MEMORY);

        public void LoadCard(string bmpPath, CardArea area, int wholeCardRotationDegrees)
        {
            if (wholeCardRotationDegrees != 0)
                BmpCodec.RotateFileInPlace(bmpPath, wholeCardRotationDegrees);

            _scanner.SetUnits(awAccuScan.AwareMeasurementUnits.AW_UNITS_INCHES);
            _scanner.LoadPageImageFile(bmpPath, awAccuScan.AwareScanningResolution.AW_500PPI, Page);
            _scanner.SetPageArea(area.Top, area.Left, area.Height, area.Width, 0, 500, Page);
        }

        public ExtractedImage GetRegionRaw(CropRegion region)
        {
            byte[] bmp = ExtractRegionBmp(region);
            byte[] raw = BmpCodec.DecodeTo8bppRaw(bmp, out int height, out int width);
            return new ExtractedImage(raw, height, width);
        }

        public ExtractedImage GetRegionBmp(CropRegion region)
        {
            byte[] bmp = ExtractRegionBmp(region);
            BmpCodec.ReadDimensions(bmp, out int height, out int width);
            return new ExtractedImage(bmp, height, width);
        }

        private byte[] ExtractRegionBmp(CropRegion region)
        {
            _scanner.SetPageCropRegion(CropSlot, region.Top, region.Left, region.Height, region.Width, Page);
            return _scanner.GetCropRegionImage(CropSlot, "bmp");
        }

        public void Dispose() => _scanner?.Dispose();
    }

    public sealed class AwareCardImageExtractorFactory : ICardImageExtractorFactory
    {
        public ICardImageExtractor Create() => new AwareCardImageExtractor();
    }
}
