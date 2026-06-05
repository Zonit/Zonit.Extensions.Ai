namespace Zonit.Extensions.Ai;

/// <summary>
/// Features supported by AI models.
/// </summary>
[Flags]
public enum FeaturesType
{
    None = 0,
    Streaming = 1 << 0,
    FunctionCalling = 1 << 1,
    StructuredOutputs = 1 << 2,
    FineTuning = 1 << 3,
    Distillation = 1 << 4,
    PredictedOutputs = 1 << 5,
    Inpainting = 1 << 6,
    Reasoning = 1 << 7,
    Vision = 1 << 8,
    Audio = 1 << 9,
}
