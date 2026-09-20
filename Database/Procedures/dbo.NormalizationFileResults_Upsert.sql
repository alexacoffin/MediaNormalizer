CREATE PROCEDURE [dbo].[NormalizationFileResults_Upsert]
    @Id                 bigint = NULL,
    @NormalizationRunId bigint,
    @MediaFileId        bigint = NULL,
    @MediaTitleId       bigint = NULL,
    @MediaTypeId        int,
    @SourcePath         nvarchar(2048),
    @DestinationPath    nvarchar(2048) = NULL,
    @Status             varchar(32),
    @Message            nvarchar(4000),
    @SourceRole         varchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @PersistedId bigint;

        IF @Id IS NULL
        BEGIN
            INSERT INTO [dbo].[NormalizationFileResults]
            (
                [NormalizationRunId], [MediaFileId], [MediaTitleId], [MediaTypeId],
                [SourcePath], [DestinationPath], [Status], [Message], [SourceRole]
            )
            VALUES
            (
                @NormalizationRunId, @MediaFileId, @MediaTitleId, @MediaTypeId,
                @SourcePath, @DestinationPath, @Status, @Message, @SourceRole
            );

            SET @PersistedId = CONVERT(bigint, SCOPE_IDENTITY());
        END
        ELSE
        BEGIN
            UPDATE [dbo].[NormalizationFileResults]
            SET [NormalizationRunId] = @NormalizationRunId,
                [MediaFileId] = @MediaFileId,
                [MediaTitleId] = @MediaTitleId,
                [MediaTypeId] = @MediaTypeId,
                [SourcePath] = @SourcePath,
                [DestinationPath] = @DestinationPath,
                [Status] = @Status,
                [Message] = @Message,
                [SourceRole] = @SourceRole
            WHERE [Id] = @Id;

            IF @@ROWCOUNT = 0
                THROW 50001, 'Normalization file result was not found.', 1;

            SET @PersistedId = @Id;
        END;

        COMMIT TRANSACTION;

        SELECT [Id], [NormalizationRunId], [MediaFileId], [MediaTitleId], [MediaTypeId],
               [SourcePath], [DestinationPath], [Status], [Message], [SourceRole], [CreatedAtUtc]
        FROM [dbo].[NormalizationFileResults]
        WHERE [Id] = @PersistedId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH;
END;
