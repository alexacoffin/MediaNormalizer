CREATE PROCEDURE [dbo].[MediaFiles_GetActiveByMediaType]
    @MediaTypeId int
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [Id], [TitleId], [MediaTypeId], [CurrentPath], [CanonicalPath],
           [SeasonNumber], [EpisodeNumber], [AirDate], [EpisodeTitle],
           [LastStatus], [LastMessage], [FirstSeenRunId], [LastSeenRunId],
           [DateCreated], [LastModified], [IsActive]
    FROM [dbo].[MediaFiles]
    WHERE [MediaTypeId] = @MediaTypeId
      AND [IsActive] = 1
    ORDER BY [CurrentPath];
END;
