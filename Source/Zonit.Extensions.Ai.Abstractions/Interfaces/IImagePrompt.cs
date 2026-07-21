using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// A prompt for image generation. Produces an <see cref="Asset"/> (the generated
/// image) and — because image prompts always have the same shape — is best built
/// with the ready-made <see cref="ImagePrompt"/> or, for a templated/typed prompt,
/// by inheriting <see cref="ImagePromptBase"/>.
/// </summary>
/// <remarks>
/// This is a marker over <see cref="IPrompt{Asset}"/>: the image-generation overload
/// <c>ai.GenerateAsync(IImageLlm, IImagePrompt)</c> accepts only this type, so an
/// arbitrary text prompt can never be passed to image generation by mistake.
/// </remarks>
public interface IImagePrompt : IPrompt<Asset>
{
}
