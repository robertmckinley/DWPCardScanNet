using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using DWPCardScanNet.Core.Configuration;

namespace DWPCardScanNet.Core.Grouping
{
    /// <summary>
    /// Reads/writes the per-card ".crp" crop-region override file. Port of VB6
    /// SaveCropRegionFile / WriteCropRegion. One line per region:
    /// <c>FingerIndex,Desc,Top,Left,Height,Width</c>. GroupProcessor reads it and overrides each
    /// region's Top/Left by FingerIndex (the operator can only move regions, not resize them).
    /// </summary>
    public static class CropRegionFile
    {
        public static void Write(string crpPath, IEnumerable<CropRegion> regions)
        {
            var sb = new StringBuilder();
            foreach (CropRegion r in regions)
            {
                sb.Append(r.FingerIndex.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(r.Desc ?? "").Append(',')
                  .Append(F(r.Top)).Append(',')
                  .Append(F(r.Left)).Append(',')
                  .Append(F(r.Height)).Append(',')
                  .Append(F(r.Width))
                  .Append("\r\n");
            }
            File.WriteAllText(crpPath, sb.ToString());
        }

        private static string F(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
