using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using DWPCardScanNet.Core.Configuration;

namespace DWPCardScanNet
{
    /// <summary>
    /// Displays a scanned card scaled to fit, with the selected card type's crop regions drawn as
    /// overlays that the operator can DRAG to reposition (move-only, fixed size - matching the VB6
    /// CardScanManualAx behaviour). GDI+ replacement for the VB6 LEAD display / CardScanManualAx.
    /// The adjusted regions are read back via <see cref="GetAdjustedRegions"/> and written to a .crp.
    /// </summary>
    public sealed class CardPreviewControl : Panel
    {
        private Image _display;                 // scaled-down copy for display
        private float _cardWidthInches = 8f;
        private float _cardHeightInches = 8f;
        private readonly List<CropRegion> _regions = new List<CropRegion>(); // editable clones
        private const int MaxDisplayDim = 1000;

        private int _dragIndex = -1;
        private PointF _grabOffsetInches;        // mouse-inches minus region top-left, kept while dragging

        public CardPreviewControl()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(40, 40, 40);
        }

        /// <summary>Loads a card BMP (without locking the file) and clones the crop regions for editing.</summary>
        public void SetCard(string bmpPath, float cardWidthInches, float cardHeightInches, IEnumerable<CropRegion> regions)
        {
            DisposeImage();
            _regions.Clear();
            _dragIndex = -1;
            _cardWidthInches = cardWidthInches > 0 ? cardWidthInches : 8f;
            _cardHeightInches = cardHeightInches > 0 ? cardHeightInches : 8f;

            if (!string.IsNullOrEmpty(bmpPath) && File.Exists(bmpPath))
            {
                byte[] bytes = File.ReadAllBytes(bmpPath); // read so the file stays unlocked (may be moved later)
                using (var ms = new MemoryStream(bytes))
                using (var full = new Bitmap(ms))
                {
                    float scale = Math.Min(1f, (float)MaxDisplayDim / Math.Max(full.Width, full.Height));
                    int w = Math.Max(1, (int)(full.Width * scale));
                    int h = Math.Max(1, (int)(full.Height * scale));
                    var scaled = new Bitmap(w, h);
                    using (var g = Graphics.FromImage(scaled))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(full, 0, 0, w, h);
                    }
                    _display = scaled;
                }
            }

            if (regions != null)
                foreach (CropRegion r in regions) _regions.Add(Clone(r));

            Invalidate();
        }

        public void ClearCard()
        {
            DisposeImage();
            _regions.Clear();
            _dragIndex = -1;
            Invalidate();
        }

        /// <summary>The current crop regions, with any operator move adjustments applied.</summary>
        public IReadOnlyList<CropRegion> GetAdjustedRegions() => _regions;

        /// <summary>
        /// X coordinate (in <paramref name="target"/>'s client space) of the horizontal center of the
        /// drawn card image. Lets callers align controls (e.g. the Card Type combo) over the card.
        /// </summary>
        public int GetImageCenterX(Control target)
        {
            RectangleF r = ImageRect();
            Point screenPt = PointToScreen(new Point((int)Math.Round(r.X + r.Width / 2f), 0));
            return target.PointToClient(screenPt).X;
        }

        // ---- painting -------------------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_display == null)
            {
                TextRenderer.DrawText(e.Graphics, "No card selected", Font, ClientRectangle,
                    Color.Gainsboro, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            RectangleF dest = ImageRect();
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(_display, dest);

            using (var pen = new Pen(Color.Red, 1.5f))
            using (var activePen = new Pen(Color.Cyan, 2f))
            using (var brush = new SolidBrush(Color.Red))
            {
                for (int i = 0; i < _regions.Count; i++)
                {
                    RectangleF rc = RegionRect(_regions[i], dest);
                    e.Graphics.DrawRectangle(i == _dragIndex ? activePen : pen, rc.X, rc.Y, rc.Width, rc.Height);
                    e.Graphics.DrawString(_regions[i].FingerIndex.ToString(), Font, brush, rc.X + 1, rc.Y + 1);
                }
            }
        }

        // ---- dragging -------------------------------------------------------------------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || _display == null) return;

            RectangleF dest = ImageRect();
            // Topmost region under the cursor (search back-to-front so the last-drawn wins).
            for (int i = _regions.Count - 1; i >= 0; i--)
            {
                if (RegionRect(_regions[i], dest).Contains(e.Location))
                {
                    _dragIndex = i;
                    PointF m = PointToInches(e.Location, dest);
                    _grabOffsetInches = new PointF(m.X - _regions[i].Left, m.Y - _regions[i].Top);
                    Invalidate();
                    return;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_display == null) return;
            RectangleF dest = ImageRect();

            if (_dragIndex >= 0)
            {
                PointF m = PointToInches(e.Location, dest);
                CropRegion r = _regions[_dragIndex];
                r.Left = Clamp(m.X - _grabOffsetInches.X, 0f, _cardWidthInches - r.Width);
                r.Top = Clamp(m.Y - _grabOffsetInches.Y, 0f, _cardHeightInches - r.Height);
                Invalidate();
                return;
            }

            // Hover feedback.
            Cursor hover = Cursors.Default;
            for (int i = _regions.Count - 1; i >= 0; i--)
                if (RegionRect(_regions[i], dest).Contains(e.Location)) { hover = Cursors.SizeAll; break; }
            Cursor = hover;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_dragIndex >= 0) { _dragIndex = -1; Invalidate(); }
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            Invalidate();
        }

        // ---- coordinate mapping ---------------------------------------------------------

        /// <summary>The fitted destination rectangle of the card image within the control.</summary>
        private RectangleF ImageRect()
        {
            if (_display == null) return ClientRectangle;
            int iw = _display.Width, ih = _display.Height;
            Rectangle b = ClientRectangle;
            if (iw <= 0 || ih <= 0 || b.Width <= 0 || b.Height <= 0) return b;
            float scale = Math.Min((float)b.Width / iw, (float)b.Height / ih);
            float w = iw * scale, h = ih * scale;
            return new RectangleF(b.X + (b.Width - w) / 2f, b.Y + (b.Height - h) / 2f, w, h);
        }

        private RectangleF RegionRect(CropRegion r, RectangleF dest)
        {
            float x = dest.X + (r.Left / _cardWidthInches) * dest.Width;
            float y = dest.Y + (r.Top / _cardHeightInches) * dest.Height;
            float w = (r.Width / _cardWidthInches) * dest.Width;
            float h = (r.Height / _cardHeightInches) * dest.Height;
            return new RectangleF(x, y, w, h);
        }

        private PointF PointToInches(Point p, RectangleF dest)
        {
            float x = dest.Width > 0 ? (p.X - dest.X) / dest.Width * _cardWidthInches : 0f;
            float y = dest.Height > 0 ? (p.Y - dest.Y) / dest.Height * _cardHeightInches : 0f;
            return new PointF(x, y);
        }

        private static float Clamp(float v, float min, float max)
        {
            if (max < min) return min;
            return v < min ? min : (v > max ? max : v);
        }

        private static CropRegion Clone(CropRegion r) => new CropRegion
        {
            FingerIndex = r.FingerIndex,
            ListIndex = r.ListIndex,
            NistRecordIndex = r.NistRecordIndex,
            Top = r.Top,
            Left = r.Left,
            Width = r.Width,
            Height = r.Height,
            PxHeight = r.PxHeight,
            PxWidth = r.PxWidth,
            Desc = r.Desc,
            Rotation = r.Rotation
        };

        private void DisposeImage()
        {
            if (_display != null) { _display.Dispose(); _display = null; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) DisposeImage();
            base.Dispose(disposing);
        }
    }
}
