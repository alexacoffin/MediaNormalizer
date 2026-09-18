using Application.Normalization.MediaTypes.TV;
using Xunit;

namespace Application.Tests.Normalization.MediaTypes.TV;

public sealed class TvFilenameParserTests
{
    [Theory]
    [InlineData("The.Bear.S02E03.1080p.WEB-DL.x265.mkv", "The Bear")]
    [InlineData("The_Bear_2x03_1080p_WEBRip.mkv", "The Bear")]
    [InlineData("The.Bear.2024-06-26.1080p.HDTV.mkv", "The Bear")]
    [InlineData("The Bear - 003 [1080p].mkv", "The Bear")]
    [InlineData("The Bear Season 2 Episode 3 WEB-DL.mkv", "The Bear")]
    [InlineData("The Bear Episode 12 720p.mkv", "The Bear")]
    public void TryParse_ExtractsTitlesFromSupportedEpisodeConventions(
        string fileName,
        string expectedTitle)
    {
        var candidate = TvFilenameParser.TryParse(fileName);

        Assert.Equal(expectedTitle, candidate?.Title);
    }

    [Fact]
    public void TryParse_ReturnsNullWhenNoEpisodeConventionIsPresent()
    {
        var candidate = TvFilenameParser.TryParse("The.Bear.1080p.WEB-DL.mkv");

        Assert.Null(candidate);
    }

    [Fact]
    public void TryParse_ExtractsSeasonAndEpisodeNumbers()
    {
        var candidate = TvFilenameParser.TryParse("The.Bear.S02E03.mkv");

        Assert.Equal(2, candidate?.SeasonNumber);
        Assert.Equal(3, candidate?.EpisodeNumber);
        Assert.Null(candidate?.AirDate);
    }

    [Fact]
    public void TryParse_ExtractsAirDate()
    {
        var candidate = TvFilenameParser.TryParse("The.Daily.Show.2024-06-26.mkv");

        Assert.Equal(new DateOnly(2024, 6, 26), candidate?.AirDate);
    }

    [Fact]
    public void TryParse_IdentifiesMultiEpisodeFiles()
    {
        var candidate = TvFilenameParser.TryParse("The.Bear.S01E01E02.mkv");

        Assert.True(candidate?.IsMultiEpisode);
    }
}
