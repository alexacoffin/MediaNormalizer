namespace Application.Abstractions.Database.Models;

public sealed record NormalizationDeletedDirectory(
    long Id,
    long NormalizationRunId,
    int MediaTypeId,
    string Path,
    DateTime DeletedAtUtc);

public sealed record NormalizationDeletedDirectoryUpsert(
    long? Id,
    long NormalizationRunId,
    int MediaTypeId,
    string Path);
