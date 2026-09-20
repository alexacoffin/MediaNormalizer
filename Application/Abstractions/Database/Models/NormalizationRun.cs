namespace Application.Abstractions.Database.Models;

public sealed record NormalizationRun(
    long Id,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    string Status,
    string? ErrorMessage);

public sealed record NormalizationRunUpsert(
    long? Id,
    DateTime? CompletedAtUtc,
    string Status,
    string? ErrorMessage,
    DateTime? StartedAtUtc);
