CREATE PROCEDURE [dbo].[MediaFiles_Delete]
    @Id bigint
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE [dbo].[MediaFiles]
        SET [IsActive] = 0,
            [LastModified] = CONVERT(datetime2(3), SYSUTCDATETIME())
        WHERE [Id] = @Id;

        IF @@ROWCOUNT = 0
            THROW 50001, 'Media file was not found.', 1;

        COMMIT TRANSACTION;

        SELECT [Id], [TitleId], [MediaTypeId], [CurrentPath], [CanonicalPath],
               [SeasonNumber], [EpisodeNumber], [AirDate], [EpisodeTitle],
               [LastStatus], [LastMessage], [FirstSeenRunId], [LastSeenRunId],
               [DateCreated], [LastModified], [IsActive]
        FROM [dbo].[MediaFiles]
        WHERE [Id] = @Id;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH;
END;
