namespace Application.Abstractions.Database.Models;

public sealed record MediaFile(
    long Id,
    long? TitleId,
    int MediaTypeId,
    string CurrentPath,
    string? CanonicalPath,
    int? SeasonNumber,
    int? EpisodeNumber,
    DateOnly? AirDate,
    string? EpisodeTitle,
    string? LastStatus,
    string? LastMessage,
    long? FirstSeenRunId,
    long? LastSeenRunId,
    DateTime DateCreated,
    DateTime LastModified,
    bool IsActive);

public sealed record MediaFileUpsert(
    long? Id,
    long? TitleId,
    int MediaTypeId,
    string CurrentPath,
    string? CanonicalPath,
    int? SeasonNumber,
    int? EpisodeNumber,
    DateOnly? AirDate,
    string? EpisodeTitle,
    string? LastStatus,
    string? LastMessage,
    long? FirstSeenRunId,
    long? LastSeenRunId,
    bool IsActive);
