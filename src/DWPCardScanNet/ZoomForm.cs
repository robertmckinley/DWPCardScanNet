using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DWPCardScanNet
{
    /// <summary>
    /// Full-window image viewer (fit-to-window). GDI+ replacement for the VB6 frmZoomImage
    /// (which used LEADTOOLS). The magnifier-glass tool is not ported.
    /// </summary>
    public sealed class ZoomForm : Form
    {
        private readonly PictureBox _picture;

        public ZoomForm()
        {
            _picture = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };
            Controls.Add(_picture);

            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(800, 800);
            Text = "Zoom";
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        }

        public void Display(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                MessageBox.Show("Image not found: " + imagePath, "Zoom", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Text = "Zoom - " + Path.GetFileName(imagePath);
            // Load without locking the file.
            byte[] bytes = File.ReadAllBytes(imagePath);
            var img = Image.FromStream(new MemoryStream(bytes));
            _picture.Image?.Dispose();
            _picture.Image = img;
            ShowDialog();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _picture.Image?.Dispose();
            base.Dispose(disposing);
        }
    }
}
