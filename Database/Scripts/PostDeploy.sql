IF NOT EXISTS
(
    SELECT 1
    FROM [ctlg].[MediaType]
    WHERE [Id] = 1
)
BEGIN
    INSERT INTO [ctlg].[MediaType] ([Id], [Name])
    VALUES (1, 'TV');
END;

IF NOT EXISTS
(
    SELECT 1
    FROM [ctlg].[MediaType]
    WHERE [Id] = 2
)
BEGIN
    INSERT INTO [ctlg].[MediaType] ([Id], [Name])
    VALUES (2, 'Movies');
END;
