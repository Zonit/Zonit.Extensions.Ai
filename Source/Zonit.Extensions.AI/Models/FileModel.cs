namespace Zonit.Extensions.Ai.Models;

// TODO: Zwróæ extension pliku, np jpg, png itp

public class FileModel : IFile
{
    /// <summary>
    /// Tworzy nowy obiekt pliku na podstawie danych binarnych.
    /// </summary>
    /// <param name="name">Nazwa pliku.</param>
    /// <param name="mimeType">Typ MIME pliku.</param>
    /// <param name="data">Dane binarne pliku.</param>
    public FileModel(string name, string mimeType, byte[] data)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        MimeType = mimeType ?? throw new ArgumentNullException(nameof(mimeType));
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <summary>
    /// Tworzy nowy obiekt pliku, wczytuj¹c dane z istniej¹cego pliku.
    /// </summary>
    /// <param name="filePath">Œcie¿ka do pliku.</param>
    /// <param name="mimeType">Opcjonalny typ MIME - jeœli nie podano, zostanie okreœlony na podstawie rozszerzenia.</param>
    public static async Task<FileModel> CreateFromFilePathAsync(string filePath, string? mimeType = null)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentNullException(nameof(filePath));

        if (!System.IO.File.Exists(filePath))
            throw new FileNotFoundException("Nie znaleziono pliku", filePath);

        var name = Path.GetFileName(filePath);
        var data = await System.IO.File.ReadAllBytesAsync(filePath);
        
        // Okreœl typ MIME na podstawie rozszerzenia jeœli nie zosta³ podany
        mimeType ??= GetMimeTypeFromExtension(Path.GetExtension(filePath));

        return new FileModel(name, mimeType, data);
    }

    /// <summary>
    /// Nazwa pliku.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Typ MIME pliku.
    /// </summary>
    public string MimeType { get; }

    /// <summary>
    /// Dane binarne pliku.
    /// </summary>
    public byte[] Data { get; private set; }

    /// <summary>
    /// Konwertuje dane i tworzy nowy obiekt pliku z tymi danymi.
    /// </summary>
    /// <param name="data">Nowe dane binarne.</param>
    /// <returns>Nowy obiekt pliku z przekazanymi danymi.</returns>
    public IFile Convert(byte[] data)
    {
        return new FileModel(Name, MimeType, data);
    }

    /// <summary>
    /// Asynchronicznie pobiera dane pliku.
    /// </summary>
    /// <param name="cancellationToken">Token anulowania.</param>
    /// <returns>Dane binarne pliku.</returns>
    public Task<byte[]> GetFileDataAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Data);
    }

    /// <summary>
    /// Zapisuje dane do pliku.
    /// </summary>
    /// <param name="outputPath">Œcie¿ka docelowa.</param>
    /// <param name="cancellationToken">Token anulowania.</param>
    public async Task SaveToFileAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(outputPath))
            throw new ArgumentNullException(nameof(outputPath));

        // Upewnij siê, ¿e œcie¿ka do katalogu istnieje
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await System.IO.File.WriteAllBytesAsync(outputPath, Data, cancellationToken);
    }

    /// <summary>
    /// Aktualizuje dane binarne pliku.
    /// </summary>
    /// <param name="newData">Nowe dane binarne.</param>
    public void UpdateData(byte[] newData)
    {
        Data = newData ?? throw new ArgumentNullException(nameof(newData));
    }

    /// <summary>
    /// Okreœla typ MIME na podstawie rozszerzenia pliku.
    /// </summary>
    private static string GetMimeTypeFromExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".csv" => "text/csv",
            ".xml" => "application/xml",
            ".doc" or ".docx" => "application/msword",
            ".xls" or ".xlsx" => "application/vnd.ms-excel",
            ".ppt" or ".pptx" => "application/vnd.ms-powerpoint",
            _ => "application/octet-stream" // domyœlny typ dla nieznanych rozszerzeñ
        };
    }
}
