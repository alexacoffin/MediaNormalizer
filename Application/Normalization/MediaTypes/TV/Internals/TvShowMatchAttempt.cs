using Application.Abstractions.Imdb.Models;
using Application.Normalization.MediaTypes.TV.Internals.Enums;

namespace Application.Normalization.MediaTypes.TV.Internals;

internal sealed class TvShowMatchAttempt
{
    public TvShowMatchAttempt(
        TvShowEvidenceSource evidenceSource,
        string candidateTitle,
        int? candidateYear,
        TvShowIdentificationStatus status,
        ImdbTitleSummary? matchedSeries,
        ImdbError? lookupError)
    {
        EvidenceSource = evidenceSource;
        CandidateTitle = candidateTitle;
        CandidateYear = candidateYear;
        Status = status;
        MatchedSeries = matchedSeries;
        LookupError = lookupError;
    }

    public TvShowEvidenceSource EvidenceSource { get; }

    public string CandidateTitle { get; }

    public int? CandidateYear { get; }

    public TvShowIdentificationStatus Status { get; }

    public ImdbTitleSummary? MatchedSeries { get; }

    public ImdbError? LookupError { get; }
}
