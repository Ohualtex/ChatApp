using System.Drawing.Drawing2D;

namespace ChatApp.Client;

internal sealed class NavigationButton : InteractiveButton
{
    private readonly bool useChatIcon;
    private bool selected;

    public NavigationButton(bool useChatIcon = false)
    {
        this.useChatIcon = useChatIcon;
        DoubleBuffered = true;
    }

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
                : selected || KeyboardFocused ? ChatTheme.Raised : Color.FromArgb(58, 61, 60));
            e.Graphics.FillEllipse(fill, circle);
        }
        Rectangle glyph = ClientRectangle;
        if (Pressed) glyph.Offset(0, Math.Max(1, (int)Math.Round(DeviceDpi / 96d)));
        Color iconColor = Enabled && (selected || Hovered || KeyboardFocused) ? ChatTheme.Ink : ChatTheme.Muted;
        if (useChatIcon)
            DrawChatIcon(e.Graphics, glyph, iconColor);
        else
            DrawSettingsIcon(e.Graphics, glyph, iconColor);
    }

    private void DrawChatIcon(Graphics graphics, Rectangle bounds, Color color)
    {
        float scale = DeviceDpi / 96f;
        float left = bounds.X + (bounds.Width - 22 * scale) / 2f;
        float top = bounds.Y + (bounds.Height - 18 * scale) / 2f;
        PointF P(float x, float y) => new(left + x * scale, top + y * scale);
        RectangleF Slot(float x, float y, float width)
            => new(left + x * scale, top + y * scale, width * scale, 1.85f * scale);

        using var icon = new GraphicsPath(FillMode.Alternate);
        icon.AddLine(P(1.5f, 2), P(19.4f, 2));
        icon.AddBezier(P(19.4f, 2), P(20.2f, 2), P(20.7f, 2.5f), P(20.7f, 3.3f));
        icon.AddLine(P(20.7f, 3.3f), P(20.7f, 14.8f));
        icon.AddBezier(P(20.7f, 14.8f), P(20.7f, 15.6f), P(20.2f, 16), P(19.4f, 16));
        icon.AddLine(P(19.4f, 16), P(4.8f, 16));
        icon.AddBezier(P(4.8f, 16), P(4, 16), P(3.6f, 15.6f), P(3.6f, 14.8f));
        icon.AddLine(P(3.6f, 14.8f), P(3.6f, 6));
        icon.CloseFigure();

        using var fill = new SolidBrush(color);
        if (selected)
        {
            SizeF corner = new(1.2f * scale, 1.2f * scale);
            icon.AddRoundedRectangle(Slot(7, 6, 10), corner);
            icon.AddRoundedRectangle(Slot(7, 10.3f, 6.7f), corner);
            graphics.FillPath(fill, icon);
            return;
        }

        using var outline = new Pen(color, 1.65f * scale) { LineJoin = LineJoin.Round };
        graphics.DrawPath(outline, icon);
        graphics.FillRectangle(fill, Slot(7, 6, 10));
        graphics.FillRectangle(fill, Slot(7, 10.3f, 6.7f));
    }

    private void DrawSettingsIcon(Graphics graphics, Rectangle bounds, Color color)
    {
        float scale = DeviceDpi / 96f;
        float centerX = bounds.Left + bounds.Width / 2f, centerY = bounds.Top + bounds.Height / 2f;
        const int teeth = 8;
        var points = new PointF[teeth * 4];
        for (int tooth = 0; tooth < teeth; tooth++)
        {
            points[tooth * 4] = PointOnGear(tooth - .34, 6.5f);
            points[tooth * 4 + 1] = PointOnGear(tooth - .22, 9.8f);
            points[tooth * 4 + 2] = PointOnGear(tooth + .22, 9.8f);
            points[tooth * 4 + 3] = PointOnGear(tooth + .34, 6.5f);
        }

        RectangleF HubBounds(float radius)
        {
            float scaledRadius = radius * scale;
            return new RectangleF(centerX - scaledRadius, centerY - scaledRadius,
                scaledRadius * 2, scaledRadius * 2);
        }
        using var outline = new Pen(color, 1.65f * scale) { LineJoin = LineJoin.Round };
        if (selected)
        {
            using var silhouette = new GraphicsPath(FillMode.Alternate);
            silhouette.AddPolygon(points);
            silhouette.AddEllipse(HubBounds(4.5f));
            using var fill = new SolidBrush(color);
            graphics.FillPath(fill, silhouette);
            graphics.FillEllipse(fill, HubBounds(2.1f));
        }
        else
        {
            graphics.DrawEllipse(outline, HubBounds(3.2f));
        }
        graphics.DrawPolygon(outline, points);

        PointF PointOnGear(double tooth, float radius)
        {
            double angle = tooth * (2 * Math.PI / teeth) - Math.PI / 2;
            return new PointF(centerX + (float)Math.Cos(angle) * radius * scale,
                centerY + (float)Math.Sin(angle) * radius * scale);
        }
    }
}
