using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Aware.AccuScan;
using DWPCardScanNet.Core.Scanning;
using ErrCode = Aware.AccuScan.ErrorInfo.errorCode;

namespace DWPCardScanNet.Aware
{
    /// <summary>
    /// Scans cards using the managed Aware AccuScan API (<see cref="awAccuScan"/>).
    /// Port of the VB6-era DWPAwareScan_Net.clsDWPAwareScan_Net.ScanCard, adapted to the
    /// <see cref="IScanner"/> contract. Behaviour is preserved: files are written to the
    /// working directory as NNNN_Front.BMP / NNNN_Back.BMP with a matching .JPG thumbnail,
    /// ADF paging continues until the feeder reports empty, duplex backs are rotated 180.
    /// </summary>
    public sealed class AwareScanner : IScanner
    {
        // The single crop region / page the scanner config uses (the whole card).
        private const int CropIndex = 1;
        private const int Page = 1;
        private const int ScanResolutionDpi = 500;
        private const int ThumbHeightPx = 200;

        public ScanResult Scan(ScanRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            string workingDir = EnsureTrailingSlash(request.WorkingDirectory);
            if (!Directory.Exists(workingDir))
                return ScanResult.Fail("Folder: " + workingDir + " not found.");

            bool useAdf = request.UseAdf;
            bool useDuplex = request.UseDuplex;
            // Only the Epson 10000XL (10) and 11000XL (15/11 in the wrapper) support ADF/duplex.
            if (request.ScannerType != 10 && request.ScannerType != 11)
            {
                useAdf = false;
                useDuplex = false;
            }

            var produced = new List<string>();
            awAccuScan scanner = null;
            try
            {
                scanner = new awAccuScan(MapScannerType(request.ScannerType));
                scanner.SetAdfUsage(useAdf);
                scanner.SetDuplexerUsage(useDuplex);
                scanner.SetUnits(0); // inches

                bool done = false;
                int pageCount = 0;
                while (!done)
                {
                    pageCount++;
                    scanner.SetPageArea(0, 0, request.CardHeight, request.CardWidth, 0, ScanResolutionDpi, Page);
                    scanner.SetPageCropRegion(CropIndex, 0, 0, request.CardHeight, request.CardWidth, Page);

                    // In duplex, even pages are the back of a card - rotate 180.
                    bool cardBack = useDuplex && (pageCount % 2 == 0);
                    scanner.SetCropRegionRotation(CropIndex, cardBack ? 180 : 0);

                    ErrCode err;
                    try
                    {
                        err = scanner.AcquirePage(Page);
                    }
                    catch (Exception ex)
                    {
                        // Aware reports an empty ADF as AwScanAcquirePage error 60 or 67.
                        if (ex.Message.Contains("AwScanAcquirePage: 60") ||
                            ex.Message.Contains("AwScanAcquirePage: 67"))
                        {
                            break; // feeder empty - normal end of an ADF run
                        }
                        return ScanResult.Fail(ex.Message, produced);
                    }

                    if (err != ErrCode.AWSCAN_NO_ERRORS)
                        return ScanResult.Fail(err.ToString(), produced);

                    string bmpPath = GetNextBmpFileName(workingDir, cardBack);
                    scanner.GetCropRegionImageFile(CropIndex, "bmp", bmpPath); // proper 500dpi BMP
                    produced.Add(bmpPath);

                    WriteThumbnail(bmpPath);

                    // No ADF => a single scan only.
                    if (request.ScannerType != 10 && request.ScannerType != 11)
                        done = true;
                }

                return ScanResult.Ok(produced);
            }
            catch (Exception ex)
            {
                return ScanResult.Fail(ex.Message, produced);
            }
            finally
            {
                if (scanner != null) scanner.Dispose();
            }
        }

        /// <summary>
        /// Produces the next sequential card filename in the working directory:
        /// fronts increment the 4-digit number, backs reuse the current highest number.
        /// Ported from clsDWPAwareScan_Net.GetNextBMPFileName.
        /// </summary>
        private static string GetNextBmpFileName(string workingDir, bool cardBack)
        {
            string cardDesc = cardBack ? "Back" : "Front";

            string[] files = Directory.GetFiles(workingDir);
            if (files.Length == 0)
                return Path.Combine(workingDir, "0001_" + cardDesc + ".BMP");

            int highest = 0;
            foreach (string file in files)
            {
                string upper = file.ToUpperInvariant();
                // Only real card BMPs: ignore thumbnails, .crp files, and undo files.
                if (!upper.Contains(".BMP") || upper.Contains("AA_UNDO"))
                    continue;

                int underscore = upper.IndexOf('_');
                if (underscore <= 0) continue;

                string numText = upper.Substring(underscore - 4, 4);
                if (!int.TryParse(numText, out int fileNum))
                    continue; // faithful to VB6 intent: skip names whose prefix isn't numeric

                if (fileNum > highest) highest = fileNum;
            }

            int next = cardBack ? highest : highest + 1; // backs keep the front's number
            return Path.Combine(workingDir, next.ToString("D4") + "_" + cardDesc + ".BMP");
        }

        private static void WriteThumbnail(string bmpPath)
        {
            using (Image full = Image.FromFile(bmpPath))
            {
                double aspect = (double)full.Height / full.Width;
                int thumbWidth = (int)Math.Round(ThumbHeightPx / aspect);
                using (Image thumb = full.GetThumbnailImage(thumbWidth, ThumbHeightPx, null, IntPtr.Zero))
                {
                    string jpgPath = bmpPath.Substring(0, bmpPath.Length - 4) + ".JPG";
                    thumb.Save(jpgPath, ImageFormat.Jpeg);
                }
            }
        }

        private static string EnsureTrailingSlash(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.EndsWith("\\", StringComparison.Ordinal) ? s : s + "\\";
        }

        /// <summary>Maps a DWPCardScan .ini scanner-type code to an Aware scanner enum value.</summary>
        private static awAccuScan.AwareScannerType MapScannerType(int scannerType)
        {
            switch (scannerType)
            {
                case 0: return awAccuScan.AwareScannerType.AW_SCAN_TYPE_MEMORY;
                case 1: return awAccuScan.AwareScannerType.AW_SCAN_TYPE_HP6300C;
                case 2: return awAccuScan.AwareScannerType.AW_SCAN_TYPE_1640SU;
                case 3: return awAccuScan.AwareScannerType.AW_SCAN_TYPE_1640XL;
                case 4: return awAccuScan.AwareScannerType.AW_SCAN_TYPE_PERF1650;
                case 5: return awAccuScan.AwareScannerType.AW_SCAN_TYPE_PERF2450;
                case 6: return awAccuScan.AwareScannerType.AW_SCAN_TYPE_PERF3200;
                case 7: return awAccuScan.AwareScannerType.AW_SCAN_TYPE_PERF3170;
                case 8: return awAccuScan.AwareScannerType.AW_SCAN_TYPE_PERF4870;
                case 9: return awAccuScan.AwareScannerType.AW_SCAN_TYPE_PERF4180;
                case 10: return awAccuScan.AwareScannerType.AW_SCAN_TYPE_10000XL;
                case 11: return awAccuScan.AwareScannerType.AW_SCAN_TYPE_PERF4990;
                case 12: return awAccuScan.AwareScannerType.AW_SCAN_TYPE_PERF4490;
                case 13: return awAccuScan.AwareScannerType.AW_SCAN_TYPE_PERFV700;
                case 14: return awAccuScan.AwareScannerType.AW_SCAN_TYPE_PERFV500;
                case 15: return awAccuScan.AwareScannerType.AW_SCANNER_TYPE_11000XL;
                case 16: return awAccuScan.AwareScannerType.AW_SCAN_TYPE_PERFV550;
                case 17: return awAccuScan.AwareScannerType.AW_SCAN_TYPE_PERFV800;
                default: return awAccuScan.AwareScannerType.AW_SCAN_TYPE_10000XL;
            }
        }
    }
}
