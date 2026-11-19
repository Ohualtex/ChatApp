using System.Diagnostics;

namespace ChatApp.Client;

internal sealed class VoiceMessageDialog : Form
{
    private readonly Label state = new() { Text = "Ready to record", AutoSize = true, ForeColor = ChatTheme.Muted };
    private readonly Label clock = new() { Text = "00:00 / 01:00", AutoSize = true, Font = new Font("Segoe UI", 24, FontStyle.Bold) };
    private readonly Button record = new() { Text = "Record" };
    private readonly Button preview = new() { Text = "Play", Enabled = false };
    private readonly Button send = new() { Text = "Send", Enabled = false };
    private readonly Button cancel = new() { Text = "Cancel", DialogResult = DialogResult.Cancel };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 100 };
    private readonly Stopwatch elapsed = new();
    private readonly VoicePlayback playback = new();
    private VoiceRecorder? recorder;
    private byte[]? audio;

    public ChatAttachment? Attachment { get; private set; }

    public VoiceMessageDialog(string destination)
    {
        Text = "Voice message";
        Font = new Font("Segoe UI", 10);
        BackColor = ChatTheme.Surface;
        ForeColor = ChatTheme.Ink;
        ClientSize = new Size(440, 240);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        CancelButton = cancel;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
            Padding = new Padding(22) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.Controls.Add(new Label { Text = "To " + destination, AutoEllipsis = true, Dock = DockStyle.Fill }, 0, 0);
        layout.Controls.Add(clock, 0, 1);
        state.MaximumSize = new Size(390, 0);
        layout.Controls.Add(state, 0, 2);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = Padding.Empty };
        foreach (Button button in new[] { record, preview, send, cancel })
        {
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            button.Dock = DockStyle.Fill;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = button == send ? ChatTheme.Accent : ChatTheme.Raised;
            button.ForeColor = button == send ? ChatTheme.Canvas : ChatTheme.Ink;
            actions.Controls.Add(button);
        }
        layout.Controls.Add(actions, 0, 3);
        Controls.Add(layout);
        Shown += (_, _) => ChatTheme.DarkTitle(this);
        record.Click += (_, _) => ToggleRecording();
        preview.Click += (_, _) =>
        {
            if (playback.Playing) playback.Stop();
            else if (audio is not null)
            {
                try { playback.Play(audio); }
                catch (Exception error) when (error is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
                { ShowAudioError(error); }
            }
        };
        playback.Changed += () => preview.Text = playback.Playing ? "Stop" : "Play";
        timer.Tick += (_, _) =>
        {
            clock.Text = $"{Math.Min(60, (int)elapsed.Elapsed.TotalSeconds) / 60:00}:{(int)Math.Min(60, elapsed.Elapsed.TotalSeconds) % 60:00} / 01:00";
            if (elapsed.Elapsed >= TimeSpan.FromSeconds(60)) FinishRecording();
        };
        send.Click += (_, _) =>
        {
            if (audio is null) return;
            Attachment = new ChatAttachment("voice-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".wav",
                "audio/wav", Convert.ToBase64String(audio));
            DialogResult = DialogResult.OK;
            Close();
        };
    }

    private void ToggleRecording()
    {
        if (recorder is not null) { FinishRecording(); return; }
        playback.Stop();
        audio = null;
        preview.Enabled = send.Enabled = false;
        clock.Text = "00:00 / 01:00";
        recorder = new VoiceRecorder();
        try
        {
            recorder.Start();
            elapsed.Restart();
            timer.Start();
            record.Text = "Stop";
            record.BackColor = Color.FromArgb(160, 48, 48);
            state.Text = "Recording from your microphone...";
        }
        catch (IOException error)
        {
            recorder.Dispose();
            recorder = null;
            ShowAudioError(error);
        }
    }

    private void FinishRecording()
    {
        timer.Stop();
        elapsed.Stop();
        if (recorder is null) return;
        try
        {
            audio = recorder.Finish();
            TimeSpan duration = WaveAudio.Duration(audio);
            clock.Text = duration.ToString(@"mm\:ss");
            preview.Enabled = send.Enabled = true;
            state.Text = "Listen before sending, or record again.";
        }
        catch (IOException error) { ShowAudioError(error); }
        finally
        {
            recorder.Dispose();
            recorder = null;
            record.Text = "Record";
            record.BackColor = ChatTheme.Raised;
        }
    }

    private void ShowAudioError(Exception error)
    {
        state.Text = "Audio unavailable. Please try again.";
        MessageBox.Show(this, error.Message, "Voice message", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            recorder?.Dispose();
            playback.Dispose();
        }
        base.Dispose(disposing);
    }
}
