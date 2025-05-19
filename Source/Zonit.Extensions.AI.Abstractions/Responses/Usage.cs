namespace Zonit.Extensions.Ai;

public class Usage
{
    public int Input { get; init; }
    public Details? InputDetails { get; init; }
    public int Output { get; init; }
    public Details? OutputDetails { get; init; }
    public int Total => Input + Output;

    public class Details
    {
        public int Text { get; init; }
        public int Image { get; init; }
        public int Audio { get; init; }
    }
}