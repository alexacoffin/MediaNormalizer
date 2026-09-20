CREATE PROCEDURE [dbo].[NormalizationDeletedDirectories_Upsert]
    @Id                 bigint = NULL,
    @NormalizationRunId bigint,
    @MediaTypeId        int,
    @Path               nvarchar(2048)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @PersistedId bigint;

        IF @Id IS NOT NULL
        BEGIN
            UPDATE [dbo].[NormalizationDeletedDirectories]
            SET [NormalizationRunId] = @NormalizationRunId,
                [MediaTypeId] = @MediaTypeId,
                [Path] = @Path
            WHERE [Id] = @Id;

            IF @@ROWCOUNT = 0
                THROW 50001, 'Deleted directory record was not found.', 1;

            SET @PersistedId = @Id;
        END
        ELSE
        BEGIN
            SELECT @PersistedId = [Id]
            FROM [dbo].[NormalizationDeletedDirectories] WITH (UPDLOCK, HOLDLOCK)
            WHERE [NormalizationRunId] = @NormalizationRunId
              AND [Path] = @Path;

            IF @PersistedId IS NULL
            BEGIN
                INSERT INTO [dbo].[NormalizationDeletedDirectories]
                (
                    [NormalizationRunId], [MediaTypeId], [Path]
                )
                VALUES
                (
                    @NormalizationRunId, @MediaTypeId, @Path
                );

                SET @PersistedId = CONVERT(bigint, SCOPE_IDENTITY());
            END
            ELSE
            BEGIN
                UPDATE [dbo].[NormalizationDeletedDirectories]
                SET [MediaTypeId] = @MediaTypeId
                WHERE [Id] = @PersistedId;
            END;
        END;

        COMMIT TRANSACTION;

        SELECT [Id], [NormalizationRunId], [MediaTypeId], [Path], [DeletedAtUtc]
        FROM [dbo].[NormalizationDeletedDirectories]
        WHERE [Id] = @PersistedId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH;
END;
