using System.Collections.Generic;

namespace DWPCardScanNet.Core.Configuration
{
    /// <summary>
    /// Definition of one card type. Ported from VB6 clsCardDef.
    /// <para>
    /// Faithful 1:1 port: fields that were String in the VB6 class remain string here
    /// (values such as CardHeight="8.0", Rotation="0", Duplex="N"). Introducing strong
    /// types (double/bool/enum) is a deliberate later refinement to be done once
    /// golden-file regression tests exist - not during the initial port.
    /// </para>
    /// <para>
    /// Note the VB6 quirk preserved by <see cref="ConfigReader.LoadCardDef"/>: the
    /// constructor defaults below (e.g. CardType="FBI") are overwritten by whatever the
    /// .ini contains, including blank, because clsConfig.LoadCardDef assigned every key
    /// unconditionally. The defaults therefore only apply to a CardDefinition that is
    /// never passed through LoadCardDef.
    /// </para>
    /// </summary>
    public sealed class CardDefinition
    {
        public string CardName { get; set; } = "";
        public string FlipSide { get; set; } = "";
        public string VerificationFile { get; set; } = "";
        public string CardType { get; set; } = "FBI";
        public string CardGroup { get; set; } = "";
        public string Rotation { get; set; } = "0";
        public string Duplex { get; set; } = "N";
        public string AddBackToGroup { get; set; } = "Y";
        public string CardHeight { get; set; } = "8.0";
        public string CardWidth { get; set; } = "8.0";
        public string ScanAreaTop { get; set; } = "3.0";
        public string ScanAreaLeft { get; set; } = "0.0";
        public string ScanAreaHeight { get; set; } = "5.0";
        public string ScanAreaWidth { get; set; } = "8.0";
        public string OCRTop { get; set; } = "0.0";
        public string OCRLeft { get; set; } = "0.0";
        public string OCRHeight { get; set; } = "0.0";
        public string OCRWidth { get; set; } = "0.0";
        public string HostData { get; set; } = "0";

        /// <summary>Crop regions for this card, in .ini order (CropRegion1, CropRegion2, ...).</summary>
        public List<CropRegion> CropRegions { get; } = new List<CropRegion>();
    }
}
