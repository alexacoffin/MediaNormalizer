CREATE PROCEDURE [dbo].[MediaTitles_Delete]
    @Id bigint
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE [dbo].[MediaTitles]
        SET [IsActive] = 0,
            [LastModified] = CONVERT(datetime2(3), SYSUTCDATETIME())
        WHERE [Id] = @Id;

        IF @@ROWCOUNT = 0
            THROW 50001, 'Media title was not found.', 1;

        COMMIT TRANSACTION;

        SELECT [Id], [MediaTypeId], [OmdbEntryId], [Name], [ReleaseYear],
               [DateCreated], [LastModified], [LastSeenRunId], [IsActive]
        FROM [dbo].[MediaTitles]
        WHERE [Id] = @Id;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH;
END;
