using Application.Normalization.MediaTypes.TV.Internals;
using System.Xml;
using System.Xml.Linq;

namespace Application.Normalization.MediaTypes.TV;

internal static class TvShowMetadataParser
{
    public static TvShowMetadata? TryParse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var stringReader = new StringReader(content);
            using var xmlReader = XmlReader.Create(stringReader, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
            var document = XDocument.Load(xmlReader);
            var metadata = new TvShowMetadata(
                FindImdbId(document),
                FindElementValue(document, "title"),
                FindYear(document));

            return metadata.ImdbId is null && metadata.Title is null
                ? null
                : metadata;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static string? FindImdbId(XDocument document)
    {
        var values = document
            .Descendants()
            .Where(element =>
                string.Equals(element.Name.LocalName, "imdbid", StringComparison.OrdinalIgnoreCase)
                || (string.Equals(element.Name.LocalName, "uniqueid", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        element.Attribute("type")?.Value,
                        "imdb",
                        StringComparison.OrdinalIgnoreCase)))
            .Select(element => element.Value.Trim());

        return values.FirstOrDefault(value =>
            value.StartsWith("tt", StringComparison.OrdinalIgnoreCase));
    }

    private static int? FindYear(XDocument document)
    {
        var year = FindElementValue(document, "year");
        if (int.TryParse(year, out var parsedYear))
        {
            return parsedYear;
        }

        var premiered = FindElementValue(document, "premiered");
        return premiered is { Length: >= 4 }
            && int.TryParse(premiered[..4], out parsedYear)
                ? parsedYear
                : null;
    }

    private static string? FindElementValue(XDocument document, string elementName) =>
        document
            .Descendants()
            .FirstOrDefault(element => string.Equals(
                element.Name.LocalName,
                elementName,
                StringComparison.OrdinalIgnoreCase))?
            .Value
            .Trim();
}
