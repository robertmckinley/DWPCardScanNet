using System;
using System.Collections.Generic;
using DWPCardScanNet.Core.Configuration;

namespace DWPCardScanNet.Core.Processing
{
    /// <summary>Compresses a raw 8-bpp grayscale image to WSQ. Replaces VB6 AwWsqCtrl.WsqCompressRaw.</summary>
    public interface IWsqCompressor
    {
        byte[] CompressRaw(byte[] rawImage, int height, int width);
    }

    /// <summary>
    /// Builds and writes a single NIST/EBTS transaction. Replaces VB6 AwTransCtrl
    /// (AwReadVerification / AwNew / AwAddRecord / AwSet / AwSetImage / AwWrite).
    /// </summary>
    public interface INistTransaction : IDisposable
    {
        void ReadVerification(string verificationFile);
        void NewTransaction(string transactionType);
        int AddRecord(int recordType);
        void Set(string mnemonic, string data, int recordIndex);
        void SetImage(int recordType, int recordIndex, string inputFormat, byte[] image,
                      string storageFormat, float compression);
        void Write(string path);
    }

    /// <summary>Creates a fresh <see cref="INistTransaction"/> (one per group folder).</summary>
    public interface INistTransactionFactory
    {
        INistTransaction Create();
    }

    /// <summary>A region extracted from a card: pixel data plus its pixel dimensions.</summary>
    public sealed class ExtractedImage
    {
        public byte[] Data { get; }
        public int Height { get; }
        public int Width { get; }

        public ExtractedImage(byte[] data, int height, int width)
        {
            Data = data;
            Height = height;
            Width = width;
        }
    }

    /// <summary>The card scan area (inches), from a <see cref="CardDefinition"/>.</summary>
    public struct CardArea
    {
        public float Top, Left, Height, Width;
    }

    /// <summary>
    /// Loads a scanned card image and extracts crop regions from it. Replaces the VB6
    /// AwScanControl usage (AwScanBmpToRaw / AwScanLoadCardMem / AwScanSetCropRegion /
    /// AwScanGetCropRegion / AwScanGetCropRegionImage) via managed AccuScan memory mode.
    /// One instance is used per group folder (matching the VB6 AwScanCreate/AwScanClose scope).
    /// </summary>
    public interface ICardImageExtractor : IDisposable
    {
        /// <summary>Loads a card BMP, rotating the whole card first if <paramref name="wholeCardRotationDegrees"/> is 90/180/270.</summary>
        void LoadCard(string bmpPath, CardArea area, int wholeCardRotationDegrees);

        /// <summary>Extracts a region as raw 8-bpp grayscale pixels (for WSQ / Type-4 &amp; Type-15).</summary>
        ExtractedImage GetRegionRaw(CropRegion region);

        /// <summary>Extracts a region as BMP bytes (for the Type-7 data-header image).</summary>
        ExtractedImage GetRegionBmp(CropRegion region);
    }

    /// <summary>Creates a fresh <see cref="ICardImageExtractor"/> (one per group folder).</summary>
    public interface ICardImageExtractorFactory
    {
        ICardImageExtractor Create();
    }

    /// <summary>
    /// Writes a multi-page PDF from a set of image files (one page per image). Replaces the VB6
    /// MUGPDF.clsMUGPDF.CreatePDF(ExtDocs, path, title) call, which archived each scanned card BMP
    /// as a PDF page. Implementations live outside Core (they carry the PDF-library dependency).
    /// </summary>
    public interface IPdfWriter
    {
        void CreatePdf(IReadOnlyList<string> imagePaths, string pdfPath, string title);
    }

    /// <summary>Inputs for a group-processing run.</summary>
    public sealed class GroupProcessRequest
    {
        public Defaults Defaults { get; set; }

        /// <summary>Card definitions keyed by card name (case-insensitive), as loaded from the .ini.</summary>
        public IDictionary<string, CardDefinition> CardsByName { get; set; }

        /// <summary>Group definitions keyed by group name (case-insensitive).</summary>
        public IDictionary<string, GroupDefinition> GroupsByName { get; set; }

        /// <summary>
        /// Delete each group folder after its packet is written (VB6 RemoveGroup behaviour).
        /// Defaults to false here so validation runs are non-destructive; production sets true.
        /// </summary>
        public bool DeleteGroupsAfterProcessing { get; set; } = false;
    }

    /// <summary>Outcome of a group-processing run.</summary>
    public sealed class GroupProcessResult
    {
        public int GroupsProcessed { get; set; }
        public int GroupsFailed { get; set; }
        public List<string> WrittenPackets { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();

        /// <summary>Path to the written group status report (null if no report folder is configured).</summary>
        public string StatusReportPath { get; set; }
    }

    /// <summary>Processes queued card groups into NIST packets. Replaces VB6 frmMain.cmdProcessGroup_Click.</summary>
    public interface IGroupProcessor
    {
        GroupProcessResult ProcessGroups(GroupProcessRequest request);
    }
}
