using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// A prompt for video generation. Produces an <see cref="Asset"/> (the generated
/// video) and — because video prompts always have the same shape — is best built
/// with the ready-made <see cref="VideoPrompt"/> or, for a templated/typed prompt,
/// by inheriting <see cref="VideoPromptBase"/>.
/// </summary>
/// <remarks>
/// This is a marker over <see cref="IPrompt{Asset}"/>: the video-generation overload
/// <c>ai.GenerateAsync(IVideoLlm, IVideoPrompt)</c> accepts only this type, so an
/// arbitrary text prompt can never be passed to video generation by mistake. A
/// <see cref="VideoPrompt"/> also carries the optional source image (image-to-video)
/// or source video (video-to-video / edit).
/// </remarks>
public interface IVideoPrompt : IPrompt<Asset>
{
}
