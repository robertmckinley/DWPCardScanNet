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
    /// Each page is sized to the image's natural dimensions (at its DPI), matching a "one card per page"
    /// archival layout.
    /// </para>
    /// </summary>
    public sealed class PdfSharpWriter : IPdfWriter
    {
        private const long JpegQuality = 85L;

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
