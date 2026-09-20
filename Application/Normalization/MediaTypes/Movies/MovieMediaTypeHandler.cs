using Application.Abstractions.Database;
using Microsoft.Extensions.Logging;
using Application.Normalization;

namespace Application.Normalization.MediaTypes.Movies;

public sealed class MovieMediaTypeHandler : MediaTypeHandlerBase
{
    public MovieMediaTypeHandler(
        string[] locations,
        INormalizationRunsRepository? runsRepository = null,
        ILogger? logger = null)
        : base(runsRepository, logger)
    {
    }

    public override Task<MediaTypeNormalizationResult> Normalize(CancellationToken cancellationToken = default) =>
        Task.FromResult(MediaTypeNormalizationResult.Empty);

}
