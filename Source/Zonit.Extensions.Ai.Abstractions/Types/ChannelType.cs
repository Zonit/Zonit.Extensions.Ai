namespace Zonit.Extensions.Ai;

/// <summary>
/// Supported input/output modalities.
/// </summary>
[Flags]
public enum ChannelType
{
    None = 0,
    Text = 1 << 0,
    Image = 1 << 1,
    Audio = 1 << 2
}
