namespace Zonit.Extensions.Ai.Llm;

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
}