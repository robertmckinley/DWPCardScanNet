namespace DWPCardScanNet.Core.Configuration
{
    /// <summary>
    /// A single crop region on a card. Ported from VB6 clsCropRegion.
    /// <para>
    /// The four LEADTOOLS-specific fields on the original class (LeadTop/LeadLeft/
    /// LeadWidth/LeadHeight) were intentionally dropped: they were vestigial and this
    /// port replaces LEADTOOLS with GDI+. The runtime pixel-size fields (pxHeight/pxWidth,
    /// set during scan processing) and NistRecordIndex are kept.
    /// </para>
    /// </summary>
    public sealed class CropRegion
    {
        /// <summary>Aware finger/palm position code (e.g. 1 = Right Thumb, 15 = data header, 21 = Right Full Palm).</summary>
        public int FingerIndex { get; set; }

        /// <summary>1-based ordinal of this region within its card definition (CropRegion&lt;N&gt;).</summary>
        public int ListIndex { get; set; }

        /// <summary>Assigned when the NIST record for this region is created (runtime, not from INI).</summary>
        public int NistRecordIndex { get; set; }

        // Region geometry, in inches (as stored in the .ini).
        public float Top { get; set; }
        public float Left { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }

        // Pixel dimensions of the extracted region (runtime, filled in during processing).
        public float PxHeight { get; set; }
        public float PxWidth { get; set; }

        /// <summary>Optional description (e.g. "Data Header", "Left Four Fingers").</summary>
        public string Desc { get; set; } = "";

        /// <summary>
        /// Optional clockwise rotation (0, 90, 180 or 270) applied to the cropped image
        /// before it is compressed/stored. 0 = no rotation.
        /// </summary>
        public int Rotation { get; set; }
    }
}
