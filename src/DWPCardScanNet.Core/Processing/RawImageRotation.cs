using System;

namespace DWPCardScanNet.Core.Processing
{
    /// <summary>
    /// Rotates a tightly-packed 8-bit-per-pixel grayscale image (row-major, no row padding)
    /// by 90, 180 or 270 degrees CLOCKWISE. Faithful port of VB6 frmMain.RotateRawImage8bpp.
    /// Right-angle rotations are an exact pixel remap - no resampling, no quality loss.
    /// </summary>
    public static class RawImageRotation
    {
        /// <summary>
        /// Returns the rotated pixel buffer. <paramref name="rotation"/> must be 90, 180 or 270.
        /// On 90/270 the height and width are swapped (reported via the out params).
        /// </summary>
        public static byte[] Rotate(byte[] src, int height, int width, int rotation,
                                    out int newHeight, out int newWidth)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            int total = height * width;
            if (total <= 0)
                throw new ArgumentException("RawImageRotation: invalid image dimensions (" +
                                            height + "x" + width + ").");

            // Guard against a buffer that is not exactly Height*Width (e.g. unexpected row
            // padding): rotating it with these dimensions would scramble the image.
            if (src.Length != total)
                throw new ArgumentException("RawImageRotation: buffer size " + src.Length +
                                            " <> Height*Width " + total + "; not rotating.");

            byte[] dst = new byte[total];

            switch (rotation)
            {
                case 90: // clockwise
                    newHeight = width;
                    newWidth = height;
                    for (int r = 0; r < height; r++)
                        for (int c = 0; c < width; c++)
                            dst[c * newWidth + (height - 1 - r)] = src[r * width + c];
                    break;

                case 180:
                    newHeight = height;
                    newWidth = width;
                    for (int r = 0; r < height; r++)
                        for (int c = 0; c < width; c++)
                            dst[(height - 1 - r) * width + (width - 1 - c)] = src[r * width + c];
                    break;

                case 270: // clockwise (i.e. 90 counter-clockwise)
                    newHeight = width;
                    newWidth = height;
                    for (int r = 0; r < height; r++)
                        for (int c = 0; c < width; c++)
                            dst[(width - 1 - c) * newWidth + r] = src[r * width + c];
                    break;

                default:
                    throw new ArgumentException("RawImageRotation: unsupported rotation " +
                                                rotation + " (expected 90/180/270).");
            }

            return dst;
        }
    }
}
