using System.Drawing.Drawing2D;

namespace ChatApp.Client;

internal sealed class ConversationAvatar : Control
{
    private static readonly Font GroupFont = new("Segoe MDL2 Assets", 15);
    private static readonly Font InitialFont = new("Segoe UI", 12, FontStyle.Bold);
    private string title = "General";
    private bool group = true;

    public ConversationAvatar()
    {
        DoubleBuffered = true;
        BackColor = ChatTheme.Surface;
        TabStop = false;
    }

    public void ShowConversation(string name, bool shared)
    {
        AccessibleName = name + " avatar";
        if (title == name && group == shared) return;
        title = name;
        group = shared;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int size = Math.Max(1, Math.Min(Width, Height) - 1);
        var circle = new Rectangle((Width - size) / 2, (Height - size) / 2, size, size);
        using var fill = new SolidBrush(group ? Color.FromArgb(40, 48, 46) : Color.FromArgb(48, 50, 54));
        e.Graphics.FillEllipse(fill, circle);
        string glyph = group ? "\uE902" : System.Globalization.StringInfo.GetNextTextElement(title).ToUpperInvariant();
        TextRenderer.DrawText(e.Graphics, glyph, group ? GroupFont : InitialFont, circle, ChatTheme.Ink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
