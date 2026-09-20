CREATE PROCEDURE [dbo].[MediaTitles_GetActiveByMediaType]
    @MediaTypeId int
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [Id], [MediaTypeId], [OmdbEntryId], [Name], [ReleaseYear],
           [DateCreated], [LastModified], [LastSeenRunId], [IsActive]
    FROM [dbo].[MediaTitles]
    WHERE [MediaTypeId] = @MediaTypeId
      AND [IsActive] = 1
    ORDER BY [Id];
END;
