using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DWPCardScanNet.Core.Configuration;
using DWPCardScanNet.Core.Logging;

namespace DWPCardScanNet.Core.Processing
{
    /// <summary>
    /// Processes queued card-group folders into NIST/EBTS packets. Port of the VB6
    /// frmMain.cmdProcessGroup_Click + ParseScannedCard + AddType4Rec/AddType15Rec/AddType7Rec.
    /// <para>
    /// The record-building logic (which mnemonics to set, how FingerIndex maps to record type)
    /// lives here in Core and is exercised through the <see cref="INistTransaction"/> /
    /// <see cref="ICardImageExtractor"/> / <see cref="IWsqCompressor"/> abstractions, so it can be
    /// unit-tested with fakes. Only the Aware calls behind those interfaces are vendor-specific.
    /// </para>
    /// <para>
    /// Deliberate deviations from the VB6, all pending golden-file validation:
    /// (1) One bad group is logged and skipped; the run continues (VB6 aborted the whole run).
    /// (2) T4_VLL/T4_HLL are set to the real pixel dimensions - the VB6 left them empty because
    ///     its Height/Width locals were never assigned (a latent bug); Type-15 already used real dims.
    /// (3) PDF output (VB6 MUGPDF) IS ported when an <see cref="IPdfWriter"/> is supplied; if none is
    ///     supplied the PDF tag is still written but the file is skipped (logged).
    /// (4) The PostProcessDLL hook IS ported (managed, no COM) when an <see cref="IPostProcessorLoader"/>
    ///     is supplied; if none is supplied a configured hook is logged and skipped.
    /// </para>
    /// </summary>
    public sealed class GroupProcessor : IGroupProcessor
    {
        private readonly INistTransactionFactory _transactionFactory;
        private readonly ICardImageExtractorFactory _extractorFactory;
        private readonly IWsqCompressor _wsq;
        private readonly IPdfWriter _pdfWriter;
        private readonly IPostProcessorLoader _postProcessorLoader;
        private readonly ILog _log;

        public GroupProcessor(INistTransactionFactory transactionFactory,
                              ICardImageExtractorFactory extractorFactory,
                              IWsqCompressor wsq,
                              IPdfWriter pdfWriter = null,
                              IPostProcessorLoader postProcessorLoader = null,
                              ILog log = null)
        {
            _transactionFactory = transactionFactory ?? throw new ArgumentNullException(nameof(transactionFactory));
            _extractorFactory = extractorFactory ?? throw new ArgumentNullException(nameof(extractorFactory));
            _wsq = wsq ?? throw new ArgumentNullException(nameof(wsq));
            _pdfWriter = pdfWriter; // optional: null => PDF file creation skipped
            _postProcessorLoader = postProcessorLoader; // optional: null => PostProcessDLL skipped
            _log = log ?? NullLog.Instance;
        }

        public GroupProcessResult ProcessGroups(GroupProcessRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            _current = request;
            var result = new GroupProcessResult();

            string root = request.Defaults.GroupsToProcessFolder;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                result.Errors.Add("GroupsToProcessFolder not found: " + root);
                return result;
            }

            result.StatusReportPath = CreateStatusReport(request.Defaults);

            foreach (string folder in Directory.GetDirectories(root))
            {
                string name = Path.GetFileName(folder);
                try
                {
                    string packetPath = ProcessGroup(folder, request, result.StatusReportPath);
                    result.GroupsProcessed++;
                    result.WrittenPackets.Add(packetPath);
                    AppendStatusReport(result.StatusReportPath, name + ": processed successfully.");
                }
                catch (Exception ex)
                {
                    result.GroupsFailed++;
                    string msg = "Group '" + name + "' failed: " + ex.Message;
                    result.Errors.Add(msg);
                    _log.Write(msg);
                    AppendStatusReport(result.StatusReportPath, name + ": FAILED - " + ex.Message);
                }
            }

            return result;
        }

        private string ProcessGroup(string folder, GroupProcessRequest request, string statusReportPath)
        {
            string groupName = Path.GetFileName(folder);
            _log.Write("Processing group: " + groupName);

            GroupDefinition groupDef = ResolveGroupDefinition(folder, request);
            if (groupDef == null)
                throw new InvalidOperationException(
                    "No configured card type found for any file in group: " + groupName);

            if (!File.Exists(groupDef.VerificationFile))
                throw new FileNotFoundException("Verification file not found: " + groupDef.VerificationFile);

            using (INistTransaction tx = _transactionFactory.Create())
            using (ICardImageExtractor extractor = _extractorFactory.Create())
            {
                tx.ReadVerification(groupDef.VerificationFile);
                tx.NewTransaction(groupDef.TOT);

                int type2Index = tx.AddRecord(2);
                tx.Set(groupDef.AwareKeyTag, groupName, type2Index);
                // VB6 stored the group folder's creation timestamp as a locale datetime string
                // (e.g. "7/23/2026 1:25:04 PM"), NOT CCYYMMDD - matched here for downstream parity.
                tx.Set("T2_PRC",
                    Directory.GetCreationTime(folder).ToString("M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture),
                    type2Index);

                // When SaveGroupPDF=Y the VB6 archived each scanned card BMP as a PDF page. We store
                // the GUID PDF-filename tag in the Type-2 record (as the VB6 did) and collect the card
                // BMP paths as we process them, then build the PDF after the packet is written.
                bool savePdf = request.Defaults.SaveGroupPDF == "Y";
                string pdfFilePath = null;
                var pdfImagePaths = savePdf ? new List<string>() : null;
                if (savePdf)
                {
                    string pdfFileName = Guid.NewGuid().ToString("B").ToUpperInvariant() + ".PDF";
                    pdfFilePath = EnsureTrailingSlash(request.Defaults.GroupPDFFolder) + pdfFileName;
                    if (!string.IsNullOrEmpty(request.Defaults.PDFFileNameKeyTag))
                        tx.Set(request.Defaults.PDFFileNameKeyTag, pdfFileName, type2Index);
                    _log.Write("SaveGroupPDF=Y: PDF tag " + pdfFileName + " -> " + pdfFilePath);
                }

                var handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Pass 1: fronts, each immediately followed by its matching back.
                foreach (string file in Directory.GetFiles(folder))
                {
                    if (!IsBmp(file) || file.IndexOf("Front", StringComparison.Ordinal) < 0)
                        continue;

                    ParseCard(file, extractor, tx, type2Index, request.Defaults);
                    handled.Add(file);
                    pdfImagePaths?.Add(file);

                    string backFile = Path.Combine(folder,
                        Path.GetFileName(file).Replace("Front", "Back"));
                    if (File.Exists(backFile))
                    {
                        ParseCard(backFile, extractor, tx, type2Index, request.Defaults);
                        handled.Add(backFile);
                        pdfImagePaths?.Add(backFile);
                    }
                }

                // Pass 2: anything not handled above (single-sided cards, palms, etc.).
                foreach (string file in Directory.GetFiles(folder))
                {
                    if (!IsBmp(file) || handled.Contains(file)) continue;
                    ParseCard(file, extractor, tx, type2Index, request.Defaults);
                    handled.Add(file);
                    pdfImagePaths?.Add(file);
                }

                string packetPath = BuildPacketPath(groupDef.NISTOutDir, groupName);
                _log.Write("Writing NIST packet: " + packetPath);
                tx.Write(packetPath);

                // Build the PDF while the card BMPs still exist (before any folder cleanup).
                if (savePdf)
                    CreateGroupPdf(pdfImagePaths, pdfFilePath);

                // Run the customer post-process hook (managed; port of the VB6 PostProcessDLL COM call).
                bool processComplete = RunPostProcessor(groupDef, packetPath, groupName, statusReportPath);

                // VB6: with a PostProcessDLL configured, the group is removed only when it reports
                // ProcessComplete; otherwise it is left in place. Without one, it is always removed.
                if (request.DeleteGroupsAfterProcessing && processComplete)
                    DeleteFolder(folder);

                return packetPath;
            }
        }

        /// <summary>Port of ParseScannedCard: load a card and add its crop regions as NIST records.</summary>
        private void ParseCard(string bmpPath, ICardImageExtractor extractor, INistTransaction tx,
                               int type2Index, Defaults defaults)
        {
            string cardName = Path.GetFileNameWithoutExtension(bmpPath);
            _log.Write("Parsing card: " + cardName);

            if (!_current.CardsByName.TryGetValue(cardName, out CardDefinition card))
                throw new InvalidOperationException("No card definition for file: " + cardName);

            var area = new CardArea
            {
                Top = ParseF(card.ScanAreaTop),
                Left = ParseF(card.ScanAreaLeft),
                Height = ParseF(card.ScanAreaHeight),
                Width = ParseF(card.ScanAreaWidth)
            };

            extractor.LoadCard(bmpPath, area, ParseWholeCardRotation(card.Rotation));

            IDictionary<int, CropOverride> overrides = ReadCropOverrides(bmpPath);

            foreach (CropRegion region in card.CropRegions)
            {
                if (overrides != null && overrides.TryGetValue(region.FingerIndex, out CropOverride ov))
                {
                    region.Top = ov.Top;
                    region.Left = ov.Left;
                }

                if (region.FingerIndex != 15)
                {
                    ExtractedImage ext = extractor.GetRegionRaw(region);
                    byte[] data = ext.Data;
                    int h = ext.Height, w = ext.Width;

                    if (region.Rotation == 90 || region.Rotation == 180 || region.Rotation == 270)
                        data = RawImageRotation.Rotate(data, h, w, region.Rotation, out h, out w);

                    region.PxHeight = h;
                    region.PxWidth = w;

                    byte[] wsq = _wsq.CompressRaw(data, h, w);

                    if (region.FingerIndex < 15)
                        AddType4(tx, region, wsq, h, w);
                    if (region.FingerIndex >= 20 && region.FingerIndex <= 30)
                        AddType15(tx, region, wsq, h, w, defaults);
                }
                else
                {
                    ExtractedImage ext = extractor.GetRegionBmp(region);
                    region.PxHeight = ext.Height;
                    region.PxWidth = ext.Width;

                    tx.Set("T2_TP7", region.Desc, type2Index);
                    AddType7(tx, region, ext.Data, ext.Height, ext.Width);
                }
            }
        }

        // ---- NIST record builders (ports of AddType4Rec/AddType15Rec/AddType7Rec) ----------

        private static void AddType4(INistTransaction tx, CropRegion region, byte[] wsq, int pxHeight, int pxWidth)
        {
            int idx = tx.AddRecord(4);
            // WSQ bytes are passed with format "raw" (documented Aware workaround from the VB6).
            tx.SetImage(4, idx, "raw", wsq, "raw", 0f);
            // VB6 left these empty (its Height/Width locals were never assigned) and the golden
            // packet confirms they are stored as 0. We match that; the WSQ header carries the true
            // dimensions. (pxHeight/pxWidth remain available should a future NIST-correct fix be desired.)
            tx.Set("T4_VLL", "0", idx);
            tx.Set("T4_HLL", "0", idx);
            tx.Set("T4_FGP", region.FingerIndex.ToString(CultureInfo.InvariantCulture), idx);
            // IMP: 3 = NonLiveScan rolled (fingers 1-10), 2 = NonLiveScan plain (11-14).
            tx.Set("T4_IMP", region.FingerIndex < 11 ? "3" : "2", idx);
            tx.Set("T4_GCA", "1", idx); // 1 = WSQ
        }

        private static void AddType15(INistTransaction tx, CropRegion region, byte[] wsq,
                                      int pxHeight, int pxWidth, Defaults defaults)
        {
            int idx = tx.AddRecord(15);
            tx.SetImage(15, idx, "raw", wsq, "raw", 0f);
            tx.Set("T15_VLL", pxHeight.ToString(CultureInfo.InvariantCulture), idx);
            tx.Set("T15_HLL", pxWidth.ToString(CultureInfo.InvariantCulture), idx);
            tx.Set("T15_PLP", region.FingerIndex.ToString(CultureInfo.InvariantCulture), idx);
            tx.Set("T15_IMP", "1", idx);   // 1 = Nonlive-scan palm
            tx.Set("T15_CGA", "WSQ", idx);
            tx.Set("T15_SLC", "1", idx);   // 1 = pixels per inch
            tx.Set("T15_SRC", defaults.Source_ORI, idx);
            tx.Set("T15_PCD", GetDate(DateTime.Now), idx);
            tx.Set("T15_HPS", "500", idx);
            tx.Set("T15_VPS", "500", idx);
            tx.Set("T15_BPX", "8", idx);
        }

        private static void AddType7(INistTransaction tx, CropRegion region, byte[] image, int pxHeight, int pxWidth)
        {
            int idx = tx.AddRecord(7);
            tx.SetImage(7, idx, "raw", image, "raw", 0f);
            tx.Set("T7_VLL", pxHeight.ToString(CultureInfo.InvariantCulture), idx);
            tx.Set("T7_HLL", pxWidth.ToString(CultureInfo.InvariantCulture), idx);
            tx.Set("T7_GCA", "2", idx); // 2 = JPEG (per VB6; carried verbatim)
        }

        // ---- helpers --------------------------------------------------------------------

        /// <summary>Builds the group PDF from the collected card BMPs (port of the MUGPDF.CreatePDF call).
        /// If no PDF writer was supplied the tag is still written but the file is skipped. Never aborts
        /// the group: a PDF failure is logged (the VB6 MsgBox'd; here the packet is already written).</summary>
        private void CreateGroupPdf(IReadOnlyList<string> imagePaths, string pdfFilePath)
        {
            if (_pdfWriter == null)
            {
                _log.Write("SaveGroupPDF=Y but no PDF writer configured - PDF file skipped: " + pdfFilePath);
                return;
            }
            if (imagePaths == null || imagePaths.Count == 0)
            {
                _log.Write("SaveGroupPDF=Y but no card images to write - PDF skipped: " + pdfFilePath);
                return;
            }
            try
            {
                _log.Write("Saving PDF (" + imagePaths.Count + " page(s)): " + pdfFilePath);
                _pdfWriter.CreatePdf(imagePaths, pdfFilePath, "Scanned Document Group");
            }
            catch (Exception ex)
            {
                _log.Write("Error creating PDF '" + pdfFilePath + "': " + ex.Message);
            }
        }

        /// <summary>Runs the configured managed post-processor (port of the VB6 PostProcessDLL COM hook).
        /// Returns whether the group folder may be removed - true when none is configured, or when the
        /// processor reports ProcessComplete. Throws to fail the group when the processor reports an error.</summary>
        private bool RunPostProcessor(GroupDefinition groupDef, string packetPath, string keyValue, string statusReportPath)
        {
            if (string.IsNullOrEmpty(groupDef.PostProcessDLL))
                return true;

            if (_postProcessorLoader == null)
            {
                _log.Write("PostProcessDLL '" + groupDef.PostProcessDLL +
                           "' configured but no post-processor loader available - skipped.");
                return true;
            }

            _log.Write("Loading PostProcessDLL: " + groupDef.PostProcessDLL);
            IPostProcessor processor = _postProcessorLoader.Load(groupDef.PostProcessDLL);
            if (processor == null) return true;

            var ctx = new PostProcessContext
            {
                ConfigFileName = groupDef.PostProcessDLLcfg,
                NistPacketPath = packetPath,
                KeyValue = keyValue,
                VerificationFile = groupDef.VerificationFile,
                StatusReportPath = statusReportPath,
                TransactionType = groupDef.TOT
            };

            _log.Write("Calling PostProcess for group: " + keyValue);
            PostProcessResult ppr = processor.PostProcess(ctx) ?? PostProcessResult.Ok();

            if (!ppr.Success)
                throw new InvalidOperationException(
                    "PostProcessDLL '" + groupDef.PostProcessDLL + "' failed: " + (ppr.LastError ?? "(no detail)"));

            _log.Write("PostProcess complete (ProcessComplete=" + ppr.ProcessComplete + ") for group: " + keyValue);
            return ppr.ProcessComplete;
        }

        private static string EnsureTrailingSlash(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            return path.EndsWith("\\") || path.EndsWith("/") ? path : path + "\\";
        }

        private sealed class CropOverride { public float Top; public float Left; }

        private GroupDefinition ResolveGroupDefinition(string folder, GroupProcessRequest request)
        {
            foreach (string file in Directory.GetFiles(folder))
            {
                if (!IsBmp(file)) continue;
                string cardName = Path.GetFileNameWithoutExtension(file);
                if (request.CardsByName.TryGetValue(cardName, out CardDefinition card) &&
                    !string.IsNullOrEmpty(card.CardGroup) &&
                    request.GroupsByName.TryGetValue(card.CardGroup, out GroupDefinition group))
                {
                    return group;
                }
            }
            return null;
        }

        // Carries the request's card map to ParseCard without threading it through every signature.
        private GroupProcessRequest _current;

        private static bool IsBmp(string path) =>
            path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase);

        private static int ParseWholeCardRotation(string rotation)
        {
            if (int.TryParse((rotation ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int deg))
            {
                deg %= 360;
                if (deg == 90 || deg == 180 || deg == 270) return deg;
            }
            return 0;
        }

        private static float ParseF(string s) =>
            float.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;

        /// <summary>Reads an optional &lt;bmp&gt;.CRP override file: "FingerIndex,Desc,Top,Left,Height,Width" per line.</summary>
        private IDictionary<int, CropOverride> ReadCropOverrides(string bmpPath)
        {
            string crpPath = Path.ChangeExtension(bmpPath, ".CRP");
            if (!File.Exists(crpPath)) return null;

            var map = new Dictionary<int, CropOverride>();
            foreach (string line in File.ReadAllLines(crpPath))
            {
                string[] parts = line.Split(',');
                if (parts.Length < 4) continue;
                if (!int.TryParse(parts[0].Trim(), out int fgp)) continue;
                map[fgp] = new CropOverride { Top = ParseF(parts[2]), Left = ParseF(parts[3]) };
            }
            return map;
        }

        private static string BuildPacketPath(string nistOutDir, string groupName)
        {
            string path = nistOutDir + groupName + ".eft";
            for (int i = 1; i <= 100 && File.Exists(path); i++)
                path = nistOutDir + groupName + "_" + i.ToString(CultureInfo.InvariantCulture) + ".eft";

            if (File.Exists(path))
                throw new IOException("File already exists: " + path);
            return path;
        }

        private static void DeleteFolder(string folder)
        {
            foreach (string file in Directory.GetFiles(folder))
                File.Delete(file);
            Directory.Delete(folder, true);
        }

        /// <summary>Formats a date as CCYYMMDD (port of VB6 GetDate).</summary>
        private static string GetDate(DateTime date) => date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        /// <summary>Creates a timestamped group status report file (port of CreateGroupReportFile). Null if no folder configured.</summary>
        private string CreateStatusReport(Defaults defaults)
        {
            string folder = defaults.GroupStatusReportFolder;
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return null;
            try
            {
                string path = folder + "DWPCardScan_GroupReport_" +
                              DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + ".txt";
                AppendStatusReport(path, "DWPCardScan Group Status Report.");
                return path;
            }
            catch (Exception ex) { _log.Write("Could not create status report: " + ex.Message); return null; }
        }

        /// <summary>Appends a timestamped line to the status report (port of WriteToStatusReport). Never throws.</summary>
        private void AppendStatusReport(string reportPath, string message)
        {
            if (string.IsNullOrEmpty(reportPath)) return;
            try { File.AppendAllText(reportPath, DateTime.Now + ": " + message + Environment.NewLine); }
            catch (Exception ex) { _log.Write("Status report write failed: " + ex.Message); }
        }
    }
}
