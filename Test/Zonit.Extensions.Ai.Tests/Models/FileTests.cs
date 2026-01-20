using FluentAssertions;
using Xunit;

namespace Zonit.Extensions.Ai.Tests.Models;

/// <summary>
/// Tests for File model - IsImage, IsDocument, IsAudio detection.
/// </summary>
public class FileTests
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
        var file = new File
        {
            Name = "test.file",
            MimeType = mimeType,
            Data = [0x00]
        };

        // Act & Assert
        file.IsImage.Should().Be(expected);
    }

    [Theory]
    [InlineData("application/pdf", true)]
    [InlineData("application/msword", true)]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", true)]
    [InlineData("text/plain", true)]
    [InlineData("image/png", false)]
    [InlineData("audio/mpeg", false)]
    [InlineData("application/json", false)]
    public void IsDocument_ShouldDetectDocumentMimeTypes(string mimeType, bool expected)
    {
        // Arrange
        var file = new File
        {
            Name = "test.file",
            MimeType = mimeType,
            Data = [0x00]
        };

        // Act & Assert
        file.IsDocument.Should().Be(expected);
    }

    [Theory]
    [InlineData("audio/mpeg", true)]
    [InlineData("audio/wav", true)]
    [InlineData("audio/ogg", true)]
    [InlineData("audio/mp3", true)]
    [InlineData("image/png", false)]
    [InlineData("application/pdf", false)]
    public void IsAudio_ShouldDetectAudioMimeTypes(string mimeType, bool expected)
    {
        // Arrange
        var file = new File
        {
            Name = "test.file",
            MimeType = mimeType,
            Data = [0x00]
        };

        // Act & Assert
        file.IsAudio.Should().Be(expected);
    }

    [Fact]
    public void ToBase64_ShouldEncodeDataCorrectly()
    {
        // Arrange
        var data = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
        var file = new File
        {
            Name = "test.txt",
            MimeType = "text/plain",
            Data = data
        };

        // Act
        var base64 = file.ToBase64();

        // Assert
        base64.Should().Be("SGVsbG8=");
    }

    [Fact]
    public void ToDataUrl_ShouldFormatCorrectly()
    {
        // Arrange
        var data = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
        var file = new File
        {
            Name = "test.txt",
            MimeType = "text/plain",
            Data = data
        };

        // Act
        var dataUrl = file.ToDataUrl();

        // Assert
        dataUrl.Should().Be("data:text/plain;base64,SGVsbG8=");
    }

    [Fact]
    public void FromBytes_ShouldCreateFileCorrectly()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03 };

        // Act
        var file = File.FromBytes(data, "application/octet-stream", "test.bin");

        // Assert
        file.Name.Should().Be("test.bin");
        file.MimeType.Should().Be("application/octet-stream");
        file.Data.Should().BeEquivalentTo(data);
    }
}
