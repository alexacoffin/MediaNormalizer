namespace Application.Normalization.MediaTypes.TV.Internals;

internal sealed class TvIdentificationRunResult
{
    public TvIdentificationRunResult(
        TvShowIdentification[] showIdentifications,
        TvShowIdentification[] looseFileIdentifications)
    {
        ShowIdentifications = showIdentifications;
        LooseFileIdentifications = looseFileIdentifications;
    }

    public TvShowIdentification[] ShowIdentifications { get; }

    public TvShowIdentification[] LooseFileIdentifications { get; }
}
