using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace DWPCardScanNet.Aware
{
    /// <summary>
    /// GDI+ helpers for the image plumbing that the VB6 did with LEADTOOLS: decoding an Aware
    /// crop-region BMP to raw 8-bpp grayscale pixels, and rotating a whole-card BMP file
    /// (the RotateBMP replacement). No LEADTOOLS.
    /// </summary>
    internal static class BmpCodec
    {
        /// <summary>
        /// Decodes BMP bytes to a tightly-packed (no row padding) top-down 8-bpp grayscale
        /// buffer. 8-bpp indexed grayscale (what Aware returns for a card) is copied directly;
        /// anything else is reduced to luminance as a safety fallback.
        /// </summary>
        public static byte[] DecodeTo8bppRaw(byte[] bmpBytes, out int height, out int width)
        {
            using (var ms = new MemoryStream(bmpBytes))
            using (var bmp = new Bitmap(ms))
            {
                width = bmp.Width;
                height = bmp.Height;
                var rect = new Rectangle(0, 0, width, height);
                byte[] raw = new byte[width * height];

                if (bmp.PixelFormat == PixelFormat.Format8bppIndexed)
                {
                    BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);
                    try
                    {
                        for (int y = 0; y < height; y++)
                            Marshal.Copy(data.Scan0 + y * data.Stride, raw, y * width, width);
                    }
                    finally { bmp.UnlockBits(data); }
                }
                else
                {
                    BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        byte[] row = new byte[width * 4];
                        for (int y = 0; y < height; y++)
                        {
                            Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, width * 4);
                            for (int x = 0; x < width; x++)
                            {
                                byte b = row[x * 4], g = row[x * 4 + 1], r = row[x * 4 + 2];
                                raw[y * width + x] = (byte)((r * 30 + g * 59 + b * 11) / 100);
                            }
                        }
                    }
                    finally { bmp.UnlockBits(data); }
                }

                return raw;
            }
        }

        public static void ReadDimensions(byte[] bmpBytes, out int height, out int width)
        {
            using (var ms = new MemoryStream(bmpBytes))
            using (var bmp = new Bitmap(ms))
            {
                width = bmp.Width;
                height = bmp.Height;
            }
        }

        /// <summary>Rotates a BMP file in place by 90/180/270 degrees clockwise (lossless). Replaces VB6 RotateBMP.</summary>
        public static void RotateFileInPlace(string path, int degrees)
        {
            RotateFlipType rft;
            switch (degrees)
            {
                case 90: rft = RotateFlipType.Rotate90FlipNone; break;
                case 180: rft = RotateFlipType.Rotate180FlipNone; break;
                case 270: rft = RotateFlipType.Rotate270FlipNone; break;
                default: return;
            }

            // Read the bytes first so the file is not left locked while we rewrite it.
            byte[] bytes = File.ReadAllBytes(path);
            using (var ms = new MemoryStream(bytes))
            using (var bmp = new Bitmap(ms))
            {
                bmp.RotateFlip(rft);
                bmp.Save(path, ImageFormat.Bmp);
            }
        }
    }
}
