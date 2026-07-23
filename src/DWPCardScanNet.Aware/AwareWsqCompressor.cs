using Aware.AwWsq1000;
using DWPCardScanNet.Core.Processing;

namespace DWPCardScanNet.Aware
{
    /// <summary>
    /// WSQ compression via the managed <see cref="AwWsq"/>. Replaces VB6
    /// AwWsqCtrl.WsqCompressRaw(image, height, width, 10, 0, 255).
    /// <para>
    /// ASSUMPTION (validate against VB6 output): the VB6 trailing args map to
    /// SetCompressWsqRatio(10) (a 10:1 compression ratio) and SetCompressWsqScannerValues(0, 255)
    /// (scanner black/white). Input is raw 8-bpp at 500 ppi. If golden-file WSQ payloads differ,
    /// this mapping (ratio vs bitrate, tolerance, resolution) is the first thing to revisit.
    /// </para>
    /// </summary>
    public sealed class AwareWsqCompressor : IWsqCompressor
    {
        private const float ScanResolutionDpi = 500f;
        private const int Bpp = 8;
        // The VB6 AwWsqCtrl.WsqCompressRaw(image, h, w, 10, 0, 255) maps to a WSQ *quality factor*
        // of 10 with scanner black/white of 0/255 - NOT a compression ratio. Verified by
        // reverse-engineering the golden packet: fixed-ratio produced constant-size output, but the
        // golden's sizes vary with content and SetCompressWsqFactor(10) reproduces the golden sizes.
        private const float QualityFactor = 10f;
        private const int ScannerBlack = 0;
        private const int ScannerWhite = 255;

        public byte[] CompressRaw(byte[] rawImage, int height, int width)
        {
            using (var wsq = new AwWsq())
            {
                wsq.SetInputImageRaw(rawImage, height, width, ScanResolutionDpi, Bpp);
                wsq.SetCompressWsqFactor(QualityFactor);
                wsq.SetCompressWsqScannerValues(ScannerBlack, ScannerWhite);
                return wsq.GetOutputImage(AwWsqImageFormat.WSQ);
            }
        }
    }
}
