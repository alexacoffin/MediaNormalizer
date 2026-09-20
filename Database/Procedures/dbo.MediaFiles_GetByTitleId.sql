CREATE PROCEDURE [dbo].[MediaFiles_GetByTitleId]
    @TitleId        bigint,
    @IncludeInactive bit = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [Id], [TitleId], [MediaTypeId], [CurrentPath], [CanonicalPath],
           [SeasonNumber], [EpisodeNumber], [AirDate], [EpisodeTitle],
           [LastStatus], [LastMessage], [FirstSeenRunId], [LastSeenRunId],
           [DateCreated], [LastModified], [IsActive]
    FROM [dbo].[MediaFiles]
    WHERE ISNULL([TitleId], CONVERT(bigint, 0)) = @TitleId
      AND [TitleId] IS NOT NULL
      AND (@IncludeInactive = 1 OR [IsActive] = 1)
    ORDER BY [CurrentPath];
END;
