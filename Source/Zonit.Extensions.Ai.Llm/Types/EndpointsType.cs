namespace Zonit.Extensions.Ai.Llm;

[Flags]
public enum EndpointsType
{
    None = 0,
    Chat = 1 << 0,
    Response = 1 << 1,
    Realtime = 1 << 2,
    Assistant = 1 << 3,
    Batch = 1 << 4,
    FineTuning = 1 << 5,
    Embedding = 1 << 6,
    Image = 1 << 7,
    ImageEdit = 1 << 8,
    Speech = 1 << 9,
    Transcription = 1 << 10,
    Translation = 1 << 11,
    Moderation = 1 << 12,
    Completion = 1 << 13,
}