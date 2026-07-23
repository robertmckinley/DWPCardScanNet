using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DWPCardScanNet
{
    /// <summary>
    /// Shows a group status report file's contents. Port of the VB6 frmStatusReport.
    /// </summary>
    public sealed class StatusReportForm : Form
    {
        private readonly TextBox _text;

        public StatusReportForm()
        {
            _text = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font("Consolas", 9F)
            };
            Controls.Add(_text);
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(640, 480);
            Text = "Group Status Report";
        }

        public void Display(string reportPath)
        {
            if (string.IsNullOrEmpty(reportPath) || !File.Exists(reportPath))
            {
                _text.Text = "Report not found: " + reportPath;
            }
            else
            {
                Text = "Group Status Report - " + Path.GetFileName(reportPath);
                try { _text.Text = File.ReadAllText(reportPath); }
                catch (Exception ex) { _text.Text = "Could not read report:" + Environment.NewLine + ex; }
            }
            ShowDialog();
        }
    }
}
