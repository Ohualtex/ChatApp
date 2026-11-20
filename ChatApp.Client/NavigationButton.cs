using System.Drawing.Drawing2D;

namespace ChatApp.Client;

internal sealed class NavigationButton : Button
{
    private bool selected;
    private bool hovered;

    public NavigationButton() => DoubleBuffered = true;

    public void SetSelected(bool value)
    {
        selected = value;
        AccessibleDescription = value ? "Current section" : "";
        Invalidate();
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

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? ChatTheme.Surface);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int diameter = Math.Max(1, Math.Min(Width, Height) - 2);
        var circle = new Rectangle((Width - diameter) / 2, (Height - diameter) / 2, diameter, diameter);
        if (selected || hovered)
        {
            using var fill = new SolidBrush(selected ? ChatTheme.Raised : Color.FromArgb(38, 40, 40));
            e.Graphics.FillEllipse(fill, circle);
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle,
            Enabled && (selected || hovered) ? ChatTheme.Ink : ChatTheme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        if (Focused && ShowFocusCues)
        {
            using var pen = new Pen(ChatTheme.Muted) { DashStyle = DashStyle.Dot };
            circle.Inflate(-2, -2);
            e.Graphics.DrawEllipse(pen, circle);
        }
    }
}
