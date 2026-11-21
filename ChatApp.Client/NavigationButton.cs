using System.Drawing.Drawing2D;

namespace ChatApp.Client;

internal sealed class NavigationButton : InteractiveButton
{
    private bool selected;

    public NavigationButton() => DoubleBuffered = true;

    public void SetSelected(bool value)
    {
        selected = value;
        AccessibleDescription = value ? "Current section" : "";
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? ChatTheme.Surface);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int diameter = Math.Max(1, Math.Min(Width, Height) - 2);
        var circle = new Rectangle((Width - diameter) / 2, (Height - diameter) / 2, diameter, diameter);
        if (selected || Hovered || KeyboardFocused || Pressed)
        {
            using var fill = new SolidBrush(Pressed ? Color.FromArgb(25, 28, 27)
                : Hovered || KeyboardFocused ? Color.FromArgb(58, 61, 60) : ChatTheme.Raised);
            e.Graphics.FillEllipse(fill, circle);
        }
        Rectangle glyph = ClientRectangle;
        if (Pressed) glyph.Offset(0, Math.Max(1, (int)Math.Round(DeviceDpi / 96d)));
        TextRenderer.DrawText(e.Graphics, Text, Font, glyph,
            Enabled && (selected || Hovered || KeyboardFocused) ? ChatTheme.Ink : ChatTheme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        if (KeyboardFocused && Enabled)
        {
            using var pen = new Pen(ChatTheme.Muted);
            circle.Inflate(-2, -2);
            e.Graphics.DrawEllipse(pen, circle);
        }
    }
}
