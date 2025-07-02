namespace Zonit.Extensions.Ai.Llm;

public abstract class OpenAiImageBase<TQuality, TSize> : OpenAiBase, IImageBase
    where TQuality : Enum
    where TSize : Enum
{
    public required abstract TQuality Quality { get; init; }
    public required abstract TSize Size { get; init; }

    public virtual int Quantity { get;init; } = 1;

    public string QualityValue => GetEnumValue(Quality);
    public string SizeValue => GetEnumValue(Size);

    // TODO: background, Compression, Format
    // https://platform.openai.com/docs/guides/image-generation?image-generation-model=gpt-image-1&lang=javascript

    private static string GetEnumValue(Enum enumValue)
    {
        var type = enumValue.GetType();
        var memberInfo = type.GetMember(enumValue.ToString());
        if (memberInfo.Length > 0)
        {
            var attributes = memberInfo[0].GetCustomAttributes(typeof(EnumValueAttribute), false);
            if (attributes.Length > 0)
            {
                return ((EnumValueAttribute)attributes[0]).Value;
            }
        }
        return enumValue.ToString().ToLowerInvariant();
    }
}