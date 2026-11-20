using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ChatApp.Client;

internal sealed class MessageScrollBar : Control
{
    private int contentHeight;
    private int viewportHeight;
    private int value;
    private bool hovered;
    private bool dragging;
    private int grabOffset;
    private int MaximumOffset => Math.Max(0, contentHeight - viewportHeight);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal int Value
    {
        get => value;
        set
        {
            int next = Math.Clamp(value, 0, MaximumOffset);
            if (this.value == next) return;
            this.value = next;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? ValueChanged;
    public event MouseEventHandler? WheelRequested;

    public MessageScrollBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
        BackColor = Color.Transparent;
        TabStop = true;
        AccessibleName = "Scroll messages";
        AccessibleRole = AccessibleRole.ScrollBar;
    }

    internal void SetRange(int content, int viewport, int requestedValue)
    {
        contentHeight = Math.Max(0, content);
        viewportHeight = Math.Max(1, viewport);
        Value = requestedValue;
        Invalidate();
    }

    private int Scale(int pixels) => Math.Max(1, (int)Math.Round(pixels * DeviceDpi / 96d));

    internal Rectangle ThumbBounds()
    {
        if (MaximumOffset == 0 || Height < 2) return Rectangle.Empty;
        int inset = Math.Min(Scale(2), Height / 2);
        int trackHeight = Math.Max(1, Height - inset * 2);
        int minimum = Math.Min(trackHeight, Scale(28));
        int thumbHeight = Math.Clamp((int)Math.Round(trackHeight * (double)viewportHeight / contentHeight), minimum, trackHeight);
        int top = inset + (int)Math.Round((trackHeight - thumbHeight) * (double)value / MaximumOffset);
        int width = Math.Min(Width, Scale(hovered || dragging || Focused ? 7 : 4));
        return new Rectangle((Width - width) / 2, top, width, thumbHeight);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Rectangle thumb = ThumbBounds();
        if (thumb.IsEmpty || thumb.Width < 1) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = new GraphicsPath();
        int diameter = Math.Min(thumb.Width, thumb.Height);
        path.AddArc(thumb.Left, thumb.Top, diameter, diameter, 180, 180);
        path.AddArc(thumb.Left, thumb.Bottom - diameter, diameter, diameter, 0, 180);
        path.CloseFigure();
        using var fill = new SolidBrush(hovered || dragging || Focused
            ? Color.FromArgb(116, 126, 121) : Color.FromArgb(72, 79, 75));
        e.Graphics.FillPath(fill, path);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        hovered = true;
        base.OnMouseEnter(e);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hovered = false;
        base.OnMouseLeave(e);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || MaximumOffset == 0) return;
        Focus();
        Rectangle thumb = ThumbBounds();
        if (e.Y >= thumb.Top && e.Y < thumb.Bottom)
        {
            dragging = true;
            grabOffset = e.Y - thumb.Top;
            Capture = true;
        }
        else MoveBy(e.Y < thumb.Top ? -viewportHeight : viewportHeight);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!dragging) return;
        Rectangle thumb = ThumbBounds();
        int inset = Math.Min(Scale(2), Height / 2);
        int travel = Height - inset * 2 - thumb.Height;
        if (travel <= 0) return;
        double fraction = Math.Clamp((e.Y - grabOffset - inset) / (double)travel, 0, 1);
        Value = (int)Math.Round(fraction * MaximumOffset);
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
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e) => WheelRequested?.Invoke(this, e);

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode)
        is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Up: MoveBy(-Scale(24)); break;
            case Keys.Down: MoveBy(Scale(24)); break;
            case Keys.PageUp: MoveBy(-viewportHeight); break;
            case Keys.PageDown: MoveBy(viewportHeight); break;
            case Keys.Home: Value = 0; break;
            case Keys.End: Value = MaximumOffset; break;
            default: base.OnKeyDown(e); return;
        }
        e.Handled = e.SuppressKeyPress = true;
    }

    private void MoveBy(int amount) => Value = (int)Math.Clamp((long)value + amount, 0, MaximumOffset);

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override AccessibleObject CreateAccessibilityInstance() => new ScrollAccessibility(this);

    private sealed class ScrollAccessibility(MessageScrollBar owner) : ControlAccessibleObject(owner)
    {
        public override string? Value
        {
            get => owner.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            set
            {
                if (int.TryParse(value, out int offset)) owner.Value = offset;
            }
        }
        public override string? DefaultAction => "Scroll down";
        public override void DoDefaultAction() => owner.MoveBy(owner.Scale(24));
    }
}
