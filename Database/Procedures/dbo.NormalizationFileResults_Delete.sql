CREATE PROCEDURE [dbo].[NormalizationFileResults_Delete]
    @Id bigint
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Deleted TABLE
        (
            [Id]                 bigint,
            [NormalizationRunId] bigint,
            [MediaFileId]        bigint,
            [MediaTitleId]       bigint,
            [MediaTypeId]        int,
            [SourcePath]         nvarchar(2048),
            [DestinationPath]    nvarchar(2048),
            [Status]             varchar(32),
            [Message]            nvarchar(4000),
            [SourceRole]         varchar(20),
            [CreatedAtUtc]       datetime2(3)
        );

        INSERT INTO @Deleted
        SELECT [Id], [NormalizationRunId], [MediaFileId], [MediaTitleId], [MediaTypeId],
               [SourcePath], [DestinationPath], [Status], [Message], [SourceRole], [CreatedAtUtc]
        FROM [dbo].[NormalizationFileResults]
        WHERE [Id] = @Id;

        IF @@ROWCOUNT = 0
            THROW 50001, 'Normalization file result was not found.', 1;

        DELETE FROM [dbo].[NormalizationFileResults]
        WHERE [Id] = @Id;

        COMMIT TRANSACTION;

        SELECT [Id], [NormalizationRunId], [MediaFileId], [MediaTitleId], [MediaTypeId],
               [SourcePath], [DestinationPath], [Status], [Message], [SourceRole], [CreatedAtUtc]
        FROM @Deleted;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH;
END;
