namespace Zonit.Extensions.Ai;

/// <summary>
/// Represents a file for AI operations (input images, documents, output images, etc.).
/// </summary>
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
    /// </summary>
    public string ToDataUrl() => $"data:{MimeType};base64,{ToBase64()}";
    
    /// <summary>
    /// Creates AiFile from file path.
    /// </summary>
    public static async Task<AiFile> FromPathAsync(string path, CancellationToken cancellationToken = default)
    {
        var data = await File.ReadAllBytesAsync(path, cancellationToken);
        var name = Path.GetFileName(path);
        var mimeType = GetMimeType(path);
        
        return new AiFile { Name = name, MimeType = mimeType, Data = data };
    }
    
    /// <summary>
    /// Creates AiFile from bytes.
    /// </summary>
    public static AiFile FromBytes(byte[] data, string mimeType, string name = "file")
        => new() { Name = name, MimeType = mimeType, Data = data };
    
    /// <summary>
    /// Creates AiFile from URL (downloads content).
    /// </summary>
    public static async Task<AiFile> FromUrlAsync(string url, HttpClient? httpClient = null, CancellationToken cancellationToken = default)
    {
        using var client = httpClient ?? new HttpClient();
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var mimeType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        var name = Path.GetFileName(new Uri(url).LocalPath);
        
        return new AiFile { Name = name, MimeType = mimeType, Data = data, Url = url };
    }
    
    /// <summary>
    /// Saves file to disk.
    /// </summary>
    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
        => await File.WriteAllBytesAsync(path, Data, cancellationToken);
    
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
}
