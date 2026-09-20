CREATE PROCEDURE [dbo].[NormalizationRuns_Delete]
    @Id bigint
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Deleted TABLE
        (
            [Id]             bigint,
            [StartedAtUtc]   datetime2(3),
            [CompletedAtUtc] datetime2(3),
            [Status]         varchar(20),
            [ErrorMessage]   nvarchar(4000)
        );

        INSERT INTO @Deleted
        SELECT [Id], [StartedAtUtc], [CompletedAtUtc], [Status], [ErrorMessage]
        FROM [dbo].[NormalizationRuns]
        WHERE [Id] = @Id;

        IF @@ROWCOUNT = 0
            THROW 50001, 'Normalization run was not found.', 1;

        DELETE FROM [dbo].[NormalizationRuns]
        WHERE [Id] = @Id;

        COMMIT TRANSACTION;

        SELECT [Id], [StartedAtUtc], [CompletedAtUtc], [Status], [ErrorMessage]
        FROM @Deleted;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH;
END;
