CREATE TABLE [dbo].[NormalizationDeletedDirectories]
(
    [Id]                 bigint IDENTITY(1, 1) NOT NULL,
    [NormalizationRunId] bigint NOT NULL,
    [MediaTypeId]        int NOT NULL,
    [Path]               nvarchar(2048) NOT NULL,
    [DeletedAtUtc]       datetime2(3) NOT NULL
        CONSTRAINT [DF_NormalizationDeletedDirectories_DeletedAtUtc]
        DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT [PK_NormalizationDeletedDirectories]
        PRIMARY KEY ([Id]),
    CONSTRAINT [FK_NormalizationDeletedDirectories_Run]
        FOREIGN KEY ([NormalizationRunId])
        REFERENCES [dbo].[NormalizationRuns] ([Id]),
    CONSTRAINT [FK_NormalizationDeletedDirectories_MediaType]
        FOREIGN KEY ([MediaTypeId])
        REFERENCES [ctlg].[MediaType] ([Id]),
    CONSTRAINT [UQ_NormalizationDeletedDirectories_Run_Path]
        UNIQUE ([NormalizationRunId], [Path])
);
