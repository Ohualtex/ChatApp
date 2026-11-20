namespace ChatApp.Client;

internal sealed class MessageFeed : Panel
{
    private static readonly Color Canvas = ChatTheme.Canvas;
    private static readonly Color SentFill = ChatTheme.Sent;
    private static readonly Color Ink = ChatTheme.Ink;
    private static readonly Color Muted = ChatTheme.Muted;
    private static readonly Font BodyFont = new("Segoe UI", 10);
    private static readonly Font MetaFont = new("Segoe UI", 8);
    private static readonly Font SenderFont = new("Segoe UI", 9, FontStyle.Bold);

    private readonly MessageScrollBar scrollBar = new() { Visible = false };
    private readonly List<MessageRow> rows = [];
    private int viewportHeight;
    private int contentHeight;
    private int wheelRemainder;
    private bool arranging;
    private string? previousSender;
    private DateTimeOffset? previousTimestamp;
    private bool previousOutgoing;
    private DateOnly? displayedDate;
    private int BottomOffset => Math.Max(0, contentHeight - Math.Max(1, viewportHeight));
    private bool FollowingNewest => scrollBar.Value >= BottomOffset - 3;

    public MessageFeed()
    {
        Dock = DockStyle.Fill;
        DoubleBuffered = true;
        ResizeRedraw = true;
        AutoScroll = false;
        Padding = new Padding(18, 12, 18, 12);
        BackColor = Canvas;
        AccessibleName = "Chat messages";
        viewportHeight = ClientSize.Height;
        Controls.Add(scrollBar);
        scrollBar.WheelRequested += (_, e) => OnMouseWheel(e);
        scrollBar.ValueChanged += (_, _) =>
        {
            if (arranging) return;
            PositionRows();
            Update();
        };
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            // Compose the wallpaper and child controls before presenting the frame.
            parameters.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
            return parameters;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e) => ChatWallpaper.Paint(e.Graphics, ClientRectangle);

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (!scrollBar.Visible) return;
        wheelRemainder += e.Delta;
        int notches = wheelRemainder / SystemInformation.MouseWheelScrollDelta;
        wheelRemainder %= SystemInformation.MouseWheelScrollDelta;
        int lines = SystemInformation.MouseWheelScrollLines;
        int step = Math.Max(1, (int)Math.Round(24 * DeviceDpi / 96d));
        int amount = lines < 0 ? Math.Max(1, viewportHeight) : Math.Max(0, lines) * step;
        scrollBar.Value = (int)Math.Clamp((long)scrollBar.Value - (long)notches * amount, 0, BottomOffset);
    }

    public void ClearMessages()
    {
        foreach (MessageRow row in rows) row.Dispose();
        rows.Clear();
        previousSender = null;
        previousTimestamp = null;
        displayedDate = null;
        wheelRemainder = 0;
        LayoutRows(follow: true);
    }

    public void AddMessage(string sender, string text, DateTimeOffset? timestamp, bool outgoing)
        => AddEntry(sender, text, timestamp, outgoing, showSender: true);

    public void AddPacket(ChatPacket packet, bool outgoing, bool showSender = true)
        => AddEntry(packet.Name, packet.Message ?? "", packet.Timestamp, outgoing, showSender,
            packet.Attachment, packet.Recording);

    private void AddEntry(string? sender, string text, DateTimeOffset? timestamp, bool outgoing,
        bool showSender, ChatAttachment? attachment = null, VoiceClip? recording = null)
    {
        DateTimeOffset? local = timestamp?.ToLocalTime();
        DateOnly? date = local.HasValue ? DateOnly.FromDateTime(local.Value.DateTime) : null;
        bool startsDay = date.HasValue && displayedDate != date;
        TimeSpan? elapsed = timestamp - previousTimestamp;
        bool grouped = !startsDay && sender is not null && sender == previousSender
            && outgoing == previousOutgoing && date.HasValue && previousTimestamp.HasValue
            && DateOnly.FromDateTime(previousTimestamp.Value.ToLocalTime().DateTime) == date
            && elapsed >= TimeSpan.Zero && elapsed <= TimeSpan.FromMinutes(5);
        var additions = new List<MessageRow>();
        if (startsDay)
        {
            DateOnly today = DateOnly.FromDateTime(DateTime.Now);
            string title = date == today ? "Today" : date == today.AddDays(-1) ? "Yesterday"
                : date!.Value.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
            additions.Add(new MessageRow(null, title, "", outgoing: false, notice: true) { LeadingGap = 8 });
        }
        additions.Add(new MessageRow(sender, text,
            local?.ToString("HH:mm") ?? "", outgoing, attachment: attachment, recording: recording, showSender: showSender && !grouped)
            { LeadingGap = grouped ? 4 : 12 });
        AddRows(additions);
        previousSender = sender;
        previousTimestamp = timestamp;
        previousOutgoing = outgoing;
        if (date.HasValue) displayedDate = date;
    }

    public void AddNotice(string text)
    {
        AddRows([new MessageRow(null, text, "", outgoing: false, notice: true) { LeadingGap = 12 }]);
        previousSender = null;
        previousTimestamp = null;
    }

    private void AddRows(IEnumerable<MessageRow> additions)
    {
        bool follow = FollowingNewest;
        var pending = additions.ToArray();
        foreach (MessageRow row in pending)
        {
            row.Visible = false;
            rows.Add(row);
            Controls.Add(row);
        }
        LayoutRows(follow);
        foreach (MessageRow row in pending) row.Visible = true;
        Invalidate(invalidateChildren: true);
        Update();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        bool follow = scrollBar is null || FollowingNewest;
        base.OnSizeChanged(e);
        if (scrollBar is not null) LayoutRows(follow);
    }

    private void LayoutRows(bool follow)
    {
        if (arranging) return;
        arranging = true;
        int previousOffset = scrollBar.Value;
        SuspendLayout();
        try
        {
            int inset = Math.Max(18, Math.Min(72, ClientSize.Width / 20));
            Padding = new Padding(inset, 12, inset, 12);
            int available = Math.Max(1, ClientSize.Width - Padding.Horizontal);
            int scrollWidth = Math.Max(1, (int)Math.Round(14 * DeviceDpi / 96d));
            bool hadScrollbar = contentHeight > viewportHeight;
            contentHeight = MeasureRows(RowWidth(hadScrollbar));
            bool overflow = contentHeight > ClientSize.Height;
            if (overflow != hadScrollbar) contentHeight = MeasureRows(RowWidth(overflow));
            int RowWidth(bool narrow) => Math.Max(1, available - (narrow ? scrollWidth : 0));
            viewportHeight = ClientSize.Height;
            scrollBar.Visible = overflow;
            scrollBar.Bounds = new Rectangle(Math.Max(0, ClientSize.Width - scrollWidth),
                0, scrollWidth, ClientSize.Height);
            scrollBar.SetRange(contentHeight, viewportHeight, follow ? BottomOffset : Math.Min(previousOffset, BottomOffset));
            scrollBar.BringToFront();
            PositionRows();
        }
        finally
        {
            ResumeLayout(performLayout: false);
            arranging = false;
        }

        int MeasureRows(int width)
        {
            int height = Padding.Vertical;
            foreach (MessageRow row in rows) { row.ResizeTo(width); height += row.Height + row.LeadingGap; }
            return height;
        }
    }

    private void PositionRows()
    {
        int y = Padding.Top - scrollBar.Value;
        foreach (MessageRow row in rows)
        {
            y += row.LeadingGap;
            row.Location = new Point(Padding.Left, y);
            y += row.Height;
        }
        Invalidate(invalidateChildren: true);
    }

    private sealed class MessageText : Label
    {
        private readonly List<string> lines = [];
        private const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix
            | TextFormatFlags.SingleLine | TextFormatFlags.NoClipping;
        private int lineHeight;
        public int LastLineWidth { get; private set; }
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal string ClockText { get; set; } = "";
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        internal Rectangle ClockBounds { get; set; }

        public MessageText()
        {
            AutoSize = false;
            UseMnemonic = false;
            DoubleBuffered = true;
        }

        public Size MeasureContent(int maximumWidth)
        {
            lines.Clear();
            lineHeight = TextRenderer.MeasureText("Ag", Font, Size.Empty, Flags).Height;
            foreach (string paragraph in Text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                if (paragraph.Length == 0) { lines.Add(""); continue; }
                int[] boundaries = System.Globalization.StringInfo.ParseCombiningCharacters(paragraph);
                int start = 0;
                while (start < boundaries.Length)
                {
                    int low = start + 1, high = boundaries.Length;
                    while (low < high)
                    {
                        int middle = (low + high + 1) / 2;
                        int end = middle < boundaries.Length ? boundaries[middle] : paragraph.Length;
                        if (Measure(paragraph[boundaries[start]..end]) <= maximumWidth) low = middle;
                        else high = middle - 1;
                    }
                    int stop = low;
                    if (stop < boundaries.Length)
                    {
                        for (int at = stop - 1; at > start; at--)
                            if (char.IsWhiteSpace(paragraph[boundaries[at]])) { stop = at + 1; break; }
                    }
                    int endIndex = stop < boundaries.Length ? boundaries[stop] : paragraph.Length;
                    lines.Add(paragraph[boundaries[start]..endIndex].TrimEnd());
                    start = stop;
                }
            }
            LastLineWidth = Measure(lines[^1]);
            return new Size(Math.Min(maximumWidth, lines.Max(Measure)), lines.Count * lineHeight);
        }

        private int Measure(string value) => value.Length == 0 ? 0
            : TextRenderer.MeasureText(value, Font, Size.Empty, Flags).Width;

        protected override void OnPaint(PaintEventArgs e)
        {
            for (int index = 0; index < lines.Count; index++)
                TextRenderer.DrawText(e.Graphics, lines[index], Font, new Point(0, index * lineHeight), ForeColor, Flags);
            TextRenderer.DrawText(e.Graphics, ClockText, MetaFont, ClockBounds.Location, Muted, Flags);
        }
    }

    private sealed class MessageRow : Panel
    {
        public int LeadingGap = 4;
        private readonly Panel bubble;
        private readonly Label? senderLabel;
        private readonly Label messageLabel;
        private readonly Label timeLabel;
        private readonly PictureBox? photo;
        private readonly Button? save;
        private readonly Button? play;
        private readonly VoicePlayback? playback;
        private readonly VoiceMessageCard? voiceCard;
        private readonly bool outgoing;
        private readonly bool notice;

        public MessageRow(string? sender, string message, string time, bool outgoing, bool notice = false, ChatAttachment? attachment = null, VoiceClip? recording = null, bool showSender = true)
        {
            this.outgoing = outgoing;
            this.notice = notice;
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            bubble = new RoundedPanel { BackColor = notice ? Canvas : outgoing ? SentFill : ChatTheme.Raised };
            if (showSender && sender is not null && !outgoing)
            {
                senderLabel = CreateLabel(sender, SenderFont, Ink);
                bubble.Controls.Add(senderLabel);
            }

            if (attachment is not null)
            {
                byte[] bytes;
                try { bytes = attachment.GetBytes(); }
                catch (IOException error) { bytes = []; message = error.Message; }
                if (bytes.Length > 0)
                {
                    string size = bytes.Length < 1024 ? $"{bytes.Length} B" : $"{bytes.Length / 1024d:N0} KB";
                    message = $"{attachment.DisplayName}\n{size}";
                    if (attachment.ContentType == "audio/wav")
                    {
                        try
                        {
                            TimeSpan duration = WaveAudio.Duration(bytes);
                            message = "Voice message  " + duration.ToString(@"mm\:ss");
                            playback = new VoicePlayback();
                            play = new Button { Text = "Play", AutoSize = true, FlatStyle = FlatStyle.Flat,
                                BackColor = ChatTheme.Surface, ForeColor = Ink };
                            playback.Changed += () => play.Text = playback.Playing ? "Stop" : "Play";
                            play.Click += (_, _) =>
                            {
                                if (playback.Playing) { playback.Stop(); return; }
                                try { playback.Play(bytes); }
                                catch (Exception error) when (error is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
                                {
                                    MessageBox.Show(FindForm(), error.Message, "Could not play recording",
                                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            };
                            bubble.Controls.Add(play);
                        }
                        catch (IOException)
                        {
                            // Non-PCM or damaged WAV attachments remain downloadable files.
                        }
                    }
                    if (attachment.ContentType.StartsWith("video/", StringComparison.Ordinal))
                        message = "Video · " + message;
                    if (attachment.ContentType.StartsWith("image/", StringComparison.Ordinal))
                    {
                        try
                        {
                            using var stream = new MemoryStream(bytes);
                            using var original = Image.FromStream(stream);
                            if (original.Width <= 4096 && original.Height <= 4096)
                            {
                                int height = Math.Max(1, Math.Min(180, original.Height * 220 / original.Width));
                                int width = Math.Max(1, Math.Min(220, original.Width * height / original.Height));
                                photo = new PictureBox { Image = new Bitmap(original, width, height),
                                    SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(width, height) };
                                bubble.Controls.Add(photo);
                            }
                        }
                        catch (Exception error) when (error is ArgumentException or OutOfMemoryException)
                        {
                            // Unsupported image formats remain downloadable file cards.
                        }
                    }
                    save = new Button { Text = "Save…", AutoSize = true, FlatStyle = FlatStyle.Flat,
                        BackColor = ChatTheme.Surface, ForeColor = Ink };
                    save.Click += async (_, _) =>
                    {
                        using var dialog = new SaveFileDialog { FileName = attachment.DisplayName,
                            Filter = "All files (*.*)|*.*", Title = "Save attachment" };
                        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                        save.Enabled = false;
                        try { await File.WriteAllBytesAsync(dialog.FileName, bytes); }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                        {
                            MessageBox.Show(FindForm(), error.Message, "Could not save attachment",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        finally { if (!save.IsDisposed) save.Enabled = true; }
                    };
                    bubble.Controls.Add(save);
                }
            }

            if (recording is not null)
            {
                voiceCard = new VoiceMessageCard(recording, sender ?? "Unknown", time);
                bubble.Controls.Add(voiceCard);
            }

            messageLabel = attachment is null && recording is null && !notice
                ? new MessageText { Text = message, Font = BodyFont, ForeColor = Ink }
                : CreateLabel(message, notice ? MetaFont : BodyFont, notice ? Muted : Ink);
            timeLabel = CreateLabel(time, MetaFont, Muted);
            messageLabel.Visible = voiceCard is null;
            timeLabel.Visible = voiceCard is null && time.Length > 0;
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
            if (senderLabel is not null && !outgoing)
            {
                PlaceLabel(senderLabel);
            }

            if (messageLabel is MessageText text)
            {
                int scale(int value) => Math.Max(1, (int)Math.Round(value * DeviceDpi / 96d));
                int inset = scale(10);
                int top = senderLabel is null ? scale(6) : y;
                int limit = Math.Max(1, Math.Min(scale(860), width * 3 / 5) - inset * 2);
                Size measured = text.MeasureContent(limit);
                Size clock = TextRenderer.MeasureText(timeLabel.Text, MetaFont, Size.Empty,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                int gap = scale(8);
                bool hasTime = timeLabel.Text.Length > 0;
                int contentWidth = Math.Max(measured.Width, senderLabel?.Width ?? 0);
                if (hasTime)
                    contentWidth = Math.Max(contentWidth, Math.Min(limit, text.LastLineWidth + gap + clock.Width));
                bool shared = !hasTime || text.LastLineWidth + gap + clock.Width <= contentWidth;
                int contentHeight = measured.Height + (shared ? 0 : clock.Height + scale(2));
                int height = top + contentHeight + scale(6);
                int textBubbleWidth = Math.Min(width, contentWidth + inset * 2);
                text.Bounds = new Rectangle(inset, top, contentWidth, contentHeight);
                if (senderLabel is not null) senderLabel.Left = inset;
                timeLabel.Bounds = new Rectangle(textBubbleWidth - inset - clock.Width,
                    top + contentHeight - clock.Height, clock.Width, clock.Height);
                timeLabel.Visible = false;
                text.ClockText = timeLabel.Text;
                text.ClockBounds = new Rectangle(timeLabel.Left - inset, timeLabel.Top - top, clock.Width, clock.Height);
                text.Invalidate();
                Size = new Size(width, height);
                bubble.Bounds = new Rectangle(outgoing ? width - textBubbleWidth : 0, 0, textBubbleWidth, height);
                return;
            }

            if (voiceCard is not null)
            {
                int cardWidth = Math.Max(1, Math.Min(350, width * 3 / 4) - horizontalPadding * 2);
                voiceCard.Bounds = new Rectangle(horizontalPadding, y, cardWidth, 68);
                int height = y + voiceCard.Height + verticalPadding;
                int voiceWidth = cardWidth + horizontalPadding * 2;
                Size = new Size(width, height);
                bubble.Bounds = new Rectangle(outgoing ? width - voiceWidth : 0, 0, voiceWidth, height);
                return;
            }

            if (photo is not null)
            {
                photo.Bounds = new Rectangle(horizontalPadding, y, Math.Min(textLimit, photo.Image!.Width),
                    Math.Min(180, photo.Image.Height));
                y += photo.Height + 6;
                textWidth = Math.Max(textWidth, photo.Width);
            }
            PlaceLabel(messageLabel);
            if (save is not null)
            {
                int actionLeft = horizontalPadding;
                if (play is not null)
                {
                    play.Location = new Point(actionLeft, y + 6);
                    actionLeft += play.Width + 8;
                }
                save.Location = new Point(actionLeft, y + 6);
                y += Math.Max(save.Height, play?.Height ?? 0) + 12;
                textWidth = Math.Max(textWidth, actionLeft - horizontalPadding + save.Width);
            }
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                playback?.Dispose();
                photo?.Image?.Dispose();
            }
            base.Dispose(disposing);
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
