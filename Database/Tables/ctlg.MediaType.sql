CREATE TABLE [ctlg].[MediaType]
(
    [Id]   int          NOT NULL,
    [Name] varchar(255) NOT NULL,

    CONSTRAINT [PK_MediaType]
        PRIMARY KEY ([Id]),
    CONSTRAINT [UQ_MediaType_Name]
        UNIQUE ([Name])
);
