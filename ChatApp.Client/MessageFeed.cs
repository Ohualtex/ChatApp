namespace ChatApp.Client;

internal sealed class MessageFeed : Panel
{
    private static readonly Color Canvas = Color.FromArgb(243, 241, 237);
    private static readonly Color SentFill = Color.FromArgb(217, 244, 229);
    private static readonly Color Ink = Color.FromArgb(23, 43, 37);
    private static readonly Color Muted = Color.FromArgb(79, 99, 90);
    private static readonly Font BodyFont = new("Segoe UI", 10);
    private static readonly Font MetaFont = new("Segoe UI", 8);
    private static readonly Font SenderFont = new("Segoe UI", 9, FontStyle.Bold);

    private int viewportHeight;

    public MessageFeed()
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;
        Padding = new Padding(18, 12, 18, 12);
        BackColor = Canvas;
        AccessibleName = "Chat messages";
        viewportHeight = ClientSize.Height;
    }

    public void ClearMessages()
    {
        SuspendLayout();
        try
        {
            foreach (Control row in Controls.Cast<Control>().ToArray())
            {
                row.Dispose();
            }
            AutoScrollMinSize = Size.Empty;
        }
        finally
        {
            ResumeLayout();
        }
    }

    public void AddMessage(string sender, string text, DateTimeOffset? timestamp, bool outgoing)
    {
        string time = timestamp?.ToLocalTime().ToString("HH:mm") ?? "";
        AddRow(new MessageRow(outgoing ? null : sender, text, time, outgoing));
    }

    public void AddNotice(string text) => AddRow(new MessageRow(null, text, "", outgoing: false, notice: true));

    private void AddRow(MessageRow row)
    {
        int offset = -AutoScrollPosition.Y;
        bool follow = offset >= BottomOffset() - 3;
        Controls.Add(row);
        LayoutRows();
        AutoScrollPosition = new Point(0, follow ? BottomOffset() : offset);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        int offset = -AutoScrollPosition.Y;
        bool follow = offset >= Math.Max(0, AutoScrollMinSize.Height - viewportHeight) - 3;
        base.OnSizeChanged(e);
        viewportHeight = ClientSize.Height;
        LayoutRows();
        AutoScrollPosition = new Point(0, follow ? BottomOffset() : offset);
    }

    private int BottomOffset() => Math.Max(0, AutoScrollMinSize.Height - ClientSize.Height);

    private void LayoutRows()
    {
        int width = Math.Max(1, ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
        int y = Padding.Top;
        SuspendLayout();
        try
        {
            foreach (MessageRow row in Controls)
            {
                row.ResizeTo(width);
                row.Location = new Point(Padding.Left, y + AutoScrollPosition.Y);
                y += row.Height + 8;
            }
            AutoScrollMinSize = new Size(0, y + Padding.Bottom);
        }
        finally
        {
            ResumeLayout();
        }
    }

    private sealed class MessageRow : Panel
    {
        private readonly Panel bubble;
        private readonly Label? senderLabel;
        private readonly Label messageLabel;
        private readonly Label timeLabel;
        private readonly bool outgoing;
        private readonly bool notice;

        public MessageRow(string? sender, string message, string time, bool outgoing, bool notice = false)
        {
            this.outgoing = outgoing;
            this.notice = notice;
            bubble = new Panel { BackColor = notice ? Canvas : outgoing ? SentFill : Color.White };
            if (sender is not null)
            {
                senderLabel = CreateLabel(sender, SenderFont, Ink);
                bubble.Controls.Add(senderLabel);
            }

            messageLabel = CreateLabel(message, notice ? MetaFont : BodyFont, notice ? Muted : Ink);
            timeLabel = CreateLabel(time, MetaFont, Muted);
            bubble.Controls.AddRange([messageLabel, timeLabel]);
            Controls.Add(bubble);
        }

        public void ResizeTo(int width)
        {
            const int horizontalPadding = 12;
            const int verticalPadding = 8;
            int textLimit = Math.Max(1, Math.Min(520, width * 3 / 4) - horizontalPadding * 2);
            int y = verticalPadding;
            int textWidth = 0;
            if (senderLabel is not null)
            {
                PlaceLabel(senderLabel);
            }

            PlaceLabel(messageLabel);
            if (timeLabel.Text.Length > 0)
            {
                PlaceLabel(timeLabel);
                timeLabel.Left = horizontalPadding + textWidth - timeLabel.Width;
            }

            int bubbleWidth = textWidth + horizontalPadding * 2;
            int bubbleHeight = y + verticalPadding;
            Size = new Size(width, bubbleHeight);
            int x = notice ? (width - bubbleWidth) / 2 : outgoing ? width - bubbleWidth : 0;
            bubble.Bounds = new Rectangle(x, 0, bubbleWidth, bubbleHeight);

            void PlaceLabel(Label label)
            {
                label.MaximumSize = new Size(textLimit, 0);
                Size preferred = label.GetPreferredSize(new Size(textLimit, 0));
                label.Bounds = new Rectangle(horizontalPadding, y, Math.Min(textLimit, preferred.Width), preferred.Height);
                y += label.Height;
                textWidth = Math.Max(textWidth, label.Width);
            }
        }

        private static Label CreateLabel(string text, Font font, Color color) => new()
        {
            Text = text,
            Font = font,
            ForeColor = color,
            UseMnemonic = false,
            AutoSize = false
        };
    }
}
