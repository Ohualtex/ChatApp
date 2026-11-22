namespace ChatApp.Client;

internal sealed record AttachmentCandidate(string Path, string Name, long Length, string? Error)
{
    internal string Description
    {
        get
        {
            string extension = System.IO.Path.GetExtension(Name).TrimStart('.').ToUpperInvariant();
            return Error ?? $"{(extension.Length == 0 ? "FILE" : extension)} · {FormatSize(Length)}";
        }
    }

    // Check readability and size without loading the file contents before the user confirms sending.
    internal static AttachmentCandidate Inspect(string path)
    {
        string name = path;
        try
        {
            name = System.IO.Path.GetFileName(path);
            if (Directory.Exists(path)) return new(path, name, 0, "Folders cannot be sent.");
            if (name.Length > 160) return new(path, name, 0, "The file name exceeds 160 characters.");
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            string? error = file.Length is 0 or > ChatAttachment.MaximumBytes
                ? "Choose a file between 1 byte and 10 MB." : null;
            return new(System.IO.Path.GetFullPath(path), name, file.Length, error);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return new(path, name, 0, error.Message); }
    }

    internal static string FormatSize(long size) => size < 1024 ? $"{size} B"
        : size < 1024 * 1024 ? $"{size / 1024d:0.#} KB" : $"{size / (1024d * 1024):0.#} MB";
}
