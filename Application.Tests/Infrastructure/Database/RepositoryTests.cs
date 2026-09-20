using System.Data;
using Application.Abstractions.Database;
using Application.Abstractions.Database.Models;
using Infrastructure.Database;
using Xunit;
using static Application.Tests.Infrastructure.Database.RepositoryTestAssertions;

namespace Application.Tests.Infrastructure.Database;

public sealed class NormalizationRunsRepositoryTests
{
    [Fact]
    public async Task UpsertAndDelete_CallExpectedProceduresAndMapRows()
    {
        var factory = new FakeDbConnectionFactory();
        var repository = new NormalizationRunsRepository(factory);
        var expected = new NormalizationRun(
            7,
            new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            new DateTime(2026, 1, 2, 3, 5, 5, DateTimeKind.Utc),
            "Completed",
            null);
        factory.Connection.Rows = [RunRow(expected)];

        var actual = await repository.UpsertAsync(
            new NormalizationRunUpsert(
                null,
                expected.CompletedAtUtc,
                expected.Status,
                expected.ErrorMessage,
                expected.StartedAtUtc));

        Assert.Equal(expected, actual);
        AssertCommand(factory.Connection.Commands[0], "dbo.NormalizationRuns_Upsert");
        AssertParameter(factory.Connection.Commands[0], "@Id", DbType.Int64, DBNull.Value);
        AssertParameter(factory.Connection.Commands[0], "@Status", DbType.AnsiString, "Completed", 20);

        factory.Connection.Rows = [RunRow(expected)];
        actual = await repository.DeleteAsync(expected.Id);

        Assert.Equal(expected, actual);
        AssertCommand(factory.Connection.Commands[1], "dbo.NormalizationRuns_Delete");
        AssertParameter(factory.Connection.Commands[1], "@Id", DbType.Int64, expected.Id);
    }

    private static IReadOnlyDictionary<string, object?> RunRow(NormalizationRun run) =>
        new Dictionary<string, object?>
        {
            ["Id"] = run.Id,
            ["StartedAtUtc"] = run.StartedAtUtc,
            ["CompletedAtUtc"] = run.CompletedAtUtc,
            ["Status"] = run.Status,
            ["ErrorMessage"] = run.ErrorMessage
        };
}

public sealed class MediaTitlesRepositoryTests
{
    [Fact]
    public async Task AllMethods_CallExpectedProceduresAndHonorInactiveFilter()
    {
        var factory = new FakeDbConnectionFactory();
        var repository = new MediaTitlesRepository(factory);
        var expected = new MediaTitle(
            11,
            1,
            "tt1234567",
            "Example",
            2026,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            4,
            true);

        factory.Connection.Rows = [TitleRow(expected)];
        Assert.Equal(
            expected,
            await repository.UpsertAsync(
                new MediaTitleUpsert(
                    null,
                    expected.MediaTypeId,
                    expected.OmdbEntryId,
                    expected.Name,
                    expected.ReleaseYear,
                    expected.LastSeenRunId,
                    expected.IsActive)));
        AssertCommand(factory.Connection.Commands[0], "dbo.MediaTitles_Upsert");
        AssertParameter(factory.Connection.Commands[0], "@OmdbEntryId", DbType.AnsiString, "tt1234567", 32);

        factory.Connection.Rows = [TitleRow(expected)];
        Assert.Equal(expected, await repository.DeleteAsync(expected.Id));
        AssertCommand(factory.Connection.Commands[1], "dbo.MediaTitles_Delete");

        factory.Connection.Rows = [];
        Assert.Null(await repository.GetByIdAsync(expected.Id));
        AssertCommand(factory.Connection.Commands[2], "dbo.MediaTitles_GetById");
        AssertParameter(factory.Connection.Commands[2], "@IncludeInactive", DbType.Boolean, false);

        factory.Connection.Rows = [TitleRow(expected)];
        Assert.Equal(
            expected,
            await repository.GetByOmdbEntryIdAsync(expected.MediaTypeId, expected.OmdbEntryId!, true));
        AssertCommand(factory.Connection.Commands[3], "dbo.MediaTitles_GetByOmdbEntryId");
        AssertParameter(factory.Connection.Commands[3], "@IncludeInactive", DbType.Boolean, true);

        factory.Connection.Rows = [TitleRow(expected)];
        Assert.Equal([expected], await repository.GetActiveByMediaTypeAsync(expected.MediaTypeId));
        AssertCommand(factory.Connection.Commands[4], "dbo.MediaTitles_GetActiveByMediaType");
        AssertParameter(factory.Connection.Commands[4], "@MediaTypeId", DbType.Int32, expected.MediaTypeId);
    }

    private static IReadOnlyDictionary<string, object?> TitleRow(MediaTitle title) =>
        new Dictionary<string, object?>
        {
            ["Id"] = title.Id,
            ["MediaTypeId"] = title.MediaTypeId,
            ["OmdbEntryId"] = title.OmdbEntryId,
            ["Name"] = title.Name,
            ["ReleaseYear"] = title.ReleaseYear,
            ["DateCreated"] = title.DateCreated,
            ["LastModified"] = title.LastModified,
            ["LastSeenRunId"] = title.LastSeenRunId,
            ["IsActive"] = title.IsActive
        };
}

public sealed class MediaFilesRepositoryTests
{
    [Fact]
    public async Task AllMethods_CallExpectedProceduresMapNullableFieldsAndPreserveReaderOrder()
    {
        var factory = new FakeDbConnectionFactory();
        var repository = new MediaFilesRepository(factory);
        var first = CreateMediaFile(21, "z:\\last.mkv");
        var second = CreateMediaFile(22, "z:\\next.mkv");
        var request = new MediaFileUpsert(
            null,
            first.TitleId,
            first.MediaTypeId,
            first.CurrentPath,
            first.CanonicalPath,
            first.SeasonNumber,
            first.EpisodeNumber,
            first.AirDate,
            first.EpisodeTitle,
            first.LastStatus,
            first.LastMessage,
            first.FirstSeenRunId,
            first.LastSeenRunId,
            first.IsActive);

        factory.Connection.Rows = [FileRow(first)];
        Assert.Equal(first, await repository.UpsertAsync(request));
        AssertCommand(factory.Connection.Commands[0], "dbo.MediaFiles_Upsert");
        AssertParameter(
            factory.Connection.Commands[0],
            "@AirDate",
            DbType.Date,
            first.AirDate!.Value.ToDateTime(TimeOnly.MinValue));
        AssertParameter(factory.Connection.Commands[0], "@CanonicalPath", DbType.String, DBNull.Value, 2048);

        factory.Connection.Rows = [FileRow(first)];
        Assert.Equal(first, await repository.DeleteAsync(first.Id));
        AssertCommand(factory.Connection.Commands[1], "dbo.MediaFiles_Delete");

        factory.Connection.Rows = [FileRow(first)];
        Assert.Equal(first, await repository.GetByIdAsync(first.Id, true));
        AssertCommand(factory.Connection.Commands[2], "dbo.MediaFiles_GetById");
        AssertParameter(factory.Connection.Commands[2], "@IncludeInactive", DbType.Boolean, true);

        factory.Connection.Rows = [FileRow(first), FileRow(second)];
        var files = await repository.GetByTitleIdAsync(first.TitleId!.Value);
        Assert.Equal([first, second], files);
        AssertCommand(factory.Connection.Commands[3], "dbo.MediaFiles_GetByTitleId");
        AssertParameter(factory.Connection.Commands[3], "@IncludeInactive", DbType.Boolean, false);

        factory.Connection.Rows = [];
        Assert.Empty(await repository.GetByTitleIdAsync(first.TitleId.Value));

        factory.Connection.Rows = [];
        Assert.Null(await repository.GetByCurrentPathAsync(first.CurrentPath));
        AssertCommand(factory.Connection.Commands[5], "dbo.MediaFiles_GetByCurrentPath");

        factory.Connection.Rows = [FileRow(first), FileRow(second)];
        Assert.Equal([first, second], await repository.GetActiveByMediaTypeAsync(first.MediaTypeId));
        AssertCommand(factory.Connection.Commands[6], "dbo.MediaFiles_GetActiveByMediaType");
        AssertParameter(factory.Connection.Commands[6], "@MediaTypeId", DbType.Int32, first.MediaTypeId);
    }

    [Fact]
    public async Task DatabaseCancellationToken_IsForwardedToConnectionAndCommand()
    {
        var factory = new FakeDbConnectionFactory();
        var repository = new MediaFilesRepository(factory);
        var cancellationToken = new CancellationTokenSource().Token;
        factory.Connection.Rows = [];

        await repository.GetByIdAsync(21, cancellationToken: cancellationToken);

        Assert.Equal(cancellationToken, factory.Connection.OpenCancellationToken);
        Assert.Equal(cancellationToken, factory.Connection.Commands[0].ExecuteCancellationToken);
        Assert.True(factory.Connection.IsDisposed);
    }

    private static MediaFile CreateMediaFile(long id, string currentPath) =>
        new(
            id,
            11,
            1,
            currentPath,
            null,
            1,
            2,
            new DateOnly(2026, 1, 3),
            "Pilot",
            "Renamed",
            "Moved",
            4,
            5,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            true);

    private static IReadOnlyDictionary<string, object?> FileRow(MediaFile file) =>
        new Dictionary<string, object?>
        {
            ["Id"] = file.Id,
            ["TitleId"] = file.TitleId,
            ["MediaTypeId"] = file.MediaTypeId,
            ["CurrentPath"] = file.CurrentPath,
            ["CanonicalPath"] = file.CanonicalPath,
            ["SeasonNumber"] = file.SeasonNumber,
            ["EpisodeNumber"] = file.EpisodeNumber,
            ["AirDate"] = file.AirDate,
            ["EpisodeTitle"] = file.EpisodeTitle,
            ["LastStatus"] = file.LastStatus,
            ["LastMessage"] = file.LastMessage,
            ["FirstSeenRunId"] = file.FirstSeenRunId,
            ["LastSeenRunId"] = file.LastSeenRunId,
            ["DateCreated"] = file.DateCreated,
            ["LastModified"] = file.LastModified,
            ["IsActive"] = file.IsActive
        };
}

public sealed class HistoryRepositoryTests
{
    [Fact]
    public async Task NormalizationFileResultsRepository_CallsUpsertAndDelete()
    {
        var factory = new FakeDbConnectionFactory();
        var repository = new NormalizationFileResultsRepository(factory);
        var expected = new NormalizationFileResult(
            31,
            4,
            21,
            11,
            1,
            "z:\\source.mkv",
            "z:\\destination.mkv",
            "Renamed",
            "Moved",
            "Intake",
            DateTime.UtcNow);

        factory.Connection.Rows = [ResultRow(expected)];
        Assert.Equal(
            expected,
            await repository.UpsertAsync(
                new NormalizationFileResultUpsert(
                    null,
                    expected.NormalizationRunId,
                    expected.MediaFileId,
                    expected.MediaTitleId,
                    expected.MediaTypeId,
                    expected.SourcePath,
                    expected.DestinationPath,
                    expected.Status,
                    expected.Message,
                    expected.SourceRole)));
        AssertCommand(factory.Connection.Commands[0], "dbo.NormalizationFileResults_Upsert");
        AssertParameter(factory.Connection.Commands[0], "@SourceRole", DbType.AnsiString, "Intake", 20);

        factory.Connection.Rows = [ResultRow(expected)];
        Assert.Equal(expected, await repository.DeleteAsync(expected.Id));
        AssertCommand(factory.Connection.Commands[1], "dbo.NormalizationFileResults_Delete");
    }

    [Fact]
    public async Task NormalizationDeletedDirectoriesRepository_CallsUpsertAndDelete()
    {
        var factory = new FakeDbConnectionFactory();
        var repository = new NormalizationDeletedDirectoriesRepository(factory);
        var expected = new NormalizationDeletedDirectory(
            41,
            4,
            1,
            "z:\\empty",
            DateTime.UtcNow);

        factory.Connection.Rows = [DirectoryRow(expected)];
        Assert.Equal(
            expected,
            await repository.UpsertAsync(
                new NormalizationDeletedDirectoryUpsert(
                    null,
                    expected.NormalizationRunId,
                    expected.MediaTypeId,
                    expected.Path)));
        AssertCommand(factory.Connection.Commands[0], "dbo.NormalizationDeletedDirectories_Upsert");

        factory.Connection.Rows = [DirectoryRow(expected)];
        Assert.Equal(expected, await repository.DeleteAsync(expected.Id));
        AssertCommand(factory.Connection.Commands[1], "dbo.NormalizationDeletedDirectories_Delete");
    }

    private static IReadOnlyDictionary<string, object?> ResultRow(NormalizationFileResult result) =>
        new Dictionary<string, object?>
        {
            ["Id"] = result.Id,
            ["NormalizationRunId"] = result.NormalizationRunId,
            ["MediaFileId"] = result.MediaFileId,
            ["MediaTitleId"] = result.MediaTitleId,
            ["MediaTypeId"] = result.MediaTypeId,
            ["SourcePath"] = result.SourcePath,
            ["DestinationPath"] = result.DestinationPath,
            ["Status"] = result.Status,
            ["Message"] = result.Message,
            ["SourceRole"] = result.SourceRole,
            ["CreatedAtUtc"] = result.CreatedAtUtc
        };

    private static IReadOnlyDictionary<string, object?> DirectoryRow(NormalizationDeletedDirectory directory) =>
        new Dictionary<string, object?>
        {
            ["Id"] = directory.Id,
            ["NormalizationRunId"] = directory.NormalizationRunId,
            ["MediaTypeId"] = directory.MediaTypeId,
            ["Path"] = directory.Path,
            ["DeletedAtUtc"] = directory.DeletedAtUtc
        };
}

public sealed class SqlConnectionFactoryTests
{
    [Fact]
    public void MissingConnectionString_FailsWhenConnectionIsRequested()
    {
        var factory = new SqlConnectionFactory(null);

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateConnection());

        Assert.Contains("ConnectionStrings:MediaNormalizer", exception.Message);
    }
}

internal static class RepositoryTestAssertions
{
    public static void AssertCommand(FakeDbCommand command, string expectedProcedure)
    {
        Assert.Equal(expectedProcedure, command.CommandText);
        Assert.Equal(CommandType.StoredProcedure, command.CommandType);
    }

    public static void AssertParameter(
        FakeDbCommand command,
        string name,
        DbType type,
        object? value,
        int? size = null)
    {
        var parameter = Assert.Single(
            command.Parameters.Cast<FakeDbParameter>(),
            parameter => parameter.ParameterName == name);

        Assert.Equal(type, parameter.DbType);
        Assert.Equal(value, parameter.Value);
        if (size.HasValue)
        {
            Assert.Equal(size.Value, parameter.Size);
        }
    }
}
