using System.Drawing.Drawing2D;

namespace ChatApp.Client;

internal sealed class ZoomImageCanvas : Control
{
    private readonly Image image;
    private double zoom = 1;
    private PointF pan, dragPan;
    private Point dragStart;
    private bool dragging;
    internal double ZoomFactor => zoom;
    internal double ZoomPercent => FitScale() * zoom * 100;
    public event EventHandler? ZoomChanged;

    public ZoomImageCanvas(Image image)
    {
        this.image = image;
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        BackColor = ChatTheme.Canvas;
        AccessibleRole = AccessibleRole.Graphic;
        AccessibleDescription = "Use the mouse wheel or plus and minus to zoom. Drag to pan. Home fits the image.";
    }

    private double FitScale() => Math.Max(.0001, Math.Min(ClientSize.Width / (double)image.Width, ClientSize.Height / (double)image.Height));

    internal RectangleF ImageBounds()
    {
        float width = (float)(image.Width * FitScale() * zoom);
        float height = (float)(image.Height * FitScale() * zoom);
        return new RectangleF((ClientSize.Width - width) / 2f + pan.X,
            (ClientSize.Height - height) / 2f + pan.Y, width, height);
    }

    internal void ZoomBy(double multiplier) => ZoomAt(multiplier, new Point(ClientSize.Width / 2, ClientSize.Height / 2));

    private void ZoomAt(double multiplier, Point anchor)
    {
        double next = Math.Clamp(zoom * multiplier, 1, 8);
        if (Math.Abs(next - zoom) < .00001) return;
        RectangleF before = ImageBounds();
        double relativeX = (anchor.X - before.Left) / before.Width;
        double relativeY = (anchor.Y - before.Top) / before.Height;
        zoom = next;
        RectangleF after = ImageBounds();
        pan.X += (float)(anchor.X - after.Left - relativeX * after.Width);
        pan.Y += (float)(anchor.Y - after.Top - relativeY * after.Height);
        ClampPan();
        NotifyChange();
    }

    internal void FitImage()
    {
        zoom = 1;
        pan = PointF.Empty;
        Capture = false;
        NotifyChange();
    }

    private void ClampPan()
    {
        double scale = FitScale() * zoom;
        float maxX = Math.Max(0, (float)(image.Width * scale - ClientSize.Width) / 2);
        float maxY = Math.Max(0, (float)(image.Height * scale - ClientSize.Height) / 2);
        pan.X = Math.Clamp(pan.X, -maxX, maxX);
        pan.Y = Math.Clamp(pan.Y, -maxY, maxY);
    }

    private void NotifyChange()
    {
        UpdateCursor();
        Invalidate();
        ZoomChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool CanPan() => zoom > 1.00001 && (ImageBounds().Width > ClientSize.Width || ImageBounds().Height > ClientSize.Height);
    private void UpdateCursor() => Cursor = dragging ? Cursors.SizeAll : CanPan() ? Cursors.Hand : Cursors.Default;

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (image is null) return;
        ClampPan();
        NotifyChange();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        e.Graphics.DrawImage(image, ImageBounds());
        if (Focused && ShowFocusCues && Width > 3 && Height > 3)
        {
            using var outline = new Pen(ChatTheme.Muted);
            e.Graphics.DrawRectangle(outline, 1, 1, Width - 3, Height - 3);
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        Focus();
        ZoomAt(Math.Pow(1.25, e.Delta / (double)SystemInformation.MouseWheelScrollDelta), e.Location);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        if (!CanPan()) return;
        dragging = true;
        dragStart = e.Location;
        dragPan = pan;
        Capture = true;
        UpdateCursor();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!dragging) return;
        pan = new PointF(dragPan.X + e.X - dragStart.X, dragPan.Y + e.Y - dragStart.Y);
        ClampPan();
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left) Capture = false;
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        dragging = false;
        base.OnMouseCaptureChanged(e);
        UpdateCursor();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Capture = false;
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        float step = 40 * DeviceDpi / 96f;
        switch (e.KeyCode)
        {
            case Keys.Left: pan.X += step; break;
            case Keys.Right: pan.X -= step; break;
            case Keys.Up: pan.Y += step; break;
            case Keys.Down: pan.Y -= step; break;
            default: base.OnKeyDown(e); return;
        }
        ClampPan();
        Invalidate();
        e.Handled = e.SuppressKeyPress = true;
    }
}
