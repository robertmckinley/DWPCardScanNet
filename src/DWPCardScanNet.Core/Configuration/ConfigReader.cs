using System;
using System.Collections.Generic;
using System.Globalization;

namespace DWPCardScanNet.Core.Configuration
{
    /// <summary>
    /// Loads <see cref="Defaults"/>, card definitions and group definitions from a
    /// DWPCardScan .ini file. Port of the VB6 clsConfig (GetDefaults / GetCardDefinitions /
    /// GetGroupDefinitions / LoadCardDef / LoadGroupDef).
    /// <para>
    /// Behavioural parity notes vs. the VB6 original:
    /// <list type="bullet">
    /// <item>Reading is lenient: missing keys yield "" and fall back to the object's default,
    /// exactly as GetParam did. No exceptions for absent keys.</item>
    /// <item>Numeric values are parsed with <see cref="CultureInfo.InvariantCulture"/> so the
    /// "." decimal separators in the .ini (e.g. "1.6", "7.75") parse correctly regardless of
    /// the machine's regional settings - closing a latent locale bug in the VB6 CSng/CInt calls.</item>
    /// <item>Folder settings get a trailing "\" appended (matching modCommon.EnsureTrailingSlash).</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class ConfigReader
    {
        private readonly IniFile _ini;

        public ConfigReader(string iniPath) : this(new IniFile(iniPath)) { }

        public ConfigReader(IniFile ini)
        {
            _ini = ini ?? throw new ArgumentNullException(nameof(ini));
        }

        public string IniFilePath => _ini.FilePath;

        private string Get(string section, string key) => _ini.GetString(section, key);

        /// <summary>Loads the [DefaultSettings] section. Mirrors clsConfig.GetDefaults.</summary>
        public Defaults LoadDefaults()
        {
            const string s = "DefaultSettings";
            var d = new Defaults();

            d.DefaultCard = Get(s, "DefaultCard");
            if (TryInt(Get(s, "ScannerType"), out var scannerType)) d.ScannerType = scannerType;
            d.KeyFieldTag = Get(s, "KeyFieldTag");
            if (TryInt(Get(s, "SaveProcessedCards"), out var saveProcessed)) d.SaveProcessedCards = saveProcessed;
            d.GroupsToProcessFolder = Get(s, "GroupsToProcessFolder");
            d.SaveGroupPDF = Get(s, "SaveGroupPDF").ToUpperInvariant();     // "Y" or "N"
            d.GroupPDFFolder = Get(s, "GroupPDFFolder");
            d.PDFFileNameKeyTag = Get(s, "PDFFileNameKeyTag");
            d.RememberCardType = Get(s, "RememberCardType").ToUpperInvariant(); // "Y" or "N"
            d.ToProcessQueue = Get(s, "ToProcessQueue");
            d.ProcessedQueue = Get(s, "ProcessedQueue");
            d.GroupStatusReportFolder = Get(s, "GroupStatusReportFolder");
            d.LogFileDir = Get(s, "LogFileDir");
            d.DefaultCardSize = Get(s, "DefaultCardSize");
            d.DefaultDuplex = Get(s, "DefaultDuplex");
            d.ScanShellFilePath = Get(s, "ScanShellFilePath");
            if (TryInt(Get(s, "DupKeyEntry"), out var dupKey)) d.DupKeyEntry = dupKey;
            if (TryInt(Get(s, "UseADF"), out var useAdf)) d.UseADF = useAdf;
            d.BannerText = Get(s, "BannerText");
            d.BannerFontColor = Get(s, "BannerFontColor");

            // VB6 only overwrote the default caption when the key was present and non-blank.
            var formCaption = Get(s, "FormCaption");
            if (formCaption != "") d.FormCaption = formCaption;

            d.ProcessMessage = Get(s, "ProcessMessage");
            if (TryInt(Get(s, "DisplayGroupStatusReport"), out var displayReport)) d.DisplayGroupStatusReport = displayReport;

            // Normalise folder paths to a trailing backslash (blank stays blank).
            d.GroupsToProcessFolder = EnsureTrailingSlash(d.GroupsToProcessFolder);
            d.ToProcessQueue = EnsureTrailingSlash(d.ToProcessQueue);
            d.GroupStatusReportFolder = EnsureTrailingSlash(d.GroupStatusReportFolder);
            d.LogFileDir = EnsureTrailingSlash(d.LogFileDir);

            return d;
        }

        /// <summary>
        /// Enumerates Card1, Card2, ... in [Card Definitions] until a blank is hit, loading each.
        /// Mirrors clsConfig.GetCardDefinitions.
        /// </summary>
        public List<CardDefinition> LoadCardDefinitions()
        {
            var cards = new List<CardDefinition>();
            int i = 0;
            while (true)
            {
                i++;
                string name = Get("Card Definitions", "Card" + i.ToString(CultureInfo.InvariantCulture));
                if (name == "") break;

                var card = new CardDefinition { CardName = name };
                LoadCardDef(card);
                cards.Add(card);
            }
            return cards;
        }

        /// <summary>
        /// Enumerates Group1, Group2, ... in [Group Definitions] until a blank is hit, loading each.
        /// Mirrors clsConfig.GetGroupDefinitions.
        /// </summary>
        public List<GroupDefinition> LoadGroupDefinitions()
        {
            var groups = new List<GroupDefinition>();
            int i = 0;
            while (true)
            {
                i++;
                string name = Get("Group Definitions", "Group" + i.ToString(CultureInfo.InvariantCulture));
                if (name == "") break;

                var group = new GroupDefinition { GroupName = name };
                LoadGroupDef(group);
                groups.Add(group);
            }
            return groups;
        }

        /// <summary>
        /// Populates a card definition from its own [CardName] section, including its crop regions.
        /// Mirrors clsConfig.LoadCardDef. Note: every key is assigned unconditionally, so a blank
        /// value overwrites the constructor default (preserved VB6 behaviour).
        /// </summary>
        public void LoadCardDef(CardDefinition card)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            string s = card.CardName;

            card.VerificationFile = Get(s, "VerificationFile");
            card.CardType = Get(s, "CardType");
            card.CardGroup = Get(s, "CardGroup");
            card.Rotation = Get(s, "Rotation");
            card.Duplex = Get(s, "Duplex");
            card.AddBackToGroup = Get(s, "AddBackToGroup");
            card.FlipSide = Get(s, "FlipSide");
            card.CardHeight = Get(s, "CardHeight");
            card.CardWidth = Get(s, "CardWidth");
            card.ScanAreaTop = Get(s, "ScanAreaTop");
            card.ScanAreaLeft = Get(s, "ScanAreaLeft");
            card.ScanAreaHeight = Get(s, "ScanAreaHeight");
            card.ScanAreaWidth = Get(s, "ScanAreaWidth");
            card.OCRTop = Get(s, "OCRTop");
            card.OCRLeft = Get(s, "OCRLeft");
            card.OCRHeight = Get(s, "OCRHeight");
            card.OCRWidth = Get(s, "OCRWidth");
            card.HostData = Get(s, "HostData");

            // Crop regions: CropRegion1, CropRegion2, ... The "CropRegion<N>" key is only an
            // enabler flag; the region's finger position comes from "CropRegionAwareFingerIndex<N>".
            card.CropRegions.Clear();
            int i = 1;
            while (Get(s, "CropRegion" + i.ToString(CultureInfo.InvariantCulture)) != "")
            {
                string n = i.ToString(CultureInfo.InvariantCulture);
                var cr = new CropRegion
                {
                    FingerIndex = ToInt(Get(s, "CropRegionAwareFingerIndex" + n)),
                    Top = ToSingle(Get(s, "CropRegionTop" + n)),
                    Left = ToSingle(Get(s, "CropRegionLeft" + n)),
                    Height = ToSingle(Get(s, "CropRegionHeight" + n)),
                    Width = ToSingle(Get(s, "CropRegionWidth" + n)),
                    Desc = Get(s, "CropRegionDesc" + n),
                    ListIndex = i
                };
                if (TryInt(Get(s, "CropRegionRotation" + n), out var rotation))
                    cr.Rotation = rotation;

                card.CropRegions.Add(cr);
                i++;
            }
        }

        /// <summary>
        /// Populates a group definition from its own [GroupName] section.
        /// Mirrors clsConfig.LoadGroupDef.
        /// </summary>
        public void LoadGroupDef(GroupDefinition group)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            string s = group.GroupName;

            group.NISTOutDir = Get(s, "NISTOutDir");
            group.PostProcessDLL = Get(s, "PostProcessDLL");
            group.PostProcessDLLcfg = Get(s, "PostProcessDLLcfg");
            group.VerificationFile = Get(s, "VerificationFile");
            group.TOT = Get(s, "TOT");
            group.AwareKeyTag = Get(s, "AwareKeyTag");
            group.KeyPrompt = Get(s, "KeyPrompt");
            group.KeyPrefix = Get(s, "KeyPrefix");

            group.NISTOutDir = EnsureTrailingSlash(group.NISTOutDir);
        }

        // ---- helpers -------------------------------------------------------

        private static bool TryInt(string value, out int result) =>
            int.TryParse((value ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

        private static int ToInt(string value) => TryInt(value, out var r) ? r : 0;

        private static float ToSingle(string value) =>
            float.TryParse((value ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : 0f;

        private static string EnsureTrailingSlash(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.EndsWith("\\", StringComparison.Ordinal) ? s : s + "\\";
        }
    }
}
