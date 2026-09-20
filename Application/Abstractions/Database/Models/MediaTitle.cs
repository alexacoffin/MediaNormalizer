namespace Application.Abstractions.Database.Models;

public sealed record MediaTitle(
    long Id,
    int MediaTypeId,
    string? OmdbEntryId,
    string? Name,
    short? ReleaseYear,
    DateTime DateCreated,
    DateTime LastModified,
    long? LastSeenRunId,
    bool IsActive);

public sealed record MediaTitleUpsert(
    long? Id,
    int MediaTypeId,
    string? OmdbEntryId,
    string? Name,
    short? ReleaseYear,
    long? LastSeenRunId,
    bool IsActive);
