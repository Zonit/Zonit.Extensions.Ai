namespace Zonit.Extensions.Ai;

/// <summary>
/// Interface for files used in AI operations.
/// </summary>
public interface IFile
{
    /// <summary>
    /// File name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// MIME type (e.g., "image/png", "application/pdf").
    /// </summary>
    string MimeType { get; }

    /// <summary>
    /// File content as bytes.
    /// </summary>
    byte[] Data { get; }

    /// <summary>
    /// Optional URL if file is remote.
    /// </summary>
    string? Url { get; }

    /// <summary>
    /// Checks if this is an image file.
    /// </summary>
    bool IsImage { get; }

    /// <summary>
    /// Checks if this is a document (PDF, DOC, etc.).
    /// </summary>
    bool IsDocument { get; }

    /// <summary>
    /// Checks if this is an audio file.
    /// </summary>
    bool IsAudio { get; }

    /// <summary>
    /// Gets data as Base64 string.
    /// </summary>
    string ToBase64();

    /// <summary>
    /// Gets data URL (data:mime;base64,xxx).
    /// </summary>
    string ToDataUrl();

    /// <summary>
    /// Saves file to disk.
    /// </summary>
    Task SaveAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a file for AI operations (input images, documents, output images, etc.).
/// </summary>
/// <remarks>
/// This is the primary class for AI file operations.
/// Legacy code can use <see cref="AiFile"/> which is an obsolete alias.
/// </remarks>
public sealed class File : IFile
{
    /// <summary>
    /// File name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// MIME type (e.g., "image/png", "application/pdf").
    /// </summary>
    public required string MimeType { get; init; }

    /// <summary>
    /// File content as bytes.
    /// </summary>
    public required byte[] Data { get; init; }

    /// <summary>
    /// Optional URL if file is remote.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Checks if this is an image file.
    /// </summary>
    public bool IsImage => MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if this is a document (PDF, DOC, etc.).
    /// </summary>
    public bool IsDocument => MimeType switch
    {
        "application/pdf" => true,
        "application/msword" => true,
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => true,
        "text/plain" => true,
        _ => false
    };

    /// <summary>
    /// Checks if this is an audio file.
    /// </summary>
    public bool IsAudio => MimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets data as Base64 string.
    /// </summary>
    public string ToBase64() => Convert.ToBase64String(Data);

    /// <summary>
    /// Gets data URL (data:mime;base64,xxx).
    /// Uses detected MIME type from binary data for images to ensure accuracy.
    /// </summary>
    public string ToDataUrl()
    {
        var mimeType = IsImage ? DetectImageMimeType(Data) ?? MimeType : MimeType;
        return $"data:{mimeType};base64,{ToBase64()}";
    }

    /// <summary>
    /// Gets the actual MIME type, detecting from binary data for images.
    /// </summary>
    public string GetActualMimeType() => IsImage ? DetectImageMimeType(Data) ?? MimeType : MimeType;

    /// <summary>
    /// Creates File from file path.
    /// </summary>
    public static async Task<File> FromPathAsync(string path, CancellationToken cancellationToken = default)
    {
        var data = await System.IO.File.ReadAllBytesAsync(path, cancellationToken);
        var name = Path.GetFileName(path);
        var mimeType = GetMimeType(path);

        return new File { Name = name, MimeType = mimeType, Data = data };
    }

    /// <summary>
    /// Creates File from bytes.
    /// </summary>
    public static File FromBytes(byte[] data, string mimeType, string name = "file")
        => new() { Name = name, MimeType = mimeType, Data = data };

    /// <summary>
    /// Creates File from URL (downloads content).
    /// </summary>
    public static async Task<File> FromUrlAsync(string url, HttpClient? httpClient = null, CancellationToken cancellationToken = default)
    {
        using var client = httpClient ?? new HttpClient();
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var mimeType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        var name = Path.GetFileName(new Uri(url).LocalPath);

        return new File { Name = name, MimeType = mimeType, Data = data, Url = url };
    }

    /// <summary>
    /// Saves file to disk.
    /// </summary>
    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
        => await System.IO.File.WriteAllBytesAsync(path, Data, cancellationToken);

    private static string GetMimeType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".txt" => "text/plain",
        ".json" => "application/json",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".mp4" => "video/mp4",
        _ => "application/octet-stream"
    };

    /// <summary>
    /// Detects image MIME type from binary data using magic bytes.
    /// Returns null if format is not recognized.
    /// </summary>
    private static string? DetectImageMimeType(byte[] data)
    {
        if (data == null || data.Length < 12)
            return null;

        // JPEG: FF D8 FF
        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "image/jpeg";

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
            data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A)
            return "image/png";

        // GIF: 47 49 46 38 (GIF8)
        if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38)
            return "image/gif";

        // WebP: 52 49 46 46 ... 57 45 42 50 (RIFF....WEBP)
        if (data.Length >= 12 &&
            data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
            data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
            return "image/webp";

        // BMP: 42 4D (BM)
        if (data[0] == 0x42 && data[1] == 0x4D)
            return "image/bmp";

        // TIFF: 49 49 2A 00 (little endian) or 4D 4D 00 2A (big endian)
        if ((data[0] == 0x49 && data[1] == 0x49 && data[2] == 0x2A && data[3] == 0x00) ||
            (data[0] == 0x4D && data[1] == 0x4D && data[2] == 0x00 && data[3] == 0x2A))
            return "image/tiff";

        // ICO: 00 00 01 00
        if (data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x01 && data[3] == 0x00)
            return "image/x-icon";

        // AVIF: check for ftyp box with avif brand
        if (data.Length >= 12 && data[4] == 0x66 && data[5] == 0x74 && data[6] == 0x79 && data[7] == 0x70)
        {
            // Check for avif, avis, or mif1 brands
            if ((data[8] == 0x61 && data[9] == 0x76 && data[10] == 0x69 && data[11] == 0x66) ||
                (data[8] == 0x61 && data[9] == 0x76 && data[10] == 0x69 && data[11] == 0x73) ||
                (data[8] == 0x6D && data[9] == 0x69 && data[10] == 0x66 && data[11] == 0x31))
                return "image/avif";
        }

        return null;
    }
}

/// <summary>
/// Legacy alias for <see cref="File"/>.
/// </summary>
[Obsolete("Use File instead. This alias will be removed in a future version.")]
public sealed class AiFile
{
    /// <summary>
    /// File name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// MIME type (e.g., "image/png", "application/pdf").
    /// </summary>
    public required string MimeType { get; init; }

    /// <summary>
    /// File content as bytes.
    /// </summary>
    public required byte[] Data { get; init; }

    /// <summary>
    /// Optional URL if file is remote.
    /// </summary>
    public string? Url { get; init; }

    /// <inheritdoc cref="File.IsImage"/>
    public bool IsImage => MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc cref="File.IsDocument"/>
    public bool IsDocument => MimeType switch
    {
        "application/pdf" => true,
        "application/msword" => true,
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => true,
        "text/plain" => true,
        _ => false
    };

    /// <inheritdoc cref="File.IsAudio"/>
    public bool IsAudio => MimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc cref="File.ToBase64"/>
    public string ToBase64() => Convert.ToBase64String(Data);

    /// <inheritdoc cref="File.ToDataUrl"/>
    public string ToDataUrl() => $"data:{MimeType};base64,{ToBase64()}";

    /// <inheritdoc cref="File.SaveAsync"/>
    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
        => await System.IO.File.WriteAllBytesAsync(path, Data, cancellationToken);

    /// <summary>
    /// Implicit conversion from AiFile to File.
    /// </summary>
    public static implicit operator File(AiFile aiFile) => new()
    {
        Name = aiFile.Name,
        MimeType = aiFile.MimeType,
        Data = aiFile.Data,
        Url = aiFile.Url
    };

    /// <summary>
    /// Implicit conversion from File to AiFile.
    /// </summary>
    public static implicit operator AiFile(File file) => new()
    {
        Name = file.Name,
        MimeType = file.MimeType,
        Data = file.Data,
        Url = file.Url
    };

    /// <inheritdoc cref="File.FromPathAsync"/>
    public static async Task<AiFile> FromPathAsync(string path, CancellationToken cancellationToken = default)
        => await File.FromPathAsync(path, cancellationToken);

    /// <inheritdoc cref="File.FromBytes"/>
    public static AiFile FromBytes(byte[] data, string mimeType, string name = "file")
        => File.FromBytes(data, mimeType, name);

    /// <inheritdoc cref="File.FromUrlAsync"/>
    public static async Task<AiFile> FromUrlAsync(string url, HttpClient? httpClient = null, CancellationToken cancellationToken = default)
        => await File.FromUrlAsync(url, httpClient, cancellationToken);
}
