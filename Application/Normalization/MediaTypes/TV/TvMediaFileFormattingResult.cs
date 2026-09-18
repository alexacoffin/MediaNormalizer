namespace Application.Normalization.MediaTypes.TV;

public sealed class TvMediaFileFormattingResult
{
    public TvMediaFileFormattingResult(
        string sourceFilePath,
        string? destinationFilePath,
        TvMediaTypeFormattingStatus status,
        string message)
    {
        SourceFilePath = sourceFilePath;
        DestinationFilePath = destinationFilePath;
        Status = status;
        Message = message;
    }

    public string SourceFilePath { get; }

    public string? DestinationFilePath { get; }

    public TvMediaTypeFormattingStatus Status { get; }

    public string Message { get; }
}
