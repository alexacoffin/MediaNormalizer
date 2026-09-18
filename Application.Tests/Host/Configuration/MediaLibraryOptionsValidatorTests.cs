using Domain.Enums;
using Host.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace Application.Tests.Host.Configuration;

public sealed class MediaLibraryOptionsValidatorTests
{
    private readonly MediaLibraryOptionsValidator validator = new();

    [Fact]
    public void Validate_EnabledTvWithAbsoluteOutputDirectory_Succeeds()
    {
        var result = validator.Validate(
            Options.DefaultName,
            new MediaLibraryOptions
            {
                MediaTypes =
                [
                    new MediaTypeOptions
                    {
                        Id = MediaType.Tv,
                        Enabled = true,
                        OutputDirectory = "/mnt/nas/normalized/tv"
                    }
                ]
            });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative/output")]
    public void Validate_EnabledTvWithInvalidOutputDirectory_Fails(string outputDirectory)
    {
        var result = validator.Validate(
            Options.DefaultName,
            new MediaLibraryOptions
            {
                MediaTypes =
                [
                    new MediaTypeOptions
                    {
                        Id = MediaType.Tv,
                        Enabled = true,
                        OutputDirectory = outputDirectory
                    }
                ]
            });

        Assert.False(result.Succeeded);
        Assert.Contains("OutputDirectory", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_DisabledTvWithMissingOutputDirectory_Succeeds()
    {
        var result = validator.Validate(
            Options.DefaultName,
            new MediaLibraryOptions
            {
                MediaTypes =
                [
                    new MediaTypeOptions
                    {
                        Id = MediaType.Tv,
                        Enabled = false
                    }
                ]
            });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_EnabledMovieWithMissingOutputDirectory_Succeeds()
    {
        var result = validator.Validate(
            Options.DefaultName,
            new MediaLibraryOptions
            {
                MediaTypes =
                [
                    new MediaTypeOptions
                    {
                        Id = MediaType.Movies,
                        Enabled = true
                    }
                ]
            });

        Assert.True(result.Succeeded);
    }
}
