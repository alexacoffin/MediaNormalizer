CREATE PROCEDURE [dbo].[NormalizationDeletedDirectories_Delete]
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
            [MediaTypeId]        int,
            [Path]               nvarchar(2048),
            [DeletedAtUtc]       datetime2(3)
        );

        INSERT INTO @Deleted
        SELECT [Id], [NormalizationRunId], [MediaTypeId], [Path], [DeletedAtUtc]
        FROM [dbo].[NormalizationDeletedDirectories]
        WHERE [Id] = @Id;

        IF @@ROWCOUNT = 0
            THROW 50001, 'Deleted directory record was not found.', 1;

        DELETE FROM [dbo].[NormalizationDeletedDirectories]
        WHERE [Id] = @Id;

        COMMIT TRANSACTION;

        SELECT [Id], [NormalizationRunId], [MediaTypeId], [Path], [DeletedAtUtc]
        FROM @Deleted;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH;
END;
