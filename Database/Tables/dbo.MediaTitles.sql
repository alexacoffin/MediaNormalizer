CREATE TABLE [dbo].[MediaTitles]
(
    [Id]            bigint IDENTITY(1, 1) NOT NULL,
    [MediaTypeId]   int NOT NULL,
    [OmdbEntryId]   varchar(32) NULL,
    [Name]          nvarchar(512) NULL,
    [ReleaseYear]   smallint NULL,
    [DateCreated]   datetime2(3) NOT NULL
        CONSTRAINT [DF_MediaTitles_DateCreated]
        DEFAULT (SYSUTCDATETIME()),
    [LastModified]  datetime2(3) NOT NULL
        CONSTRAINT [DF_MediaTitles_LastModified]
        DEFAULT (SYSUTCDATETIME()),
    [LastSeenRunId] bigint NULL,
    [IsActive]      bit NOT NULL
        CONSTRAINT [DF_MediaTitles_IsActive]
        DEFAULT (1),

    CONSTRAINT [PK_MediaTitles]
        PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MediaTitles_MediaType]
        FOREIGN KEY ([MediaTypeId])
        REFERENCES [ctlg].[MediaType] ([Id]),
    CONSTRAINT [FK_MediaTitles_LastSeenRun]
        FOREIGN KEY ([LastSeenRunId])
        REFERENCES [dbo].[NormalizationRuns] ([Id])
);
