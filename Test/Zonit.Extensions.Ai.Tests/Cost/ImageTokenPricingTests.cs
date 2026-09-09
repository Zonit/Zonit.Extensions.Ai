using FluentAssertions;
using Xunit;
using Zonit.Extensions.Ai.OpenAi;

namespace Zonit.Extensions.Ai.Tests.Cost;

/// <summary>
/// Pins the token-based image billing introduced with gpt-image-2 and carried
/// over to the gpt-image-2.5 family. These models are NOT billed at a flat
/// per-image rate: text and image tokens carry different rates on input, and
/// the generated image is billed as output tokens.
/// </summary>
public class ImageTokenPricingTests
{
    [Theory]
    [InlineData(typeof(GPTImage2))]
    [InlineData(typeof(GPTImage25Flare))]
    [InlineData(typeof(GPTImage25Sunburst))]
    public void ImageModels_ShareTheGptImageRateCard(Type modelType)
    {
        var model = (ITokenPricedImageLlm)Activator.CreateInstance(modelType)!;

        model.PriceTextInput.Should().Be(5.00m);
        model.PriceCachedTextInput.Should().Be(1.25m);
        model.PriceImageInput.Should().Be(8.00m);
        model.PriceCachedImageInput.Should().Be(2.00m);
        model.PriceImageOutput.Should().Be(30.00m);
    }

    [Fact]
    public void TokenCost_PricesEachBucketAtItsOwnRate()
    {
        var model = new GPTImage25Flare { Quality = GPTImage25Base.QualityType.High, Size = GPTImage25Base.SizeType.Square };

        var usage = new ImageTokenUsage
        {
            TextInputTokens = 100_000,
            CachedTextInputTokens = 40_000,
            ImageInputTokens = 200_000,
            CachedImageInputTokens = 50_000,
            ImageOutputTokens = 10_000,
        };

        var (inputCost, outputCost) = AiCostCalculator.CalculateImageTokenCosts(model, usage);

        // text  :  60_000/1M × $5.00 + 40_000/1M × $1.25 = 0.30 + 0.05
        // image : 150_000/1M × $8.00 + 50_000/1M × $2.00 = 1.20 + 0.10
        inputCost.Value.Should().BeApproximately(1.65m, 1e-9m);
        // output: 10_000/1M × $30 = 0.30
        outputCost.Value.Should().BeApproximately(0.30m, 1e-9m);
    }

    [Fact]
    public void PerImageEstimate_ScalesWithQualityAndPixelArea()
    {
        var square = new GPTImage25Flare { Quality = GPTImage25Base.QualityType.High, Size = GPTImage25Base.SizeType.Square };
        var landscape = new GPTImage25Flare { Quality = GPTImage25Base.QualityType.High, Size = GPTImage25Base.SizeType.Landscape };
        var low = new GPTImage25Flare { Quality = GPTImage25Base.QualityType.Low, Size = GPTImage25Base.SizeType.Square };

        // 4_160 tokens × $30/1M
        square.GetImageGenerationPrice().Should().BeApproximately(0.1248m, 1e-9m);

        // 1536×1024 is 1.5× the pixels of 1024², and OpenAI's published token
        // table scales exactly with area.
        landscape.GetImageGenerationPrice().Should().BeApproximately(square.GetImageGenerationPrice() * 1.5m, 1e-9m);

        low.GetImageGenerationPrice().Should().BeLessThan(square.GetImageGenerationPrice());
    }

    [Fact]
    public void ProviderEstimate_UsesThePerImagePriceNotTheTokenRate()
    {
        // Regression: CalculateCost(IImageLlm) used to return PriceOutput, which
        // on a token-priced model is a per-1M-token rate — $30 per image.
        var model = new GPTImage25Sunburst { Quality = GPTImage25Base.QualityType.Medium, Size = GPTImage25Base.SizeType.Square };

        AiCostCalculator.CalculateImageCost(model, imageCount: 2)
            .Value.Should().BeApproximately(model.GetImageGenerationPrice() * 2, 1e-9m);
    }
}
