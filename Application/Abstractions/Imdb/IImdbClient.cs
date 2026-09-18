using Application.Abstractions.Imdb.Models;

namespace Application.Abstractions.Imdb;

public interface IImdbClient
{
    Task<ImdbResult<ImdbSearchPage>> SearchAsync(
        string title,
        int? year = null,
        ImdbTitleType? type = null,
        int page = 1,
        CancellationToken cancellationToken = default);

    Task<ImdbResult<ImdbTitleDetails>> GetByIdAsync(
        string imdbId,
        CancellationToken cancellationToken = default);

    Task<ImdbResult<ImdbTitleDetails>> GetEpisodeAsync(
        string seriesImdbId,
        int seasonNumber,
        int episodeNumber,
        CancellationToken cancellationToken = default);
}
