IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[UserGroups]') AND name = 'CanRead'
)
BEGIN
    ALTER TABLE [dbo].[UserGroups] ADD
        [CanRead] INT NOT NULL DEFAULT 0,
        [CanWrite] INT NOT NULL DEFAULT 0,
        [CanDelete] INT NOT NULL DEFAULT 0;
END
GO
