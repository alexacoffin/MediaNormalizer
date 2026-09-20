CREATE PROCEDURE [dbo].[MediaFiles_Upsert]
    @Id             bigint = NULL,
    @TitleId        bigint = NULL,
    @MediaTypeId    int,
    @CurrentPath    nvarchar(2048),
    @CanonicalPath  nvarchar(2048) = NULL,
    @SeasonNumber   int = NULL,
    @EpisodeNumber  int = NULL,
    @AirDate        date = NULL,
    @EpisodeTitle   nvarchar(512) = NULL,
    @LastStatus     varchar(32) = NULL,
    @LastMessage    nvarchar(4000) = NULL,
    @FirstSeenRunId bigint = NULL,
    @LastSeenRunId  bigint = NULL,
    @IsActive       bit = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @PersistedId bigint;

        IF @Id IS NOT NULL
        BEGIN
            UPDATE [dbo].[MediaFiles]
            SET [TitleId] = @TitleId,
                [MediaTypeId] = @MediaTypeId,
                [CurrentPath] = @CurrentPath,
                [CanonicalPath] = @CanonicalPath,
                [SeasonNumber] = @SeasonNumber,
                [EpisodeNumber] = @EpisodeNumber,
                [AirDate] = @AirDate,
                [EpisodeTitle] = @EpisodeTitle,
                [LastStatus] = @LastStatus,
                [LastMessage] = @LastMessage,
                [FirstSeenRunId] = @FirstSeenRunId,
                [LastSeenRunId] = @LastSeenRunId,
                [IsActive] = @IsActive,
                [LastModified] = CONVERT(datetime2(3), SYSUTCDATETIME())
            WHERE [Id] = @Id;

            IF @@ROWCOUNT = 0
                THROW 50001, 'Media file was not found.', 1;

            SET @PersistedId = @Id;
        END
        ELSE
        BEGIN
            SELECT @PersistedId = [Id]
            FROM [dbo].[MediaFiles] WITH (UPDLOCK, HOLDLOCK)
            WHERE [CurrentPath] = @CurrentPath;

            IF @PersistedId IS NULL
            BEGIN
                INSERT INTO [dbo].[MediaFiles]
                (
                    [TitleId], [MediaTypeId], [CurrentPath], [CanonicalPath],
                    [SeasonNumber], [EpisodeNumber], [AirDate], [EpisodeTitle],
                    [LastStatus], [LastMessage], [FirstSeenRunId], [LastSeenRunId], [IsActive]
                )
                VALUES
                (
                    @TitleId, @MediaTypeId, @CurrentPath, @CanonicalPath,
                    @SeasonNumber, @EpisodeNumber, @AirDate, @EpisodeTitle,
                    @LastStatus, @LastMessage, @FirstSeenRunId, @LastSeenRunId, @IsActive
                );

                SET @PersistedId = CONVERT(bigint, SCOPE_IDENTITY());
            END
            ELSE
            BEGIN
                UPDATE [dbo].[MediaFiles]
                SET [TitleId] = @TitleId,
                    [MediaTypeId] = @MediaTypeId,
                    [CanonicalPath] = @CanonicalPath,
                    [SeasonNumber] = @SeasonNumber,
                    [EpisodeNumber] = @EpisodeNumber,
                    [AirDate] = @AirDate,
                    [EpisodeTitle] = @EpisodeTitle,
                    [LastStatus] = @LastStatus,
                    [LastMessage] = @LastMessage,
                    [FirstSeenRunId] = @FirstSeenRunId,
                    [LastSeenRunId] = @LastSeenRunId,
                    [IsActive] = @IsActive,
                    [LastModified] = CONVERT(datetime2(3), SYSUTCDATETIME())
                WHERE [Id] = @PersistedId;
            END;
        END;

        COMMIT TRANSACTION;

        SELECT [Id], [TitleId], [MediaTypeId], [CurrentPath], [CanonicalPath],
               [SeasonNumber], [EpisodeNumber], [AirDate], [EpisodeTitle],
               [LastStatus], [LastMessage], [FirstSeenRunId], [LastSeenRunId],
               [DateCreated], [LastModified], [IsActive]
        FROM [dbo].[MediaFiles]
        WHERE [Id] = @PersistedId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH;
END;
