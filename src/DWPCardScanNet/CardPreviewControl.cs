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
    /// Displays a scanned card with the selected card type's crop regions overlaid in red. GDI+
    /// replacement for the VB6 LEAD display / CardScanManualAx, with feature parity for crop editing:
    /// <list type="bullet">
    /// <item>Edit mode: left-drag a crop to move it; right-click to recenter it on the cursor. A move
    /// only acts when the cursor is inside exactly one movable region; the data header (index 15) is
    /// never movable.</item>
    /// <item><see cref="ResetCropRegions"/> restores all crops to their originally defined positions.</item>
    /// <item>Zoom mode: drag a marquee box to zoom into it. Mouse wheel also zooms; pan and 90&#176;
    /// view rotation are available as modes.</item>
    /// </list>
    /// A single GDI+ transform (pan * rotate * zoom) is applied to both the image and the overlays, and
    /// inverted for hit-testing, so crop editing works in any view state.
    /// </summary>
    public sealed class CardPreviewControl : Panel
    {
        public enum InteractionMode { EditCrops, Pan, Zoom }

        private Image _display;
        private float _cardWidthInches = 8f;
        private float _cardHeightInches = 8f;
        private readonly List<CropRegion> _regions = new List<CropRegion>();
        private readonly List<CropRegion> _originalRegions = new List<CropRegion>(); // as first supplied, for Reset
        private const int MaxDisplayDim = 1200;

        /// <summary>Aware finger index of the Type-7 data-header region; it is drawn but never movable
        /// (matches the legacy CardScanManualAx, which excluded index 15 from crop moves).</summary>
        private const int DataHeaderFingerIndex = 15;

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

        // Marquee-zoom state (Zoom mode: drag a box, zoom into it - like the legacy Zoom tool).
        private bool _marquee;
        private Point _marqueeStart;
        private Point _marqueeCurrent;

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

            _originalRegions.Clear();
            if (regions != null)
                foreach (CropRegion r in regions)
                {
                    _regions.Add(Clone(r));
                    _originalRegions.Add(Clone(r));   // snapshot for ResetCropRegions
                }

            Invalidate();
        }

        public void ClearCard()
        {
            DisposeImage();
            _regions.Clear();
            _originalRegions.Clear();
            _dragIndex = -1;
            ResetView();
            Invalidate();
        }

        /// <summary>Restores every crop region to the position it had when the card was loaded,
        /// undoing manual moves (port of the legacy Reset button in CropMove mode). Leaves the view
        /// transform - use <see cref="ResetView"/> for that.</summary>
        public void ResetCropRegions()
        {
            _dragIndex = -1;
            _regions.Clear();
            foreach (CropRegion r in _originalRegions) _regions.Add(Clone(r));
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

            // Marquee is drawn in screen space, on top of the (transformed) image.
            if (_marquee)
            {
                Rectangle box = MarqueeRect();
                using (var mp = new Pen(Color.White) { DashStyle = DashStyle.Dash })
                    e.Graphics.DrawRectangle(mp, box);
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
                // Left-drag a marquee to zoom into that box (legacy Zoom tool). Wheel still zooms too.
                if (e.Button == MouseButtons.Left)
                {
                    _marquee = true;
                    _marqueeStart = e.Location;
                    _marqueeCurrent = e.Location;
                }
                return;
            }
            if (_mode == InteractionMode.Pan)
            {
                if (e.Button == MouseButtons.Left) { _panning = true; _panStart = e.Location; }
                return;
            }

            // Edit mode. The data header (index 15) is not movable, and a move only starts when the
            // cursor is inside exactly one movable region (no ambiguous grab) - matching the legacy control.
            PointF inch = PointToInches(e.Location);
            int idx = MovableRegionAt(inch);
            if (idx < 0) return;

            if (e.Button == MouseButtons.Left)
            {
                _dragIndex = idx;
                _grabOffsetInches = new PointF(inch.X - _regions[idx].Left, inch.Y - _regions[idx].Top);
                Invalidate();
            }
            else if (e.Button == MouseButtons.Right)
            {
                // Right-click recenters the region on the cursor (legacy quick-reposition).
                CenterRegionOn(idx, inch);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_display == null) return;

            if (_marquee)
            {
                _marqueeCurrent = e.Location;
                Invalidate();
                return;
            }

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
                Cursor = MovableRegionAt(inch) >= 0 ? Cursors.SizeAll : Cursors.Default;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _panning = false;
            if (_marquee)
            {
                _marquee = false;
                ZoomToScreenRect(MarqueeRect());
            }
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

        /// <summary>Index of the single movable region under <paramref name="inch"/>, or -1 if none or
        /// more than one. The data-header region (index 15) is never movable. Matches the legacy control:
        /// it only acted when the cursor was inside exactly one non-header rectangle.</summary>
        private int MovableRegionAt(PointF inch)
        {
            int found = -1, count = 0;
            for (int i = 0; i < _regions.Count; i++)
            {
                if (_regions[i].FingerIndex == DataHeaderFingerIndex) continue;
                if (InchInRegion(_regions[i], inch)) { count++; found = i; }
            }
            return count == 1 ? found : -1;
        }

        private void CenterRegionOn(int idx, PointF inch)
        {
            CropRegion r = _regions[idx];
            r.Left = Clamp(inch.X - r.Width / 2f, 0f, _cardWidthInches - r.Width);
            r.Top = Clamp(inch.Y - r.Height / 2f, 0f, _cardHeightInches - r.Height);
            Invalidate();
        }

        /// <summary>The current marquee as a normalised client-space rectangle.</summary>
        private Rectangle MarqueeRect()
        {
            int x = Math.Min(_marqueeStart.X, _marqueeCurrent.X);
            int y = Math.Min(_marqueeStart.Y, _marqueeCurrent.Y);
            int w = Math.Abs(_marqueeCurrent.X - _marqueeStart.X);
            int h = Math.Abs(_marqueeCurrent.Y - _marqueeStart.Y);
            return new Rectangle(x, y, w, h);
        }

        /// <summary>Zooms so the given client-space box fills the view, keeping its centre centred
        /// (legacy Zoom tool). Ignored for a too-small box (treated as a click, not a drag).</summary>
        private void ZoomToScreenRect(Rectangle box)
        {
            if (box.Width < 6 || box.Height < 6) return;
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            PointF centerLocal = ToLocal(new Point(box.X + box.Width / 2, box.Y + box.Height / 2));
            float s = Math.Min((float)ClientSize.Width / box.Width, (float)ClientSize.Height / box.Height);
            float newZoom = Clamp(_zoom * s, 1f, 12f);
            if (Math.Abs(newZoom - _zoom) < 0.0001f) { Invalidate(); return; }
            _zoom = newZoom;

            PointF screenAfter = FromLocal(centerLocal);
            _panX += ClientSize.Width / 2f - screenAfter.X;
            _panY += ClientSize.Height / 2f - screenAfter.Y;
            Invalidate();
        }

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
