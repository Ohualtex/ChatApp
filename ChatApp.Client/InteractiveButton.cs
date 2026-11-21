using System.Drawing.Drawing2D;

namespace ChatApp.Client;

internal class InteractiveButton : Button
{
    private bool hovered, pointerDown, keyDown;
    protected bool Hovered => Enabled && hovered;
    protected bool Pressed => Enabled && ((pointerDown && hovered) || keyDown);
    protected bool KeyboardFocused => Focused && base.ShowFocusCues;
    protected override bool ShowFocusCues => false;

    public InteractiveButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
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

    protected override void OnMouseMove(MouseEventArgs e)
    {
        bool inside = ClientRectangle.Contains(e.Location);
        if (inside != hovered) { hovered = inside; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (Enabled && e.Button == MouseButtons.Left) { pointerDown = true; hovered = ClientRectangle.Contains(e.Location); }
        base.OnMouseDown(e);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) pointerDown = false;
        base.OnMouseUp(e);
        Invalidate();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        if (!Capture) pointerDown = false;
        base.OnMouseCaptureChanged(e);
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Enabled && e.KeyCode == Keys.Space) keyDown = true;
        base.OnKeyDown(e);
        Invalidate();
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space) keyDown = false;
        base.OnKeyUp(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        pointerDown = keyDown = false;
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }

    protected override void OnEnabledChanged(EventArgs e)
    {
        if (!Enabled) hovered = pointerDown = keyDown = false;
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        if (!Visible) hovered = pointerDown = keyDown = false;
        base.OnVisibleChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? BackColor);
        if (Width <= 2 || Height <= 2) return;
        float inset = 1;
        float diameter = Math.Min(12 * DeviceDpi / 96f, Math.Min(Width, Height) - 2);
        using var shape = new GraphicsPath();
        shape.AddArc(inset, inset, diameter, diameter, 180, 90);
        shape.AddArc(Width - diameter - inset, inset, diameter, diameter, 270, 90);
        shape.AddArc(Width - diameter - inset, Height - diameter - inset, diameter, diameter, 0, 90);
        shape.AddArc(inset, Height - diameter - inset, diameter, diameter, 90, 90);
        shape.CloseFigure();
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color surface = Pressed ? Mix(BackColor, Color.Black, .18f)
            : Hovered || KeyboardFocused ? Mix(BackColor, Color.White, .1f) : BackColor;
        using var fill = new SolidBrush(surface);
        e.Graphics.FillPath(fill, shape);
        if (KeyboardFocused)
        {
            using var pen = new Pen(ChatTheme.Muted);
            e.Graphics.DrawPath(pen, shape);
        }
        Rectangle text = ClientRectangle;
        if (Pressed) text.Offset(0, Math.Max(1, (int)Math.Round(DeviceDpi / 96d)));
        TextRenderer.DrawText(e.Graphics, Text, Font, text, Enabled ? ForeColor : ChatTheme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
            | TextFormatFlags.EndEllipsis | (ShowKeyboardCues ? 0 : TextFormatFlags.HidePrefix));
    }

    private static Color Mix(Color from, Color to, float amount) => Color.FromArgb(
        (int)(from.R + (to.R - from.R) * amount), (int)(from.G + (to.G - from.G) * amount),
        (int)(from.B + (to.B - from.B) * amount));
}
