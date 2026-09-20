CREATE PROCEDURE [dbo].[MediaTitles_GetByOmdbEntryId]
    @MediaTypeId    int,
    @OmdbEntryId    varchar(32),
    @IncludeInactive bit = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [Id], [MediaTypeId], [OmdbEntryId], [Name], [ReleaseYear],
           [DateCreated], [LastModified], [LastSeenRunId], [IsActive]
    FROM [dbo].[MediaTitles]
    WHERE [MediaTypeId] = @MediaTypeId
      AND ISNULL([OmdbEntryId], '') = @OmdbEntryId
      AND [OmdbEntryId] IS NOT NULL
      AND (@IncludeInactive = 1 OR [IsActive] = 1);
END;
