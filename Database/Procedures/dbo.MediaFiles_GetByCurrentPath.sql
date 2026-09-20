CREATE PROCEDURE [dbo].[MediaFiles_GetByCurrentPath]
    @CurrentPath    nvarchar(2048),
    @IncludeInactive bit = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [Id], [TitleId], [MediaTypeId], [CurrentPath], [CanonicalPath],
           [SeasonNumber], [EpisodeNumber], [AirDate], [EpisodeTitle],
           [LastStatus], [LastMessage], [FirstSeenRunId], [LastSeenRunId],
           [DateCreated], [LastModified], [IsActive]
    FROM [dbo].[MediaFiles]
    WHERE [CurrentPath] = @CurrentPath
      AND (@IncludeInactive = 1 OR [IsActive] = 1);
END;
