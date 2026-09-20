CREATE PROCEDURE [dbo].[NormalizationRuns_Upsert]
    @Id             bigint = NULL,
    @CompletedAtUtc datetime2(3) = NULL,
    @Status         varchar(20),
    @ErrorMessage   nvarchar(4000) = NULL,
    @StartedAtUtc   datetime2(3) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @PersistedId bigint;

        IF @Id IS NULL
        BEGIN
            INSERT INTO [dbo].[NormalizationRuns]
            (
                [StartedAtUtc],
                [CompletedAtUtc],
                [Status],
                [ErrorMessage]
            )
            VALUES
            (
                COALESCE(@StartedAtUtc, SYSUTCDATETIME()),
                @CompletedAtUtc,
                @Status,
                @ErrorMessage
            );

            SET @PersistedId = CONVERT(bigint, SCOPE_IDENTITY());
        END
        ELSE
        BEGIN
            UPDATE [dbo].[NormalizationRuns]
            SET [CompletedAtUtc] = @CompletedAtUtc,
                [Status] = @Status,
                [ErrorMessage] = @ErrorMessage
            WHERE [Id] = @Id;

            IF @@ROWCOUNT = 0
                THROW 50001, 'Normalization run was not found.', 1;

            SET @PersistedId = @Id;
        END;

        COMMIT TRANSACTION;

        SELECT [Id], [StartedAtUtc], [CompletedAtUtc], [Status], [ErrorMessage]
        FROM [dbo].[NormalizationRuns]
        WHERE [Id] = @PersistedId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH;
END;
