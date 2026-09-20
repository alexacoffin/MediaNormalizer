CREATE PROCEDURE [dbo].[MediaTitles_Upsert]
    @Id            bigint = NULL,
    @MediaTypeId   int,
    @OmdbEntryId   varchar(32) = NULL,
    @Name          nvarchar(512) = NULL,
    @ReleaseYear   smallint = NULL,
    @LastSeenRunId bigint = NULL,
    @IsActive      bit = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @PersistedId bigint;

        IF @Id IS NOT NULL
        BEGIN
            UPDATE [dbo].[MediaTitles]
            SET [MediaTypeId] = @MediaTypeId,
                [OmdbEntryId] = @OmdbEntryId,
                [Name] = @Name,
                [ReleaseYear] = @ReleaseYear,
                [LastSeenRunId] = @LastSeenRunId,
                [IsActive] = @IsActive,
                [LastModified] = CONVERT(datetime2(3), SYSUTCDATETIME())
            WHERE [Id] = @Id;

            IF @@ROWCOUNT = 0
                THROW 50001, 'Media title was not found.', 1;

            SET @PersistedId = @Id;
        END
        ELSE
        BEGIN
            IF @OmdbEntryId IS NOT NULL
            BEGIN
                SELECT @PersistedId = [Id]
                FROM [dbo].[MediaTitles] WITH (UPDLOCK, HOLDLOCK)
                WHERE [MediaTypeId] = @MediaTypeId
                  AND ISNULL([OmdbEntryId], '') = @OmdbEntryId
                  AND [OmdbEntryId] IS NOT NULL;
            END;

            IF @PersistedId IS NULL
            BEGIN
                INSERT INTO [dbo].[MediaTitles]
                (
                    [MediaTypeId],
                    [OmdbEntryId],
                    [Name],
                    [ReleaseYear],
                    [LastSeenRunId],
                    [IsActive]
                )
                VALUES
                (
                    @MediaTypeId,
                    @OmdbEntryId,
                    @Name,
                    @ReleaseYear,
                    @LastSeenRunId,
                    @IsActive
                );

                SET @PersistedId = CONVERT(bigint, SCOPE_IDENTITY());
            END
            ELSE
            BEGIN
                UPDATE [dbo].[MediaTitles]
                SET [Name] = @Name,
                    [ReleaseYear] = @ReleaseYear,
                    [LastSeenRunId] = @LastSeenRunId,
                    [IsActive] = @IsActive,
                    [LastModified] = CONVERT(datetime2(3), SYSUTCDATETIME())
                WHERE [Id] = @PersistedId;
            END;
        END;

        COMMIT TRANSACTION;

        SELECT [Id], [MediaTypeId], [OmdbEntryId], [Name], [ReleaseYear],
               [DateCreated], [LastModified], [LastSeenRunId], [IsActive]
        FROM [dbo].[MediaTitles]
        WHERE [Id] = @PersistedId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH;
END;
