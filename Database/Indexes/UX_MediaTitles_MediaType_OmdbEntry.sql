CREATE UNIQUE INDEX [UX_MediaTitles_MediaType_OmdbEntry]
    ON [dbo].[MediaTitles] ([MediaTypeId], [OmdbEntryId])
    WHERE [OmdbEntryId] IS NOT NULL;
