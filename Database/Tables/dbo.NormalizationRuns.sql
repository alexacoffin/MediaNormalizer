CREATE TABLE [dbo].[NormalizationRuns]
(
    [Id]             bigint IDENTITY(1, 1) NOT NULL,
    [StartedAtUtc]   datetime2(3) NOT NULL
        CONSTRAINT [DF_NormalizationRuns_StartedAtUtc]
        DEFAULT (SYSUTCDATETIME()),
    [CompletedAtUtc] datetime2(3) NULL,
    [Status]         varchar(20) NOT NULL,
    [ErrorMessage]   nvarchar(4000) NULL,

    CONSTRAINT [PK_NormalizationRuns]
        PRIMARY KEY ([Id]),
    CONSTRAINT [CK_NormalizationRuns_Status]
        CHECK ([Status] IN ('Running', 'Completed', 'Failed', 'Canceled'))
);
