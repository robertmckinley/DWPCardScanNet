using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using DWPCardScanNet.Aware;
using DWPCardScanNet.Core.Configuration;
using DWPCardScanNet.Core.Grouping;
using DWPCardScanNet.Core.Logging;
using DWPCardScanNet.Core.Processing;

namespace DWPCardScanNet
{
    /// <summary>
    /// Main window - port of VB6 frmMain. Wires the ported services together: config load,
    /// the to-process thumbnail queue, a card preview with crop-region overlays, card grouping,
    /// and NIST processing. Interactive crop-region editing, the undo journal, the group status
    /// report window, and in-process scanning are deferred (see the buttons/notes below).
    /// </summary>
    public partial class MainForm : Form
    {
        private const string MugConfigIni = @"C:\MugConfig\DWPCardScan\DWPCardScan.ini";
        private const string DriveRemapTo = @"C:\MugConfig\DWPCardScan\"; // test box had no D:

        private ILog _log = NullLog.Instance;
        private Defaults _defaults;
        private readonly List<CardDefinition> _cardList = new List<CardDefinition>();
        private readonly Dictionary<string, CardDefinition> _cards =
            new Dictionary<string, CardDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GroupDefinition> _groups =
            new Dictionary<string, GroupDefinition>(StringComparer.OrdinalIgnoreCase);
        private GroupingService _grouping;

        private string _currentSourceBmp;
        private bool _suppressCardTypeEvent;
        private int _lastQueueJpgCount = -1;
        private Timer _refreshTimer;

        public MainForm()
        {
            InitializeComponent();
            cmbCardType.SelectedIndexChanged += OnCardTypeChanged;
            btnAddGroup.Click += OnAddGroup;
            btnProcess.Click += OnProcess;
            btnUndo.Click += OnUndo;
            btnScanCards.Click += OnScan;
            tsbDelete.Click += OnDeleteCard;
            tsbPan.Click += (s, e) => TogglePreviewMode(CardPreviewControl.InteractionMode.Pan);
            tsbZoom.Click += (s, e) => TogglePreviewMode(CardPreviewControl.InteractionMode.Zoom);
            tsbRotate.Click += (s, e) => cardPreview.RotateView();
            tvwGroups.DoubleClick += OnGroupDoubleClick;

            // Keep the Card Type combo centered over the card image.
            cardPreview.SizeChanged += (s, e) => CenterCardTypeRow();
            pnlCardType.SizeChanged += (s, e) => CenterCardTypeRow();
        }

        /// <summary>Positions the Card Type combo centered over the card image, label to its left.</summary>
        private void CenterCardTypeRow()
        {
            if (cardPreview.Width <= 0 || pnlCardType.Width <= 0) return;
            const int gap = 6;
            int cx = cardPreview.GetImageCenterX(pnlCardType);
            cmbCardType.Top = (pnlCardType.Height - cmbCardType.Height) / 2;
            cmbCardType.Left = cx - cmbCardType.Width / 2;
            lblCardType.Top = (pnlCardType.Height - lblCardType.Height) / 2;
            lblCardType.Left = cmbCardType.Left - gap - lblCardType.Width;
        }

        private void OnDeleteCard(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentSourceBmp) || !File.Exists(_currentSourceBmp))
                return;
            if (MessageBox.Show("Delete this card from the queue?", "Delete Card",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            try
            {
                File.Delete(_currentSourceBmp);
                string thumb = Path.ChangeExtension(_currentSourceBmp, ".JPG");
                if (File.Exists(thumb)) File.Delete(thumb);
                _log.Write("Deleted card from queue: " + _currentSourceBmp);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not delete card:\n" + ex.Message, "Delete Card",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            LoadQueue();
        }

        /// <summary>Toggles the preview between Edit-crops and the given tool mode; Pan/Zoom are mutually exclusive.</summary>
        private void TogglePreviewMode(CardPreviewControl.InteractionMode mode)
        {
            bool turnOn = cardPreview.Mode != mode;
            cardPreview.Mode = turnOn ? mode : CardPreviewControl.InteractionMode.EditCrops;
            tsbPan.Checked = cardPreview.Mode == CardPreviewControl.InteractionMode.Pan;
            tsbZoom.Checked = cardPreview.Mode == CardPreviewControl.InteractionMode.Zoom;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                string iniPath = File.Exists(MugConfigIni)
                    ? MugConfigIni
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DWPCardScan.ini");
                if (!File.Exists(iniPath))
                {
                    MessageBox.Show("Configuration file not found:\n" + iniPath, "DWP Card Scan",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var reader = new ConfigReader(iniPath);
                _defaults = reader.LoadDefaults();

                _log = new FileLog(FileLog.ResolveDefaultPath(_defaults.LogFileDir, AppDomain.CurrentDomain.BaseDirectory));
                _log.Write("MainForm loading. INI: " + iniPath);

                foreach (CardDefinition c in reader.LoadCardDefinitions())
                {
                    _cardList.Add(c);
                    _cards[c.CardName] = c;
                }
                foreach (GroupDefinition g in reader.LoadGroupDefinitions())
                {
                    g.NISTOutDir = Remap(g.NISTOutDir);
                    _groups[g.GroupName] = g;
                }
                _defaults.GroupPDFFolder = Remap(_defaults.GroupPDFFolder);

                _grouping = new GroupingService(_defaults, _cards, _log);

                Text = _defaults.FormCaption + " - " +
                       Assembly.GetExecutingAssembly().GetName().Version.ToString(2);
                ApplyBanner();
                WarnIfFolderMissing(_defaults.ToProcessQueue, "ToProcessQueue");
                WarnIfFolderMissing(_defaults.GroupsToProcessFolder, "GroupsToProcessFolder");

                _suppressCardTypeEvent = true;
                foreach (CardDefinition c in _cardList) cmbCardType.Items.Add(c.CardName);
                _suppressCardTypeEvent = false;
                if (cmbCardType.Items.Count > 0)
                    cmbCardType.SelectedIndex = ResolveDefaultCardIndex();

                LoadQueue();
                LoadGroupsTree();

                _refreshTimer = new Timer { Interval = 5000 };
                _refreshTimer.Tick += OnRefreshTick;
                _refreshTimer.Start();

                CenterCardTypeRow();
            }
            catch (Exception ex)
            {
                _log.Write("MainForm_Load failed: " + ex);
                MessageBox.Show("Startup error:\n" + ex.Message, "DWP Card Scan",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Chooses the initially-selected card type: the configured DefaultCard if present,
        /// otherwise the plain "&lt;group&gt; - Front" card (the standard front layout), else the first.
        /// The operator changes it per card; crop overlays always reflect the selected type.
        /// </summary>
        private int ResolveDefaultCardIndex()
        {
            if (!string.IsNullOrEmpty(_defaults.DefaultCard))
            {
                int i = cmbCardType.Items.IndexOf(_defaults.DefaultCard);
                if (i >= 0) return i;
            }
            for (int i = 0; i < cmbCardType.Items.Count; i++)
                if (Regex.IsMatch(cmbCardType.Items[i].ToString(), @"^\S+\s+- Front$"))
                    return i;
            return 0;
        }

        private void ApplyBanner()
        {
            lblBanner.Text = _defaults.BannerText ?? "";
            switch ((_defaults.BannerFontColor ?? "").Trim())
            {
                case "Red": lblBanner.ForeColor = Color.Red; break;
                case "Green": lblBanner.ForeColor = Color.FromArgb(0, 128, 0); break;
                case "Yellow": lblBanner.ForeColor = Color.Gold; break;
                case "Blue": lblBanner.ForeColor = Color.Blue; break;
                default: lblBanner.ForeColor = Color.Black; break;
            }
        }

        // ---- queue (to-process thumbnails) ----------------------------------------------

        private ThumbnailControl _selectedThumb;

        private void LoadQueue()
        {
            flpQueue.SuspendLayout();
            try
            {
                while (flpQueue.Controls.Count > 0)
                {
                    Control c = flpQueue.Controls[0];
                    flpQueue.Controls.RemoveAt(0);
                    c.Dispose();
                }
                _selectedThumb = null;

                string queueDir = _defaults.ToProcessQueue;
                if (string.IsNullOrEmpty(queueDir) || !Directory.Exists(queueDir))
                {
                    _lastQueueJpgCount = 0;
                    _currentSourceBmp = null;
                    ShowPreview();
                    return;
                }

                var jpgs = new List<string>(Directory.GetFiles(queueDir, "*.JPG"));
                _lastQueueJpgCount = jpgs.Count;
                SortQueue(jpgs);

                ThumbnailControl first = null;
                foreach (string jpg in jpgs)
                {
                    Image img;
                    try
                    {
                        using (var ms = new MemoryStream(File.ReadAllBytes(jpg)))
                        using (var tmp = Image.FromStream(ms))
                            img = new Bitmap(tmp);
                    }
                    catch { continue; }

                    var thumb = new ThumbnailControl(img, Path.GetFileNameWithoutExtension(jpg),
                                                     Path.ChangeExtension(jpg, ".BMP"));
                    thumb.Click += OnThumbnailClick;
                    flpQueue.Controls.Add(thumb);
                    if (first == null) first = thumb;
                }

                if (first != null) SelectThumbnail(first);
                else { _currentSourceBmp = null; ShowPreview(); }
            }
            finally { flpQueue.ResumeLayout(); }
        }

        private void OnThumbnailClick(object sender, EventArgs e) => SelectThumbnail(sender as ThumbnailControl);

        private void SelectThumbnail(ThumbnailControl thumb)
        {
            if (thumb == null) return;
            if (_selectedThumb != null) _selectedThumb.Selected = false;
            _selectedThumb = thumb;
            thumb.Selected = true;
            _currentSourceBmp = thumb.BmpPath;
            ShowPreview();
        }

        /// <summary>Newest first (leading number descending), then Front before Back. Port of LoadToProcessQueue sort.</summary>
        private static void SortQueue(List<string> jpgs)
        {
            jpgs.Sort((a, b) =>
            {
                string na = Path.GetFileName(a).ToUpperInvariant();
                string nb = Path.GetFileName(b).ToUpperInvariant();
                int numA = LeadingNumber(na), numB = LeadingNumber(nb);
                if (numA != numB) return numB.CompareTo(numA);
                bool frontA = na.Contains("_FRONT"), frontB = nb.Contains("_FRONT");
                if (frontA != frontB) return frontA ? -1 : 1;
                return string.CompareOrdinal(na, nb);
            });
        }

        private static int LeadingNumber(string name)
        {
            int n = 0, i = 0;
            while (i < name.Length && char.IsDigit(name[i])) { n = n * 10 + (name[i] - '0'); i++; }
            return n;
        }

        private void ShowPreview()
        {
            if (string.IsNullOrEmpty(_currentSourceBmp) || !File.Exists(_currentSourceBmp))
            {
                cardPreview.ClearCard();
                return;
            }

            CardDefinition card = SelectedCard();
            if (card != null)
                cardPreview.SetCard(_currentSourceBmp, ParseF(card.CardWidth), ParseF(card.CardHeight), card.CropRegions);
            else
                cardPreview.SetCard(_currentSourceBmp, 8f, 8f, null);
        }

        // ---- card type ------------------------------------------------------------------

        private void OnCardTypeChanged(object sender, EventArgs e)
        {
            if (_suppressCardTypeEvent) return;
            CardDefinition card = SelectedCard();
            if (card != null && _groups.TryGetValue(card.CardGroup, out GroupDefinition grp))
            {
                lblKeyPrompt.Text = string.IsNullOrEmpty(grp.KeyPrompt) ? "Key Value:" : grp.KeyPrompt;
                lblKeyPrefix.Text = grp.KeyPrefix ?? "";
            }
            ShowPreview();
        }

        private CardDefinition SelectedCard()
        {
            string name = cmbCardType.SelectedItem as string;
            if (!string.IsNullOrEmpty(name) && _cards.TryGetValue(name, out CardDefinition c)) return c;
            return null;
        }

        // ---- grouping -------------------------------------------------------------------

        private void OnAddGroup(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentSourceBmp))
            {
                MessageBox.Show("Please select a card from the queue.", "Add to Group");
                return;
            }
            CardDefinition card = SelectedCard();
            if (card == null) { MessageBox.Show("Please select a card type.", "Add to Group"); return; }

            GroupingResult result = _grouping.AddCardToGroup(
                card.CardName, txtKeyValue.Text, _currentSourceBmp, lblKeyPrefix.Text,
                cardPreview.GetAdjustedRegions(),
                (baseKey, newKey) => MessageBox.Show(
                    "Group '" + baseKey + "' already contains this card or a conflicting finger.\n\n" +
                    "Create duplicate group '" + newKey + "'?",
                    "Create Duplicate Group?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);

            if (!result.Success)
            {
                if (result.ErrorMessage != null)   // null == operator cancelled the duplicate-group prompt
                    MessageBox.Show(result.ErrorMessage, "Add to Group", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            txtKeyValue.Clear();
            LoadQueue();       // picks up removed cards
            LoadGroupsTree();  // shows the new/updated group
        }

        // ---- processing -----------------------------------------------------------------

        private void OnProcess(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_defaults.ProcessMessage))
            {
                if (MessageBox.Show(_defaults.ProcessMessage, "Confirmation",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
            }

            var request = new GroupProcessRequest
            {
                Defaults = _defaults,
                CardsByName = _cards,
                GroupsByName = _groups,
                DeleteGroupsAfterProcessing = true
            };

            var processor = new GroupProcessor(
                new AwareNistTransactionFactory(),
                new AwareCardImageExtractorFactory(),
                new AwareWsqCompressor(),
                _log);

            GroupProcessResult result;
            Cursor = Cursors.WaitCursor;
            try { result = processor.ProcessGroups(request); }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                _log.Write("Process failed: " + ex);
                MessageBox.Show("Processing error:\n" + ex.Message, "Process", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally { Cursor = Cursors.Default; }

            LoadQueue();
            LoadGroupsTree();

            // Show the status report (VB6 DisplayGroupStatusReport) when configured and available;
            // otherwise fall back to a summary message box.
            if (_defaults.DisplayGroupStatusReport == 1 &&
                !string.IsNullOrEmpty(result.StatusReportPath) && File.Exists(result.StatusReportPath))
            {
                using (var report = new StatusReportForm()) report.Display(result.StatusReportPath);
            }
            else
            {
                var msg = "Groups processed: " + result.GroupsProcessed + "\nFailed: " + result.GroupsFailed;
                if (result.Errors.Count > 0) msg += "\n\nErrors:\n" + string.Join("\n", result.Errors.ToArray());
                MessageBox.Show(msg, "Process Card Groups",
                    MessageBoxButtons.OK, result.GroupsFailed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
        }

        private void OnUndo(object sender, EventArgs e)
        {
            TreeNode node = tvwGroups.SelectedNode;
            if (node == null)
            {
                MessageBox.Show("Select a card group to undo.", "Undo Group",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            TreeNode groupNode = node.Parent ?? node;   // if a card is selected, undo its whole group
            string groupName = groupNode.Text;
            if (MessageBox.Show("Undo group '" + groupName + "'?\nIts cards return to the queue.",
                    "Undo Group", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            GroupingResult result = _grouping.UndoGroup(_defaults.GroupsToProcessFolder + groupName + "\\");
            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage, "Undo Group", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Regenerate the queue thumbnails for the restored cards so they reappear in the strip.
            foreach (string bmp in result.RemovedSourcePaths)
            {
                try { BmpCodec.WriteJpegThumbnail(bmp, Path.ChangeExtension(bmp, ".JPG"), 200); }
                catch (Exception ex) { _log.Write("Undo thumbnail regen failed for " + bmp + ": " + ex.Message); }
            }

            LoadQueue();
            LoadGroupsTree();
        }

        // ---- scan -----------------------------------------------------------------------

        private void OnScan(object sender, EventArgs e)
        {
            string shell = _defaults.ScanShellFilePath;
            if (!string.IsNullOrEmpty(shell))
            {
                if (!File.Exists(shell))
                {
                    MessageBox.Show("Scan application not found:\n" + shell, "Scan Cards",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                try { Process.Start(shell); }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to launch scan application:\n" + ex.Message, "Scan Cards",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            MessageBox.Show("In-process scanning (AwareScanner) is not yet wired into the UI in this build.\n" +
                            "Configure ScanShellFilePath to launch the external scanner.", "Scan Cards",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ---- groups tree ----------------------------------------------------------------

        private void LoadGroupsTree()
        {
            tvwGroups.Nodes.Clear();
            string root = _defaults.GroupsToProcessFolder;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;

            foreach (string dir in Directory.GetDirectories(root))
            {
                var groupNode = tvwGroups.Nodes.Add(Path.GetFileName(dir));
                foreach (string bmp in Directory.GetFiles(dir, "*.BMP"))
                {
                    var child = groupNode.Nodes.Add(Path.GetFileNameWithoutExtension(bmp));
                    child.Tag = bmp;
                }
                groupNode.Expand();
            }
        }

        private void OnGroupDoubleClick(object sender, EventArgs e)
        {
            TreeNode node = tvwGroups.SelectedNode;
            if (node?.Tag is string bmp && File.Exists(bmp))
                using (var zoom = new ZoomForm()) zoom.Display(bmp);
        }

        // ---- misc -----------------------------------------------------------------------

        private void OnRefreshTick(object sender, EventArgs e)
        {
            // Pick up cards produced by the external scanner without disrupting the operator.
            string queueDir = _defaults.ToProcessQueue;
            if (string.IsNullOrEmpty(queueDir) || !Directory.Exists(queueDir)) return;
            int count = Directory.GetFiles(queueDir, "*.JPG").Length;
            if (count != _lastQueueJpgCount) LoadQueue();
        }

        private void WarnIfFolderMissing(string path, string label)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                MessageBox.Show(label + " folder does not exist:\n" + path, "DWP Card Scan",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static string Remap(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            return path.StartsWith(@"D:\", StringComparison.OrdinalIgnoreCase)
                ? DriveRemapTo + path.Substring(3) : path;
        }

        private static float ParseF(string s)
        {
            return float.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 8f;
        }
    }
}
