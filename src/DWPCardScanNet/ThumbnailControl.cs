using System;
using System.Drawing;
using System.Windows.Forms;

namespace DWPCardScanNet
{
    /// <summary>
    /// A single card thumbnail in the horizontal "Cards to Process" strip: the card image plus its
    /// filename, with a green frame when selected (matching the VB6 gallery). Raises Click when the
    /// thumbnail (or any part of it) is clicked.
    /// </summary>
    public sealed class ThumbnailControl : Panel
    {
        private readonly PictureBox _picture;
        private readonly Label _caption;
        private bool _selected;

        public string BmpPath { get; }

        public ThumbnailControl(Image image, string caption, string bmpPath)
        {
            BmpPath = bmpPath;
            Size = new Size(180, 236);
            Margin = new Padding(6);
            Padding = new Padding(4);       // the frame that turns green when selected
            BackColor = Color.White;

            _caption = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 20,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = caption,
                BackColor = Color.White
            };
            _picture = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = image,
                BackColor = Color.White
            };

            Controls.Add(_picture);
            Controls.Add(_caption);

            _picture.Click += (s, e) => OnClick(EventArgs.Empty);
            _caption.Click += (s, e) => OnClick(EventArgs.Empty);
        }

        public bool Selected
        {
            get { return _selected; }
            set
            {
                _selected = value;
                BackColor = value ? Color.Lime : Color.White;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _picture.Image != null) _picture.Image.Dispose();
            base.Dispose(disposing);
        }
    }
}
