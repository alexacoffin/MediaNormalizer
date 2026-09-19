using Xunit;

namespace Application.Tests.Normalization.MediaTypes.TV;

public sealed class TvShowMetadataParserTests
{
    [Fact]
    public void TryParse_ReturnsMetadataFromCaseInsensitiveElementsAndImdbUniqueId()
    {
        var metadata = Application.Normalization.MediaTypes.TV.TvShowMetadataParser.TryParse(
            """
            <TVSHOW>
              <UniqueId type="IMDB"> tt1561755 </UniqueId>
              <TITLE> Bob's Burgers </TITLE>
              <YEAR>2011</YEAR>
            </TVSHOW>
            """);

        Assert.NotNull(metadata);
        Assert.Equal("tt1561755", metadata.ImdbId);
        Assert.Equal("Bob's Burgers", metadata.Title);
        Assert.Equal(2011, metadata.Year);
    }

    [Fact]
    public void TryParse_UsesImdbIdElementAndPremieredYearWhenYearIsMissing()
    {
        var metadata = Application.Normalization.MediaTypes.TV.TvShowMetadataParser.TryParse(
            "<tvshow><imdbid>tt1561755</imdbid><title>Bob's Burgers</title><premiered>2011-01-09</premiered></tvshow>");

        Assert.NotNull(metadata);
        Assert.Equal("tt1561755", metadata.ImdbId);
        Assert.Equal("Bob's Burgers", metadata.Title);
        Assert.Equal(2011, metadata.Year);
    }

    [Fact]
    public void TryParse_PrefersExplicitYearOverPremieredYear()
    {
        var metadata = Application.Normalization.MediaTypes.TV.TvShowMetadataParser.TryParse(
            "<tvshow><title>Bob's Burgers</title><year>2011</year><premiered>2020-01-01</premiered></tvshow>");

        Assert.NotNull(metadata);
        Assert.Equal(2011, metadata.Year);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<tvshow />")]
    [InlineData("<tvshow><uniqueid type=\"tmdb\">123</uniqueid></tvshow>")]
    public void TryParse_ReturnsNullWhenContentHasNoUsableMetadata(string? content)
    {
        var metadata = Application.Normalization.MediaTypes.TV.TvShowMetadataParser.TryParse(content);

        Assert.Null(metadata);
    }

    [Theory]
    [InlineData("<tvshow><title>Bob's Burgers</tvshow>")]
    [InlineData("<!DOCTYPE tvshow [<!ENTITY external SYSTEM \"file:///missing\">]><tvshow><title>&external;</title></tvshow>")]
    public void TryParse_ReturnsNullForMalformedOrProhibitedXml(string content)
    {
        var metadata = Application.Normalization.MediaTypes.TV.TvShowMetadataParser.TryParse(content);

        Assert.Null(metadata);
    }
}
