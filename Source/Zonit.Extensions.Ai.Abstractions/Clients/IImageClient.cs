//using Zonit.Extensions.Ai.Llm.Base;

//namespace Zonit.Extensions.Ai;

//public interface IImageClient
//{
//    IImageClient AddVariable(string key, string? value);
//    IImageClient AddVariable(string key, string[]? values);
//    IImageClient AddVariable(string key, int? value);
//    IImageClient AddVariable(string key, decimal? value);
//    IImageClient AddVariable(string key, bool? value);
//    IImageClient AddVariable(string key, DateTime? value);
//    IImageClient AddVariable(string key, Guid? value);
//    IImageClient AddVariable(string key, IFile? value);

//    Task<Result<IFile>> GenerateImageAsync(string prompt, ILlmBase model, CancellationToken cancellationToken = default);
//    Task<Result<IReadOnlyCollection<IFile>>> GenerateImagesAsync(string prompt, ILlmBase model, CancellationToken cancellationToken = default);
//}