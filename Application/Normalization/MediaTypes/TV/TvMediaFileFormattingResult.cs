using Application.Normalization;

namespace Application.Normalization.MediaTypes.TV;

public sealed class TvMediaFileFormattingResult
{
    public TvMediaFileFormattingResult(
        string sourceFilePath,
        string? destinationFilePath,
        MediaFileNormalizationStatus status,
        string message)
    {
        SourceFilePath = sourceFilePath;
        DestinationFilePath = destinationFilePath;
        Status = status;
        Message = message;
    }

    public string SourceFilePath { get; }

    public string? DestinationFilePath { get; }

    public MediaFileNormalizationStatus Status { get; }

    public string Message { get; }
}
