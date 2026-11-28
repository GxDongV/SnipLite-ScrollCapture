using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SnipLite.Plugin.Abstractions;

namespace SnipLite.ScrollCapture;

internal sealed class ScrollProgressPreview : ScrollCaptureOverlay
{
    private readonly string _heightLabel;
    private readonly string _tailLabel;
    private readonly Dictionary<int, Bitmap> _thumbnails = new();
    private readonly List<(int Index, int Top, int ContentHeight, int SourceHeight)> _visible = new();
    private int _imageWidth;
    private int _imageHeight;
    private int _stripCount;
    private Padding _edges;
    private int _lastHeight;
    private float _scale;
    private float _firstRow;

    internal int CapturedHeight => _imageHeight;
    internal int LatestHeight => _lastHeight;
    internal bool ShowsTail => _firstRow > 0;
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int noActivate = 0x08000000;
            var parameters = base.CreateParams;
            parameters.ExStyle |= noActivate;
            return parameters;
        }
    }

    internal ScrollProgressPreview(IPluginHost host)
    {
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        Text = new PluginText("Capture progress", "长截图进度", "長截圖進度").Get(host.Language);
        _heightLabel = new PluginText("Captured {0:N0} px", "已截 {0:N0} 像素", "已截 {0:N0} 像素").Get(host.Language);
        _tailLabel = new PluginText("Showing the end of the capture", "显示长图末尾", "顯示長圖末尾").Get(host.Language);
        host.StyleWindow(this);
    }

    private int Px(int logical) => (int)Math.Round(logical * DeviceDpi / 96f);

    private Rectangle ImageArea => new(Px(9), Px(36), Math.Max(1, ClientSize.Width - Px(18)),
        Math.Max(1, ClientSize.Height - Px(65)));

    internal void UpdateImage(ScrollImageStore store, Bitmap? latestFrame)
    {
        // A smaller count means Undo. Discard its thumbnail before an append can reuse the index.
        _imageWidth = store.Width;
        _imageHeight = store.Height;
        _stripCount = store.Count;
        _edges = store.FixedEdges;
        _lastHeight = _stripCount > 1 ? store.PreviewStrip(_stripCount - 1).Height : 0;
        AccessibleName = string.Format(_heightLabel, _imageHeight);
        _scale = Math.Min(1f, ImageArea.Width / (float)_imageWidth);
        _firstRow = Math.Max(0, _imageHeight - ImageArea.Height / _scale);

        _visible.Clear();
        var bottom = _imageHeight;
        for (var index = _stripCount - 1; index >= 0 && bottom > _firstRow; index--)
        {
            var (height, path) = store.PreviewStrip(index);
            var first = index == 0;
            var contentHeight = first ? height - _edges.Bottom : height;
            var visibleHeight = contentHeight + (index == _stripCount - 1 ? _edges.Bottom : 0);
            var sourceHeight = first ? height : height + _edges.Bottom;
            bottom -= visibleHeight;
            if (!_thumbnails.ContainsKey(index))
            {
                if (index == _stripCount - 1 && latestFrame is not null)
                    _thumbnails.Add(index, Reduce(latestFrame, sourceHeight));
                else
                {
                    using var strip = new Bitmap(path);
                    _thumbnails.Add(index, Reduce(strip, sourceHeight));
                }
            }
            _visible.Add((index, bottom, contentHeight, sourceHeight));
        }

        var firstVisible = _visible[^1].Index;
        foreach (var index in _thumbnails.Keys.Where(index => index < firstVisible || index >= _stripCount).ToArray())
        {
            _thumbnails[index].Dispose();
            _thumbnails.Remove(index);
        }
        Invalidate();
    }

    private static Bitmap Reduce(Bitmap source, int height)
    {
        var scale = Math.Min(1.0, 320.0 / source.Width);
        var thumbnail = new Bitmap(Math.Max(1, (int)Math.Round(source.Width * scale)),
            Math.Max(1, (int)Math.Round(height * scale)), PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics = Graphics.FromImage(thumbnail);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            graphics.DrawImage(source, new Rectangle(Point.Empty, thumbnail.Size), 0, source.Height - height,
                source.Width, height, GraphicsUnit.Pixel);
            return thumbnail;
        }
        catch { thumbnail.Dispose(); throw; }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var palette = ScrollPalette.For(this);
        var graphics = e.Graphics;
        graphics.Clear(palette.Card);
        using var border = new Pen(palette.Border);
        graphics.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        if (_stripCount == 0) return;

        var area = ImageArea;
        var width = _imageWidth * _scale;
        var x = area.X + (area.Width - width) / 2;
        var imageBounds = new RectangleF(x, area.Y, width, Math.Min(area.Height, _imageHeight * _scale));
        TextRenderer.DrawText(graphics, AccessibleName, Font, new Rectangle(area.X, Px(6), area.Width, Px(25)),
            palette.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if (ShowsTail)
            TextRenderer.DrawText(graphics, _tailLabel, Font, new Rectangle(area.X, area.Bottom + Px(3), area.Width, Px(21)),
                palette.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        var state = graphics.Save();
        graphics.SetClip(imageBounds);
        graphics.FillRectangle(Brushes.White, imageBounds);
        graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
        foreach (var (index, top, bodyHeight, sourceHeight) in _visible)
        {
            var image = _thumbnails[index];
            var isFirst = index == 0;
            var y = area.Y + (top - _firstRow) * _scale;
            var bodyBottom = y + bodyHeight * _scale;
            var drawState = graphics.Save();
            var inset = isFirst ? 0 : _edges.Left * _scale;
            var horizontal = isFirst ? 0 : _edges.Horizontal * _scale;
            graphics.IntersectClip(new RectangleF(x + inset, y, width - horizontal, bodyHeight * _scale));
            DrawStrip(image, y, sourceHeight);
            graphics.Restore(drawState);
            if (index == _stripCount - 1 && _edges.Bottom > 0)
            {
                drawState = graphics.Save();
                graphics.IntersectClip(new RectangleF(x, bodyBottom, width, _edges.Bottom * _scale));
                DrawStrip(image, y, sourceHeight);
                graphics.Restore(drawState);
            }
        }

        if (_lastHeight > 0)
        {
            var end = area.Y + (_imageHeight - _firstRow - _edges.Bottom) * _scale;
            if (end > area.Y)
            {
                var start = Math.Max(area.Y, end - Math.Max(Px(3), _lastHeight * _scale));
                using var mark = new SolidBrush(palette.Accent);
                using var line = new Pen(palette.Accent, Math.Max(1, Px(1)));
                graphics.FillRectangle(mark, x, start, Px(4), end - start);
                graphics.DrawLine(line, x, end - 1, x + width, end - 1);
            }
        }
        graphics.Restore(state);

        void DrawStrip(Bitmap image, float y, int sourceHeight) =>
            graphics.DrawImage(image, new RectangleF(x, y, width, sourceHeight * _scale),
                new RectangleF(0, 0, image.Width, image.Height), GraphicsUnit.Pixel);
    }

    internal static Rectangle? FindBounds(Rectangle selection, Rectangle toolbar, IEnumerable<Rectangle> workAreas, int dpi)
    {
        int Scale(int value) => (int)Math.Round(value * dpi / 96f);
        var gap = Scale(9);
        var width = Scale(194);
        var minimumHeight = Scale(142);
        var maximumHeight = Scale(410);
        var areas = workAreas.ToArray();
        foreach (var placeRight in new[] { true, false })
        foreach (var area in areas)
        {
            var x = placeRight ? Math.Max(area.Left, selection.Right + gap) : Math.Min(area.Right, selection.Left - gap) - width;
            if (x < area.Left || x + width > area.Right) continue;
            var column = new Rectangle(x, area.Top, width, area.Height);
            var blocked = Rectangle.Intersect(column, Rectangle.Inflate(toolbar, gap, gap));
            var slots = blocked.IsEmpty
                ? new[] { column }
                : new[] { Rectangle.FromLTRB(x, area.Top, x + width, blocked.Top), Rectangle.FromLTRB(x, blocked.Bottom, x + width, area.Bottom) };
            foreach (var slot in slots.Where(slot => slot.Height >= minimumHeight)
                         .OrderBy(slot => Math.Abs(selection.Top - Math.Clamp(selection.Top, slot.Top, slot.Bottom - minimumHeight))))
            {
                var height = Math.Min(maximumHeight, slot.Height);
                var y = Math.Clamp(selection.Top, slot.Top, slot.Bottom - height);
                return new Rectangle(x, y, width, height);
            }
        }
        foreach (var placeBelow in new[] { true, false })
        foreach (var area in areas)
        {
            var top = placeBelow ? Math.Max(area.Top, selection.Bottom + gap) : area.Top;
            var bottom = placeBelow ? area.Bottom : Math.Min(area.Bottom, selection.Top - gap);
            if (bottom - top < minimumHeight || area.Width < width) continue;
            var height = Math.Min(maximumHeight, bottom - top);
            var y = placeBelow ? top : bottom - height;
            var preferredX = Math.Clamp(selection.Right - width, area.Left, area.Right - width);
            var avoid = Rectangle.Inflate(toolbar, gap, gap);
            foreach (var x in new[] { preferredX, avoid.Right, avoid.Left - width })
            {
                var candidate = new Rectangle(x, y, width, height);
                if (area.Contains(candidate) && !avoid.IntersectsWith(candidate)) return candidate;
            }
        }
        return null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var thumbnail in _thumbnails.Values) thumbnail.Dispose();
            _thumbnails.Clear();
        }
        base.Dispose(disposing);
    }
}
