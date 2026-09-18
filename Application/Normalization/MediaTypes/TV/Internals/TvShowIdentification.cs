using Application.Normalization.MediaTypes.TV.Internals.Enums;

namespace Application.Normalization.MediaTypes.TV.Internals;

internal sealed class TvShowIdentification
{
    public TvShowIdentification(
        TvShowFolderGroup? showFolderGroup,
        TvUnsupportedFileGroup? looseFileGroup,
        TvShowMatchAttempt[] attempts)
    {
        ShowFolderGroup = showFolderGroup;
        LooseFileGroup = looseFileGroup;
        Attempts = attempts;
    }

    public TvShowFolderGroup? ShowFolderGroup { get; }

    public TvUnsupportedFileGroup? LooseFileGroup { get; }

    public TvShowMatchAttempt[] Attempts { get; }

    public TvShowMatchAttempt FinalAttempt => Attempts[^1];

    public string CandidateTitle => FinalAttempt.CandidateTitle;

    public int? CandidateYear => FinalAttempt.CandidateYear;

    public TvShowIdentificationStatus Status => FinalAttempt.Status;

    public Abstractions.Imdb.Models.ImdbTitleSummary? MatchedSeries =>
        FinalAttempt.MatchedSeries;

    public Abstractions.Imdb.Models.ImdbError? LookupError =>
        FinalAttempt.LookupError;
}
