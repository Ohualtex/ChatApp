using System.Text.Json.Serialization;

namespace ChatApp.Client;

internal sealed record ChatAttachment(string FileName, string ContentType, string Data)
{
    public const int MaximumBytes = 10 * 1024 * 1024;

    // Validate the encoded length before allocation, then check the actual decoded size.
    public byte[] GetBytes()
    {
        if (Data.Length > ((MaximumBytes + 2) / 3) * 4)
            throw new IOException("The attachment exceeds 10 MB.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(Data); }
        catch (FormatException error) { throw new IOException("The attachment is damaged.", error); }
        if (bytes.Length is 0 or > MaximumBytes)
            throw new IOException("Attachments must be between 1 byte and 10 MB.");
        return bytes;
    }

    // Read and revalidate the file at send time because its contents may have changed since review.
    public static async Task<ChatAttachment> FromFileAsync(string path, CancellationToken token)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        if (file.Length is 0 or > MaximumBytes)
            throw new IOException("Choose a file between 1 byte and 10 MB.");
        byte[] bytes = new byte[checked((int)file.Length)];
        await file.ReadExactlyAsync(bytes, token);
        string name = Path.GetFileName(path);
        if (name.Length > 160) throw new IOException("The file name is too long.");
        string contentType = Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".webm" => "video/webm",
            ".wav" => "audio/wav",
            _ => "application/octet-stream"
        };
        return new(name, contentType, Convert.ToBase64String(bytes));
    }

    [JsonIgnore]
    public string DisplayName => string.Concat(Path.GetFileName(FileName)
        .Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
