namespace Application.Normalization.MediaTypes.TV.Internals.Enums;

internal enum TvShowIdentificationStatus
{
    Matched,
    InvalidCandidate,
    NoExactMatch,
    AmbiguousExactMatches,
    ConflictingFilenameCandidates,
    LookupFailed
}
