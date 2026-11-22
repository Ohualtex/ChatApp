using System.Drawing.Drawing2D;

namespace ChatApp.Client;

internal class InteractiveButton : Button
{
    private readonly InteractionFeedback interaction;
    protected bool Hovered => interaction.Hovered;
    protected bool Pressed => interaction.Pressed;
    protected bool KeyboardFocused => Focused && base.ShowFocusCues;
    protected override bool ShowFocusCues => false;

    public InteractiveButton()
    {
        interaction = new InteractionFeedback(this);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
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
            : KeyboardFocused ? Mix(BackColor, ChatTheme.FocusInk, .28f)
            : Hovered ? Mix(BackColor, Color.White, .1f) : BackColor;
        using var fill = new SolidBrush(surface);
        e.Graphics.FillPath(fill, shape);
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
