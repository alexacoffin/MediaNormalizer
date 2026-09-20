CREATE TABLE [dbo].[MediaFiles]
(
    [Id]              bigint IDENTITY(1, 1) NOT NULL,
    [TitleId]         bigint NULL,
    [MediaTypeId]     int NOT NULL,
    [CurrentPath]     nvarchar(2048) NOT NULL,
    [CanonicalPath]   nvarchar(2048) NULL,
    [SeasonNumber]    int NULL,
    [EpisodeNumber]   int NULL,
    [AirDate]         date NULL,
    [EpisodeTitle]    nvarchar(512) NULL,
    [LastStatus]      varchar(32) NULL,
    [LastMessage]     nvarchar(4000) NULL,
    [FirstSeenRunId]  bigint NULL,
    [LastSeenRunId]   bigint NULL,
    [DateCreated]     datetime2(3) NOT NULL
        CONSTRAINT [DF_MediaFiles_DateCreated]
        DEFAULT (SYSUTCDATETIME()),
    [LastModified]    datetime2(3) NOT NULL
        CONSTRAINT [DF_MediaFiles_LastModified]
        DEFAULT (SYSUTCDATETIME()),
    [IsActive]        bit NOT NULL
        CONSTRAINT [DF_MediaFiles_IsActive]
        DEFAULT (1),

    CONSTRAINT [PK_MediaFiles]
        PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MediaFiles_Title]
        FOREIGN KEY ([TitleId])
        REFERENCES [dbo].[MediaTitles] ([Id]),
    CONSTRAINT [FK_MediaFiles_MediaType]
        FOREIGN KEY ([MediaTypeId])
        REFERENCES [ctlg].[MediaType] ([Id]),
    CONSTRAINT [FK_MediaFiles_FirstSeenRun]
        FOREIGN KEY ([FirstSeenRunId])
        REFERENCES [dbo].[NormalizationRuns] ([Id]),
    CONSTRAINT [FK_MediaFiles_LastSeenRun]
        FOREIGN KEY ([LastSeenRunId])
        REFERENCES [dbo].[NormalizationRuns] ([Id]),
    CONSTRAINT [CK_MediaFiles_LastStatus]
        CHECK
        (
            [LastStatus] IS NULL
            OR [LastStatus] IN ('Renamed', 'AlreadyNormalized', 'Skipped', 'Failed')
        )
);
