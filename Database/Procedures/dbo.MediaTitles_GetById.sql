CREATE PROCEDURE [dbo].[MediaTitles_GetById]
    @Id             bigint,
    @IncludeInactive bit = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [Id], [MediaTypeId], [OmdbEntryId], [Name], [ReleaseYear],
           [DateCreated], [LastModified], [LastSeenRunId], [IsActive]
    FROM [dbo].[MediaTitles]
    WHERE [Id] = @Id
      AND (@IncludeInactive = 1 OR [IsActive] = 1);
END;
