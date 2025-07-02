namespace Zonit.Extensions.Ai.Llm;

// może nazwać to Modalities?
public enum ChannelType
{
    None = 0,
    Text = 1 << 0,
    Image = 1 << 1,
    Audio = 1 << 2
}