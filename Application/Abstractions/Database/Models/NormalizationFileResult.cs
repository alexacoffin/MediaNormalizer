namespace Application.Abstractions.Database.Models;

public sealed record NormalizationFileResult(
    long Id,
    long NormalizationRunId,
    long? MediaFileId,
    long? MediaTitleId,
    int MediaTypeId,
    string SourcePath,
    string? DestinationPath,
    string Status,
    string Message,
    string SourceRole,
    DateTime CreatedAtUtc);

public sealed record NormalizationFileResultUpsert(
    long? Id,
    long NormalizationRunId,
    long? MediaFileId,
    long? MediaTitleId,
    int MediaTypeId,
    string SourcePath,
    string? DestinationPath,
    string Status,
    string Message,
    string SourceRole);
