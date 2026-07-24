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
    /// Displays a scanned card with the selected card type's crop regions overlaid in red. The
    /// operator can drag the crop regions (Edit mode), pan, zoom (mouse wheel or Zoom tool), and
    /// rotate the view. A single GDI+ transform (pan * rotate * zoom) is applied to both the image
    /// and the overlays, and inverted for hit-testing, so crop editing works in any view state.
    /// GDI+ replacement for the VB6 LEAD display / CardScanManualAx.
    /// </summary>
    public sealed class CardPreviewControl : Panel
    {
        public enum InteractionMode { EditCrops, Pan, Zoom }

        private Image _display;
        private float _cardWidthInches = 8f;
        private float _cardHeightInches = 8f;
        private readonly List<CropRegion> _regions = new List<CropRegion>();
        private const int MaxDisplayDim = 1200;

        // View transform state.
        private float _zoom = 1f;
        private float _panX, _panY;
        private int _rotation;                  // 0 / 90 / 180 / 270

        // Interaction state.
        private InteractionMode _mode = InteractionMode.EditCrops;
        private int _dragIndex = -1;            // crop being dragged (Edit mode)
        private PointF _grabOffsetInches;
        private bool _panning;
        private Point _panStart;

        public CardPreviewControl()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(40, 40, 40);
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
        }

        public InteractionMode Mode
        {
            get { return _mode; }
            set { _mode = value; UpdateCursor(); }
        }

        public void RotateView() { _rotation = (_rotation + 90) % 360; Invalidate(); }
        public void ResetView() { _zoom = 1f; _panX = 0; _panY = 0; _rotation = 0; Invalidate(); }

        public void SetCard(string bmpPath, float cardWidthInches, float cardHeightInches, IEnumerable<CropRegion> regions)
        {
            DisposeImage();
            _regions.Clear();
            _dragIndex = -1;
            ResetView();
            _cardWidthInches = cardWidthInches > 0 ? cardWidthInches : 8f;
            _cardHeightInches = cardHeightInches > 0 ? cardHeightInches : 8f;

            if (!string.IsNullOrEmpty(bmpPath) && File.Exists(bmpPath))
            {
                byte[] bytes = File.ReadAllBytes(bmpPath);   // read so the file stays unlocked
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
            ResetView();
            Invalidate();
        }

        public IReadOnlyList<CropRegion> GetAdjustedRegions() => _regions;

        /// <summary>Screen X (in <paramref name="target"/>'s client space) of the card's home center (ignores zoom/pan).</summary>
        public int GetImageCenterX(Control target)
        {
            Point screenPt = PointToScreen(new Point(ClientSize.Width / 2, 0));
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

            BaseFitSize(out float baseW, out float baseH);
            using (Matrix m = BuildTransform())
            {
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.Transform = m;
                e.Graphics.DrawImage(_display, -baseW / 2f, -baseH / 2f, baseW, baseH);

                float penW = 1.6f / _zoom;   // keep the outline ~constant on screen
                using (var pen = new Pen(Color.Red, penW))
                using (var activePen = new Pen(Color.Cyan, penW * 1.4f))
                using (var brush = new SolidBrush(Color.Red))
                using (var font = new Font(Font.FontFamily, Math.Max(6f, 9f / _zoom)))
                {
                    for (int i = 0; i < _regions.Count; i++)
                    {
                        RectangleF rc = CropLocalRect(_regions[i], baseW, baseH);
                        e.Graphics.DrawRectangle(i == _dragIndex ? activePen : pen, rc.X, rc.Y, rc.Width, rc.Height);
                        e.Graphics.DrawString(_regions[i].FingerIndex.ToString(), font, brush, rc.X + 1, rc.Y + 1);
                    }
                }
                e.Graphics.ResetTransform();
            }
        }

        // ---- mouse ----------------------------------------------------------------------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();   // so the mouse wheel zooms this control
            if (_display == null) return;

            if (_mode == InteractionMode.Zoom)
            {
                ZoomAt(e.Location, e.Button == MouseButtons.Right ? 1f / 1.25f : 1.25f);
                return;
            }
            if (_mode == InteractionMode.Pan)
            {
                if (e.Button == MouseButtons.Left) { _panning = true; _panStart = e.Location; }
                return;
            }

            // Edit mode: pick the topmost crop region under the cursor.
            if (e.Button != MouseButtons.Left) return;
            PointF inch = PointToInches(e.Location);
            for (int i = _regions.Count - 1; i >= 0; i--)
            {
                if (InchInRegion(_regions[i], inch))
                {
                    _dragIndex = i;
                    _grabOffsetInches = new PointF(inch.X - _regions[i].Left, inch.Y - _regions[i].Top);
                    Invalidate();
                    return;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_display == null) return;

            if (_panning)
            {
                _panX += e.X - _panStart.X;
                _panY += e.Y - _panStart.Y;
                _panStart = e.Location;
                Invalidate();
                return;
            }

            if (_dragIndex >= 0)
            {
                PointF inch = PointToInches(e.Location);
                CropRegion r = _regions[_dragIndex];
                r.Left = Clamp(inch.X - _grabOffsetInches.X, 0f, _cardWidthInches - r.Width);
                r.Top = Clamp(inch.Y - _grabOffsetInches.Y, 0f, _cardHeightInches - r.Height);
                Invalidate();
                return;
            }

            if (_mode == InteractionMode.EditCrops)
            {
                PointF inch = PointToInches(e.Location);
                bool over = false;
                for (int i = _regions.Count - 1; i >= 0; i--)
                    if (InchInRegion(_regions[i], inch)) { over = true; break; }
                Cursor = over ? Cursors.SizeAll : Cursors.Default;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _panning = false;
            if (_dragIndex >= 0) { _dragIndex = -1; Invalidate(); }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_display == null) return;
            ZoomAt(e.Location, e.Delta > 0 ? 1.15f : 1f / 1.15f);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            ResetView();   // double-click resets zoom/pan/rotation to fit
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            Invalidate();
        }

        // ---- transform / coordinate mapping ---------------------------------------------

        private void BaseFitSize(out float w, out float h)
        {
            w = 0; h = 0;
            if (_display == null || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            float sc = Math.Min((float)ClientSize.Width / _display.Width, (float)ClientSize.Height / _display.Height);
            w = _display.Width * sc;
            h = _display.Height * sc;
        }

        /// <summary>Card-model (image local, origin at image center) -> control pixels.</summary>
        private Matrix BuildTransform()
        {
            var m = new Matrix();
            m.Translate(ClientSize.Width / 2f + _panX, ClientSize.Height / 2f + _panY);
            m.Rotate(_rotation);
            m.Scale(_zoom, _zoom);
            return m;
        }

        private RectangleF CropLocalRect(CropRegion r, float baseW, float baseH)
        {
            float x = -baseW / 2f + (r.Left / _cardWidthInches) * baseW;
            float y = -baseH / 2f + (r.Top / _cardHeightInches) * baseH;
            float w = (r.Width / _cardWidthInches) * baseW;
            float h = (r.Height / _cardHeightInches) * baseH;
            return new RectangleF(x, y, w, h);
        }

        private PointF PointToInches(Point screen)
        {
            BaseFitSize(out float baseW, out float baseH);
            if (baseW <= 0 || baseH <= 0) return PointF.Empty;
            using (Matrix m = BuildTransform())
            {
                m.Invert();
                var pts = new[] { new PointF(screen.X, screen.Y) };
                m.TransformPoints(pts);
                float left = (pts[0].X + baseW / 2f) / baseW * _cardWidthInches;
                float top = (pts[0].Y + baseH / 2f) / baseH * _cardHeightInches;
                return new PointF(left, top);
            }
        }

        private void ZoomAt(Point cursor, float factor)
        {
            PointF localBefore = ToLocal(cursor);
            float newZoom = Clamp(_zoom * factor, 1f, 12f);
            if (Math.Abs(newZoom - _zoom) < 0.0001f) return;
            _zoom = newZoom;
            PointF screenAfter = FromLocal(localBefore);   // where the same point maps now
            _panX += cursor.X - screenAfter.X;             // shift so it stays under the cursor
            _panY += cursor.Y - screenAfter.Y;
            Invalidate();
        }

        private PointF ToLocal(Point screen)
        {
            using (Matrix m = BuildTransform())
            {
                m.Invert();
                var p = new[] { new PointF(screen.X, screen.Y) };
                m.TransformPoints(p);
                return p[0];
            }
        }

        private PointF FromLocal(PointF local)
        {
            using (Matrix m = BuildTransform())
            {
                var p = new[] { local };
                m.TransformPoints(p);
                return p[0];
            }
        }

        private bool InchInRegion(CropRegion r, PointF inch) =>
            inch.X >= r.Left && inch.X <= r.Left + r.Width && inch.Y >= r.Top && inch.Y <= r.Top + r.Height;

        private void UpdateCursor()
        {
            switch (_mode)
            {
                case InteractionMode.Pan: Cursor = Cursors.Hand; break;
                case InteractionMode.Zoom: Cursor = Cursors.Cross; break;
                default: Cursor = Cursors.Default; break;
            }
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
