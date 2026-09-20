CREATE TABLE [dbo].[NormalizationFileResults]
(
    [Id]                 bigint IDENTITY(1, 1) NOT NULL,
    [NormalizationRunId] bigint NOT NULL,
    [MediaFileId]        bigint NULL,
    [MediaTitleId]       bigint NULL,
    [MediaTypeId]        int NOT NULL,
    [SourcePath]         nvarchar(2048) NOT NULL,
    [DestinationPath]    nvarchar(2048) NULL,
    [Status]             varchar(32) NOT NULL,
    [Message]            nvarchar(4000) NOT NULL,
    [SourceRole]         varchar(20) NOT NULL,
    [CreatedAtUtc]       datetime2(3) NOT NULL
        CONSTRAINT [DF_NormalizationFileResults_CreatedAtUtc]
        DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT [PK_NormalizationFileResults]
        PRIMARY KEY ([Id]),
    CONSTRAINT [FK_NormalizationFileResults_Run]
        FOREIGN KEY ([NormalizationRunId])
        REFERENCES [dbo].[NormalizationRuns] ([Id]),
    CONSTRAINT [FK_NormalizationFileResults_File]
        FOREIGN KEY ([MediaFileId])
        REFERENCES [dbo].[MediaFiles] ([Id]),
    CONSTRAINT [FK_NormalizationFileResults_Title]
        FOREIGN KEY ([MediaTitleId])
        REFERENCES [dbo].[MediaTitles] ([Id]),
    CONSTRAINT [FK_NormalizationFileResults_MediaType]
        FOREIGN KEY ([MediaTypeId])
        REFERENCES [ctlg].[MediaType] ([Id]),
    CONSTRAINT [CK_NormalizationFileResults_Status]
        CHECK ([Status] IN ('Renamed', 'AlreadyNormalized', 'Skipped', 'Failed')),
    CONSTRAINT [CK_NormalizationFileResults_SourceRole]
        CHECK ([SourceRole] IN ('Intake', 'Destination'))
);
