CREATE PROCEDURE [dbo].[MediaFiles_GetById]
    @Id             bigint,
    @IncludeInactive bit = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [Id], [TitleId], [MediaTypeId], [CurrentPath], [CanonicalPath],
           [SeasonNumber], [EpisodeNumber], [AirDate], [EpisodeTitle],
           [LastStatus], [LastMessage], [FirstSeenRunId], [LastSeenRunId],
           [DateCreated], [LastModified], [IsActive]
    FROM [dbo].[MediaFiles]
    WHERE [Id] = @Id
      AND (@IncludeInactive = 1 OR [IsActive] = 1);
END;
