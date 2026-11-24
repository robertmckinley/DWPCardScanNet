using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using DWPCardScanNet.Core.Processing;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace DWPCardScanNet
{
    /// <summary>
    /// <see cref="IPdfWriter"/> backed by PDFsharp (MIT-licensed, no cost) - the managed replacement
    /// for the VB6 MUGPDF component. Archives each scanned card image as one PDF page.
    /// <para>
    /// The scanned cards are large 8-bpp BMPs (~16 MB each at 500 DPI). Embedding them verbatim would
    /// produce enormous PDFs, so each is re-encoded to a JPEG in memory before it is placed on a page.
    /// Each page is sized to the image at <see cref="ScanDpi"/> (4000 px = 8"), so it prints at actual size.
    /// </para>
    /// </summary>
    public sealed class PdfSharpWriter : IPdfWriter
    {
        // Fingerprint cards: keep full detail. GDI+ quality 100 (quantization 1 at every frequency) is the
        // only setting at least as fine as the VB6/MUGPDF PDFs (LEAD QFactor 2 = quantization 2 throughout);
        // 98 and below quantize fine ridge detail more coarsely. ~14 MB per two-sided card.
        private const long JpegQuality = 100L;

        // Scan resolution. Forced onto each image so the page size never depends on the BMP header:
        // a BMP without DPI would otherwise default to 96 dpi and give a ~41.7" page.
        private const float ScanDpi = 500f;

        public void CreatePdf(IReadOnlyList<string> imagePaths, string pdfPath, string title)
        {
            if (imagePaths == null) throw new ArgumentNullException(nameof(imagePaths));
            if (string.IsNullOrEmpty(pdfPath)) throw new ArgumentException("pdfPath is required", nameof(pdfPath));

            string dir = Path.GetDirectoryName(pdfPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            using (var doc = new PdfDocument())
            {
                doc.Info.Title = title ?? "";

                foreach (string imagePath in imagePaths)
                {
                    if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath)) continue;

                    // Re-encode to JPEG in memory to keep the PDF small. The stream must stay open
                    // until the page is drawn, so scope it per page.
                    using (MemoryStream jpeg = ReencodeToJpeg(imagePath))
                    using (XImage image = XImage.FromStream(jpeg))
                    {
                        PdfPage page = doc.AddPage();
                        page.Width = XUnit.FromPoint(image.PointWidth);
                        page.Height = XUnit.FromPoint(image.PointHeight);
                        using (XGraphics gfx = XGraphics.FromPdfPage(page))
                            gfx.DrawImage(image, 0, 0, page.Width.Point, page.Height.Point);
                    }
                }

                // A PDF with no pages is invalid; add a blank page rather than throwing.
                if (doc.PageCount == 0) doc.AddPage();

                doc.Save(pdfPath);
            }
        }

        private static MemoryStream ReencodeToJpeg(string imagePath)
        {
            var ms = new MemoryStream();
            using (var bmp = new Bitmap(imagePath))
            {
                // Written into the JPEG header; PDFsharp sizes the page from it (PointWidth = px * 72 / dpi).
                bmp.SetResolution(ScanDpi, ScanDpi);

                ImageCodecInfo jpegCodec = GetEncoder(ImageFormat.Jpeg);
                if (jpegCodec != null)
                {
                    using (var p = new EncoderParameters(1))
                    {
                        p.Param[0] = new EncoderParameter(Encoder.Quality, JpegQuality);
                        bmp.Save(ms, jpegCodec, p);
                    }
                }
                else
                {
                    bmp.Save(ms, ImageFormat.Jpeg);
                }
            }
            ms.Position = 0;
            return ms;
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            foreach (ImageCodecInfo codec in ImageCodecInfo.GetImageEncoders())
                if (codec.FormatID == format.Guid) return codec;
            return null;
        }
    }
}
