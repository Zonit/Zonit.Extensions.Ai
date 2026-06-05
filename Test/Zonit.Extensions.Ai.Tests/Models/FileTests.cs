using FluentAssertions;
using Xunit;
using Zonit.Extensions;

namespace Zonit.Extensions.Ai.Tests.Models;

/// <summary>
/// Tests for Asset value object - IsImage, IsDocument, IsAudio detection, signature detection.
/// </summary>
public class AssetTests
{
    [Theory]
    [InlineData("image/jpeg", true)]
    [InlineData("image/png", true)]
    [InlineData("image/gif", true)]
    [InlineData("image/webp", true)]
    [InlineData("image/svg+xml", true)]
    [InlineData("application/pdf", false)]
    [InlineData("text/plain", false)]
    [InlineData("audio/mpeg", false)]
    public void IsImage_ShouldDetectImageMimeTypes(string mimeType, bool expected)
    {
        // Arrange
        var asset = new Asset([0x00], "test.file", mimeType);

        // Act & Assert
        asset.IsImage.Should().Be(expected);
    }

    [Theory]
    [InlineData("application/pdf", true)]
    [InlineData("application/msword", true)]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", true)]
    [InlineData("image/png", false)]
    [InlineData("audio/mpeg", false)]
    public void IsDocument_ShouldDetectDocumentMimeTypes(string mimeType, bool expected)
    {
        // Arrange
        var asset = new Asset([0x00], "test.file", mimeType);

        // Act & Assert
        asset.IsDocument.Should().Be(expected);
    }

    [Theory]
    [InlineData("audio/mpeg", true)]
    [InlineData("audio/wav", true)]
    [InlineData("audio/ogg", true)]
    [InlineData("image/png", false)]
    [InlineData("application/pdf", false)]
    public void IsAudio_ShouldDetectAudioMimeTypes(string mimeType, bool expected)
    {
        // Arrange
        var asset = new Asset([0x00], "test.file", mimeType);

        // Act & Assert
        asset.IsAudio.Should().Be(expected);
    }

    [Fact]
    public void ToBase64_ShouldEncodeDataCorrectly()
    {
        // Arrange
        var data = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
        Asset.MimeType mimeType = Asset.MimeType.TextPlain;
        var asset = new Asset(data, "test.txt", mimeType);

        // Act
        var base64 = asset.ToBase64();

        // Assert
        base64.Should().Be("SGVsbG8=");
    }

    [Fact]
    public void ToDataUrl_ShouldFormatCorrectly()
    {
        // Arrange
        var data = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
        Asset.MimeType mimeType = Asset.MimeType.TextPlain;
        var asset = new Asset(data, "test.txt", mimeType);

        // Act
        var dataUrl = asset.ToDataUrl();

        // Assert
        dataUrl.Should().Be("data:text/plain;base64,SGVsbG8=");
    }

    [Fact]
    public void ToActualDataUrl_ShouldUseDetectedMimeType()
    {
        // Arrange - PNG magic bytes but claiming to be JPEG
        var pngMagic = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00 };
        Asset.MimeType mimeType = Asset.MimeType.ImageJpeg;
        var asset = new Asset(pngMagic, "test.jpg", mimeType);

        // Act
        var actualDataUrl = asset.DataUrl;

        // Assert - should detect PNG from magic bytes, not use claimed JPEG
        actualDataUrl.Should().Contain("data:image/png;base64,");
    }

    [Fact]
    public void GetActualMimeType_ShouldDetectFromSignature()
    {
        // Arrange - WebP magic bytes (RIFF....WEBP)
        var webpMagic = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 };
        Asset.MimeType mimeType = Asset.MimeType.ImageJpeg;
        var asset = new Asset(webpMagic, "image.jpg", mimeType);

        // Act
        var actualMimeType = asset.GetActualMimeType();

        // Assert
        actualMimeType.Should().Be(Asset.MimeType.ImageWebp);
    }

    [Fact]
    public void GetActualMimeType_ShouldFallbackToContentType_WhenSignatureUnknown()
    {
        // Arrange - random bytes with explicit MIME type
        Asset.MimeType mimeType = Asset.MimeType.ApplicationJson;
        var asset = new Asset([0x00, 0x01, 0x02], "test.json", mimeType);

        // Act
        var actualMimeType = asset.GetActualMimeType();

        // Assert - should fallback to declared type
        actualMimeType.Should().Be(Asset.MimeType.ApplicationJson);
    }

    [Fact]
    public void ImplicitConversion_FromByteArray_ShouldWork()
    {
        // Arrange
        byte[] data = [0x01, 0x02, 0x03];

        // Act
        Asset asset = data;

        // Assert
        asset.Data.Should().BeEquivalentTo(data);
        asset.HasValue.Should().BeTrue();
    }
}
