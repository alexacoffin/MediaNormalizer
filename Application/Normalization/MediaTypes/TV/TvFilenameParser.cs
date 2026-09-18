using Application.Normalization.MediaTypes.TV.Internals;
using System.Text.RegularExpressions;

namespace Application.Normalization.MediaTypes.TV;

internal static class TvFilenameParser
{
    private static readonly Regex[] EpisodePatterns =
    [
        // Season-and-episode notation, such as S01E02 or S01 E02.
        new(@"\bS(?<season>\d{1,3})[ ._-]*E(?<episode>\d{1,3})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        // Season x episode notation, such as 1x02 or 1 x 02.
        new(@"(?<!\d)(?<season>\d{1,3})[ ._-]*x[ ._-]*(?<episode>\d{1,3})(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        // Date-based episode notation, such as 2024-05-17.
        new(@"\b(?<year>(?:19|20)\d{2})[ ._-](?<month>\d{2})[ ._-](?<day>\d{2})\b", RegexOptions.CultureInvariant),
        // Fully written season-and-episode notation, such as Season 1 Episode 2.
        new(@"\bSeason\s*(?<season>\d{1,3})\s*Episode\s*(?<episode>\d{1,3})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        // Episode-only notation, such as Episode 2.
        new(@"\bEpisode\s*(?<episode>\d{1,3})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        // Dash-separated episode number, such as "Show Title - 2".
        new(@"\s+-\s+(?<episode>\d{1,3})(?=\s|$|\[|\()", RegexOptions.CultureInvariant)
    ];

    private static readonly Regex[] MultiEpisodePatterns =
    [
        new(@"\bS(?<season>\d{1,3})[ ._-]*E(?<episode>\d{1,3})(?:[ ._-]*E\d{1,3})+\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"(?<!\d)(?<season>\d{1,3})[ ._-]*x[ ._-]*(?<episode>\d{1,3})(?:[ ._-]*(?:\d{1,3}[ ._-]*x[ ._-]*)?\d{1,3})+\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
    ];

    private static readonly HashSet<string> ReleaseNoiseTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "480p", "576p", "720p", "1080p", "1440p", "2160p", "4k", "8k",
        "web", "webdl", "webrip", "hdtv", "bluray", "bdrip", "dvdrip", "remux",
        "x264", "x265", "h264", "h265", "hevc", "av1", "xvid",
        "hdr", "hdr10", "hdr10plus", "dolbyvision", "dv",
        "aac", "ac3", "eac3", "dts", "atmos", "truehd", "flac"
    };

    public static TvFilenameCandidate? TryParse(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var marker = EpisodePatterns
            .Select(pattern => pattern.Match(fileName))
            .Concat(MultiEpisodePatterns.Select(pattern => pattern.Match(fileName)))
            .Where(match => match.Success)
            .OrderBy(match => match.Index)
            .FirstOrDefault();

        if (marker is null)
        {
            return null;
        }

        var candidate = RemoveReleaseNoise(fileName[..marker.Index]);
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var seasonNumber = ParseNumber(marker, "season");
        var episodeNumber = ParseNumber(marker, "episode");
        var airDate = ParseAirDate(marker);
        var isMultiEpisode = MultiEpisodePatterns.Any(pattern => pattern.IsMatch(fileName));
        return new TvFilenameCandidate(
            candidate,
            seasonNumber,
            episodeNumber,
            airDate,
            isMultiEpisode);
    }

    private static int? ParseNumber(Match match, string groupName) =>
        int.TryParse(match.Groups[groupName].Value, out var number) && number > 0
            ? number
            : null;

    private static DateOnly? ParseAirDate(Match match)
    {
        if (!int.TryParse(match.Groups["year"].Value, out var year)
            || !int.TryParse(match.Groups["month"].Value, out var month)
            || !int.TryParse(match.Groups["day"].Value, out var day))
        {
            return null;
        }

        return DateOnly.TryParse($"{year:D4}-{month:D2}-{day:D2}", out var airDate)
            ? airDate
            : null;
    }

    private static string RemoveReleaseNoise(string titlePrefix)
    {
        var tokens = titlePrefix
            .Replace('.', ' ')
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var titleTokens = new List<string>();

        foreach (var token in tokens)
        {
            if (IsReleaseNoise(token))
            {
                break;
            }

            titleTokens.Add(token.Trim('[', ']', '(', ')'));
        }

        return string.Join(' ', titleTokens).Trim();
    }

    private static bool IsReleaseNoise(string token)
    {
        var normalized = token
            .Trim('[', ']', '(', ')')
            .Replace("+", "plus", StringComparison.Ordinal)
            .ToLowerInvariant();

        return ReleaseNoiseTokens.Contains(normalized)
            || Regex.IsMatch(normalized, @"^(?:ddp?|eac3|ac3|dts)\d*(?:\.\d+)?$", RegexOptions.CultureInvariant);
    }
}
