namespace Zonit.Extensions.AI;

public interface IFile
{
    IFile Convert(byte[] data);


    string Name { get; }
    string MimeType { get; }
    byte[] Data { get; }
    Task<byte[]> GetFileDataAsync(CancellationToken cancellationToken = default);
    Task SaveToFileAsync(string outputPath, CancellationToken cancellationToken = default);
}
