namespace DWPCardScanNet
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tblBody = new System.Windows.Forms.TableLayoutPanel();
            this.grpSelected = new System.Windows.Forms.GroupBox();
            this.cardPreview = new DWPCardScanNet.CardPreviewControl();
            this.tsCropTools = new System.Windows.Forms.ToolStrip();
            this.tsbMoveCrop = new System.Windows.Forms.ToolStripButton();
            this.tsbZoom = new System.Windows.Forms.ToolStripButton();
            this.tsbRotate = new System.Windows.Forms.ToolStripButton();
            this.tsbResetCrops = new System.Windows.Forms.ToolStripButton();
            this.tsbDelete = new System.Windows.Forms.ToolStripButton();
            this.pnlCardType = new System.Windows.Forms.Panel();
            this.lblCardType = new System.Windows.Forms.Label();
            this.cmbCardType = new System.Windows.Forms.ComboBox();
            this.pnlMiddle = new System.Windows.Forms.Panel();
            this.grpCardGroup = new System.Windows.Forms.GroupBox();
            this.lblKeyPrompt = new System.Windows.Forms.Label();
            this.lblKeyPrefix = new System.Windows.Forms.Label();
            this.txtKeyValue = new System.Windows.Forms.TextBox();
            this.btnAddGroup = new System.Windows.Forms.Button();
            this.pnlScan = new System.Windows.Forms.Panel();
            this.btnScanCards = new System.Windows.Forms.Button();
            this.grpGroups = new System.Windows.Forms.GroupBox();
            this.tvwGroups = new System.Windows.Forms.TreeView();
            this.pnlGroupTop = new System.Windows.Forms.Panel();
            this.btnProcess = new System.Windows.Forms.Button();
            this.btnUndo = new System.Windows.Forms.Button();
            this.grpQueue = new System.Windows.Forms.GroupBox();
            this.flpQueue = new System.Windows.Forms.FlowLayoutPanel();
            this.lblBanner = new System.Windows.Forms.Label();
            this.tblBody.SuspendLayout();
            this.grpSelected.SuspendLayout();
            this.tsCropTools.SuspendLayout();
            this.pnlCardType.SuspendLayout();
            this.pnlMiddle.SuspendLayout();
            this.grpCardGroup.SuspendLayout();
            this.pnlScan.SuspendLayout();
            this.grpGroups.SuspendLayout();
            this.pnlGroupTop.SuspendLayout();
            this.grpQueue.SuspendLayout();
            this.SuspendLayout();
            //
            // tblBody (3 columns: Selected Card 100%, middle 300, Card Groups 340)
            //
            this.tblBody.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tblBody.ColumnCount = 3;
            this.tblBody.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tblBody.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 300F));
            this.tblBody.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 340F));
            this.tblBody.RowCount = 1;
            this.tblBody.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tblBody.Controls.Add(this.grpSelected, 0, 0);
            this.tblBody.Controls.Add(this.pnlMiddle, 1, 0);
            this.tblBody.Controls.Add(this.grpGroups, 2, 0);
            this.tblBody.Name = "tblBody";
            //
            // grpSelected ("Selected Card")
            //
            this.grpSelected.Controls.Add(this.cardPreview);
            this.grpSelected.Controls.Add(this.tsCropTools);
            this.grpSelected.Controls.Add(this.pnlCardType);
            this.grpSelected.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpSelected.Name = "grpSelected";
            this.grpSelected.Padding = new System.Windows.Forms.Padding(6);
            this.grpSelected.Text = "Selected Card";
            //
            // cardPreview
            //
            this.cardPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cardPreview.Name = "cardPreview";
            //
            // tsCropTools (pan / zoom / rotate / delete)
            //
            this.tsCropTools.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.tsCropTools.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.tsbMoveCrop, this.tsbZoom, this.tsbRotate, this.tsbResetCrops, this.tsbDelete });
            this.tsCropTools.Dock = System.Windows.Forms.DockStyle.Top;
            this.tsCropTools.Name = "tsCropTools";
            this.tsbMoveCrop.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbMoveCrop.Text = "✋"; this.tsbMoveCrop.ToolTipText = "Move Crop Region";
            this.tsbZoom.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbZoom.Text = "\U0001F50D"; this.tsbZoom.ToolTipText = "Zoom";
            this.tsbRotate.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbRotate.Text = "↻"; this.tsbRotate.ToolTipText = "Rotate";
            this.tsbResetCrops.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbResetCrops.Text = "Reset"; this.tsbResetCrops.ToolTipText = "Reset zoom/rotation and crop positions";
            this.tsbDelete.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbDelete.Text = "✖"; this.tsbDelete.ToolTipText = "Delete card";
            this.tsbDelete.ForeColor = System.Drawing.Color.Red;
            //
            // pnlCardType (Card Type combo row)
            //
            this.pnlCardType.Controls.Add(this.cmbCardType);
            this.pnlCardType.Controls.Add(this.lblCardType);
            this.pnlCardType.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlCardType.Height = 30;
            this.pnlCardType.Name = "pnlCardType";
            this.lblCardType.AutoSize = true;
            this.lblCardType.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.lblCardType.Location = new System.Drawing.Point(220, 8);
            this.lblCardType.Name = "lblCardType";
            this.lblCardType.Text = "Card Type:";
            this.cmbCardType.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.cmbCardType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCardType.Location = new System.Drawing.Point(295, 4);
            this.cmbCardType.Name = "cmbCardType";
            this.cmbCardType.Size = new System.Drawing.Size(300, 21);
            //
            // pnlMiddle (Scan Cards + Card Group)
            //
            this.pnlMiddle.Controls.Add(this.grpCardGroup);
            this.pnlMiddle.Controls.Add(this.pnlScan);
            this.pnlMiddle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlMiddle.Name = "pnlMiddle";
            //
            // pnlScan (holds the Scan Cards button, centered)
            //
            this.pnlScan.Controls.Add(this.btnScanCards);
            this.pnlScan.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlScan.Height = 60;
            this.pnlScan.Name = "pnlScan";
            this.btnScanCards.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.btnScanCards.Location = new System.Drawing.Point(55, 16);
            this.btnScanCards.Name = "btnScanCards";
            this.btnScanCards.Size = new System.Drawing.Size(190, 30);
            this.btnScanCards.Text = "Scan Cards";
            this.btnScanCards.UseVisualStyleBackColor = true;
            //
            // grpCardGroup ("Card Group")
            //
            this.grpCardGroup.Controls.Add(this.lblKeyPrompt);
            this.grpCardGroup.Controls.Add(this.lblKeyPrefix);
            this.grpCardGroup.Controls.Add(this.txtKeyValue);
            this.grpCardGroup.Controls.Add(this.btnAddGroup);
            this.grpCardGroup.Dock = System.Windows.Forms.DockStyle.Top;
            this.grpCardGroup.Height = 150;
            this.grpCardGroup.Name = "grpCardGroup";
            this.grpCardGroup.Text = "Card Group";
            this.lblKeyPrompt.AutoSize = true;
            this.lblKeyPrompt.Location = new System.Drawing.Point(14, 44);
            this.lblKeyPrompt.Name = "lblKeyPrompt";
            this.lblKeyPrompt.Text = "Key Value:";
            this.lblKeyPrefix.AutoSize = true;
            this.lblKeyPrefix.Location = new System.Drawing.Point(96, 44);
            this.lblKeyPrefix.Name = "lblKeyPrefix";
            this.lblKeyPrefix.Size = new System.Drawing.Size(24, 13);
            this.lblKeyPrefix.Text = "";
            this.txtKeyValue.Location = new System.Drawing.Point(122, 40);
            this.txtKeyValue.Name = "txtKeyValue";
            this.txtKeyValue.Size = new System.Drawing.Size(150, 20);
            this.btnAddGroup.Location = new System.Drawing.Point(16, 92);
            this.btnAddGroup.Name = "btnAddGroup";
            this.btnAddGroup.Size = new System.Drawing.Size(256, 32);
            this.btnAddGroup.Text = "Add Card to Group >>";
            this.btnAddGroup.UseVisualStyleBackColor = true;
            //
            // grpGroups ("Card Groups")
            //
            this.grpGroups.Controls.Add(this.tvwGroups);
            this.grpGroups.Controls.Add(this.pnlGroupTop);
            this.grpGroups.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpGroups.Name = "grpGroups";
            this.grpGroups.Padding = new System.Windows.Forms.Padding(8);
            this.grpGroups.Text = "Card Groups";
            //
            // pnlGroupTop (Process button + Undo)
            //
            this.pnlGroupTop.Controls.Add(this.btnProcess);
            this.pnlGroupTop.Controls.Add(this.btnUndo);
            this.pnlGroupTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlGroupTop.Height = 44;
            this.pnlGroupTop.Name = "pnlGroupTop";
            this.btnProcess.Location = new System.Drawing.Point(4, 6);
            this.btnProcess.Name = "btnProcess";
            this.btnProcess.Size = new System.Drawing.Size(200, 32);
            this.btnProcess.Text = "Process Card Groups";
            this.btnProcess.UseVisualStyleBackColor = true;
            this.btnUndo.Location = new System.Drawing.Point(270, 6);
            this.btnUndo.Name = "btnUndo";
            this.btnUndo.Size = new System.Drawing.Size(40, 32);
            this.btnUndo.Text = "↺";
            this.btnUndo.Font = new System.Drawing.Font("Segoe UI Symbol", 15F, System.Drawing.FontStyle.Bold);
            this.btnUndo.UseVisualStyleBackColor = true;
            //
            // tvwGroups
            //
            this.tvwGroups.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tvwGroups.Name = "tvwGroups";
            //
            // grpQueue ("Cards to Process", full-width strip)
            //
            this.grpQueue.Controls.Add(this.flpQueue);
            this.grpQueue.Dock = System.Windows.Forms.DockStyle.Top;
            this.grpQueue.Height = 330;
            this.grpQueue.Name = "grpQueue";
            this.grpQueue.Padding = new System.Windows.Forms.Padding(6);
            this.grpQueue.Text = "Cards to Process";
            //
            // flpQueue (horizontal thumbnail strip - scrolls horizontally)
            //
            this.flpQueue.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flpQueue.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            this.flpQueue.WrapContents = false;
            this.flpQueue.AutoScroll = true;
            this.flpQueue.Name = "flpQueue";
            //
            // lblBanner
            //
            this.lblBanner.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.lblBanner.Font = new System.Drawing.Font("Microsoft Sans Serif", 22F, System.Drawing.FontStyle.Bold);
            this.lblBanner.Height = 60;
            this.lblBanner.Name = "lblBanner";
            this.lblBanner.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblBanner.Text = "";
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1300, 900);
            this.Controls.Add(this.tblBody);
            this.Controls.Add(this.grpQueue);
            this.Controls.Add(this.lblBanner);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "DWP Card Scan";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.tblBody.ResumeLayout(false);
            this.grpSelected.ResumeLayout(false);
            this.grpSelected.PerformLayout();
            this.tsCropTools.ResumeLayout(false);
            this.tsCropTools.PerformLayout();
            this.pnlCardType.ResumeLayout(false);
            this.pnlCardType.PerformLayout();
            this.pnlMiddle.ResumeLayout(false);
            this.grpCardGroup.ResumeLayout(false);
            this.grpCardGroup.PerformLayout();
            this.pnlScan.ResumeLayout(false);
            this.grpGroups.ResumeLayout(false);
            this.pnlGroupTop.ResumeLayout(false);
            this.grpQueue.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.TableLayoutPanel tblBody;
        private System.Windows.Forms.GroupBox grpSelected;
        private DWPCardScanNet.CardPreviewControl cardPreview;
        private System.Windows.Forms.ToolStrip tsCropTools;
        private System.Windows.Forms.ToolStripButton tsbMoveCrop;
        private System.Windows.Forms.ToolStripButton tsbZoom;
        private System.Windows.Forms.ToolStripButton tsbRotate;
        private System.Windows.Forms.ToolStripButton tsbResetCrops;
        private System.Windows.Forms.ToolStripButton tsbDelete;
        private System.Windows.Forms.Panel pnlCardType;
        private System.Windows.Forms.Label lblCardType;
        private System.Windows.Forms.ComboBox cmbCardType;
        private System.Windows.Forms.Panel pnlMiddle;
        private System.Windows.Forms.Panel pnlScan;
        private System.Windows.Forms.Button btnScanCards;
        private System.Windows.Forms.GroupBox grpCardGroup;
        private System.Windows.Forms.Label lblKeyPrompt;
        private System.Windows.Forms.Label lblKeyPrefix;
        private System.Windows.Forms.TextBox txtKeyValue;
        private System.Windows.Forms.Button btnAddGroup;
        private System.Windows.Forms.GroupBox grpGroups;
        private System.Windows.Forms.TreeView tvwGroups;
        private System.Windows.Forms.Panel pnlGroupTop;
        private System.Windows.Forms.Button btnProcess;
        private System.Windows.Forms.Button btnUndo;
        private System.Windows.Forms.GroupBox grpQueue;
        private System.Windows.Forms.FlowLayoutPanel flpQueue;
        private System.Windows.Forms.Label lblBanner;
    }
}
