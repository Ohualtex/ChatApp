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

    private readonly VScrollBar scrollBar = new() { Visible = false, SmallChange = 24, TabStop = false };
    private readonly List<MessageRow> rows = [];
    private int viewportHeight;
    private int contentHeight;
    private int wheelRemainder;
    private bool arranging;
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
        int amount = lines < 0 ? scrollBar.LargeChange : Math.Max(0, lines) * scrollBar.SmallChange;
        scrollBar.Value = Math.Clamp(scrollBar.Value - notches * amount, 0, BottomOffset);
    }

    public void ClearMessages()
    {
        foreach (MessageRow row in rows) row.Dispose();
        rows.Clear();
        wheelRemainder = 0;
        LayoutRows(follow: true);
    }

    public void AddMessage(string sender, string text, DateTimeOffset? timestamp, bool outgoing)
    {
        string time = timestamp?.ToLocalTime().ToString("HH:mm") ?? "";
        AddRow(new MessageRow(outgoing ? null : sender, text, time, outgoing));
    }

    public void AddPacket(ChatPacket packet, bool outgoing)
    {
        string time = packet.Timestamp?.ToLocalTime().ToString("HH:mm") ?? "";
        AddRow(new MessageRow(packet.Name, packet.Message ?? "", time, outgoing,
            attachment: packet.Attachment, recording: packet.Recording));
    }

    public void AddNotice(string text) => AddRow(new MessageRow(null, text, "", outgoing: false, notice: true));

    private void AddRow(MessageRow row)
    {
        bool follow = FollowingNewest;
        row.Visible = false;
        rows.Add(row);
        Controls.Add(row);
        LayoutRows(follow);
        row.Visible = true;
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
            int available = Math.Max(1, ClientSize.Width - Padding.Horizontal);
            bool hadScrollbar = contentHeight > viewportHeight;
            contentHeight = MeasureRows(RowWidth(hadScrollbar));
            bool overflow = contentHeight > ClientSize.Height;
            if (overflow != hadScrollbar) contentHeight = MeasureRows(RowWidth(overflow));
            int RowWidth(bool narrow) => Math.Max(1, available - (narrow ? SystemInformation.VerticalScrollBarWidth : 0));
            viewportHeight = ClientSize.Height;
            scrollBar.Visible = overflow;
            scrollBar.Bounds = new Rectangle(Math.Max(0, ClientSize.Width - SystemInformation.VerticalScrollBarWidth),
                0, SystemInformation.VerticalScrollBarWidth, ClientSize.Height);
            scrollBar.Maximum = Math.Max(0, contentHeight - 1);
            scrollBar.LargeChange = Math.Max(1, viewportHeight);
            scrollBar.Value = follow ? BottomOffset : Math.Min(previousOffset, BottomOffset);
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
            foreach (MessageRow row in rows) { row.ResizeTo(width); height += row.Height + 8; }
            return height;
        }
    }

    private void PositionRows()
    {
        int y = Padding.Top - scrollBar.Value;
        foreach (MessageRow row in rows)
        {
            row.Location = new Point(Padding.Left, y);
            y += row.Height + 8;
        }
        Invalidate(invalidateChildren: true);
    }

    private sealed class MessageRow : Panel
    {
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

        public MessageRow(string? sender, string message, string time, bool outgoing, bool notice = false, ChatAttachment? attachment = null, VoiceClip? recording = null)
        {
            this.outgoing = outgoing;
            this.notice = notice;
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            bubble = new RoundedPanel { BackColor = notice ? Canvas : outgoing ? SentFill : ChatTheme.Raised };
            if (sender is not null && !outgoing)
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

            messageLabel = CreateLabel(message, notice ? MetaFont : BodyFont, notice ? Muted : Ink);
            timeLabel = CreateLabel(time, MetaFont, Muted);
            messageLabel.Visible = timeLabel.Visible = voiceCard is null;
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
