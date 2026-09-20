CREATE INDEX [IX_MediaTitles_Active]
    ON [dbo].[MediaTitles] ([MediaTypeId], [IsActive], [Name]);
