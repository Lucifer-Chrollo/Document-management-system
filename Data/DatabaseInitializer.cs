using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using DocumentManagementSystem.Services;

namespace DocumentManagementSystem.Data;

public class DatabaseInitializer
{
    private readonly IConfiguration _configuration;
    private readonly Microsoft.Extensions.Logging.ILogger<DatabaseInitializer> _logger;
    private readonly IEncryptionService _encryptionService;

    public DatabaseInitializer(IConfiguration configuration, Microsoft.Extensions.Logging.ILogger<DatabaseInitializer> logger, IEncryptionService encryptionService)
    {
        _configuration = configuration;
        _logger = logger;
        _encryptionService = encryptionService;
    }

    public async Task EnsureSchemaAsync()
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString)) return;

            var builder = new SqlConnectionStringBuilder(connectionString);
            var databaseName = builder.InitialCatalog;
            
            // Connect to master to create the database if missing
            builder.InitialCatalog = "master";
            using (var masterConnection = new SqlConnection(builder.ConnectionString))
            {
                await masterConnection.OpenAsync();
                using var checkCmd = new SqlCommand($"IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '{databaseName}') CREATE DATABASE [{databaseName}]", masterConnection);
                await checkCmd.ExecuteNonQueryAsync();
            }

            // Now connect to the actual database
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            
            // Prepare Secure Admin Data
            var adminEmail = "admin@dms.local";
            var normalizedAdminEmail = "ADMIN@DMS.LOCAL"; // Used to be plain text
            
            // Calculate Blind Index values
            var encryptedEmail = await _encryptionService.EncryptTextAsync(adminEmail);
            var hashedNormalizedEmail = _encryptionService.Hash(normalizedAdminEmail);

            command.CommandText = $@"
                -- Create Departments Table
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Departments' AND xtype='U')
                BEGIN
                    CREATE TABLE Departments (
                        DepartmentId INT PRIMARY KEY,
                        DepartmentName NVARCHAR(100) NOT NULL,
                        SortOrder INT NOT NULL DEFAULT 0
                    );
                    
                    INSERT INTO Departments (DepartmentId, DepartmentName) VALUES 
                    (1, 'Sales'), (2, 'HR'), (3, 'IT'), (4, 'Finance');
                END

                -- Create Categories Table
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Categories' AND xtype='U')
                BEGIN
                    CREATE TABLE Categories (
                        CategoryId INT PRIMARY KEY,
                        CategoryName NVARCHAR(200) NOT NULL,
                        CategoryOrder INT NOT NULL DEFAULT 0,
                        CreatedDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        UpdatedDate DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    );

                    INSERT INTO Categories (CategoryId, CategoryName) VALUES 
                    (1, 'Invoices'), (2, 'Contracts'), (3, 'Reports');
                END

                -- Create Locations Table
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Locations' AND xtype='U')
                BEGIN
                    CREATE TABLE Locations (
                        LocationId INT PRIMARY KEY,
                        LocationName NVARCHAR(200) NOT NULL,
                        SortOrder INT NOT NULL DEFAULT 0
                    );

                    INSERT INTO Locations (LocationId, LocationName) VALUES 
                    (1, 'Main Office'), (2, 'Branch A');
                END

                -- Create Documents Table
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Documents' AND xtype='U')
                BEGIN
                    CREATE TABLE Documents (
                        DocumentId INT IDENTITY(1,1) PRIMARY KEY,
                        DocumentName NVARCHAR(500) NOT NULL,
                        FileType NVARCHAR(200),
                        CategoryID INT NOT NULL,
                        UploadedBy INT NOT NULL DEFAULT 0,
                        Path NVARCHAR(1000) NOT NULL,
                        SourcePath NVARCHAR(1000),
                        Extension NVARCHAR(20),
                        Password NVARCHAR(256),
                        Status NVARCHAR(50) DEFAULT 'Active',
                        ParentID INT NOT NULL DEFAULT 0,
                        UploadedDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        UpdatedDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        FileData VARBINARY(MAX),
                        MyProperty INT NOT NULL DEFAULT 0,
                        DepartmentID INT NOT NULL DEFAULT 0,
                        LocationID INT NOT NULL DEFAULT 0,
                        UpdatedBy INT NOT NULL DEFAULT 0,
                        FileSize BIGINT NOT NULL DEFAULT 0,
                        FileHash NVARCHAR(MAX),
                        IsDeleted BIT NOT NULL DEFAULT 0,
                        IsOcrProcessed BIT NOT NULL DEFAULT 0,
                        OcrText NVARCHAR(MAX) NULL,
                        OcrConfidence DECIMAL(3,2) NULL,
                        OcrEngine NVARCHAR(50) NULL,
                        OcrProcessedDate DATETIME2 NULL,
                        HasExtractedText BIT NOT NULL DEFAULT 0,
                        CompressionAlgorithm NVARCHAR(50) NULL,
                        CompressedSize BIGINT NULL,
                        BatchLabel NVARCHAR(200) NULL
                    );
                END
                
                -- Create UserDocumentRights Table
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='UserDocumentRights' AND xtype='U')
                BEGIN
                    CREATE TABLE UserDocumentRights (
                        RightId INT IDENTITY(1,1) PRIMARY KEY,
                        UserId INT NOT NULL,
                        DocumentId INT NOT NULL,
                        Rights INT NOT NULL
                    );
                END

                -- Create Users Table (Custom Identity)
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
                BEGIN
                    CREATE TABLE Users (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        UserName NVARCHAR(256) NOT NULL,
                        NormalizedUserName NVARCHAR(256) NOT NULL,
                        Email NVARCHAR(256),
                        NormalizedEmail NVARCHAR(256),
                        EmailConfirmed BIT NOT NULL DEFAULT 0,
                        PasswordHash NVARCHAR(MAX),
                        SecurityStamp NVARCHAR(MAX),
                        ConcurrencyStamp NVARCHAR(MAX),
                        PhoneNumber NVARCHAR(MAX),
                        PhoneNumberConfirmed BIT NOT NULL DEFAULT 0,
                        TwoFactorEnabled BIT NOT NULL DEFAULT 0,
                        LockoutEnd DATETIMEOFFSET,
                        LockoutEnabled BIT NOT NULL DEFAULT 0,
                        AccessFailedCount INT NOT NULL DEFAULT 0,
                        FirstName NVARCHAR(MAX),
                        LastName NVARCHAR(MAX),
                        Department NVARCHAR(MAX),
                        CreatedDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        LastLoginDate DATETIME2,
                        StorageQuotaBytes BIGINT NOT NULL DEFAULT 1073741824,
                        StorageUsedBytes BIGINT NOT NULL DEFAULT 0,
                        IsActive BIT NOT NULL DEFAULT 1
                    );
                END
                -- Users table already handled in EnsureSchemaAsync logic below
            ";
            command.ExecuteNonQuery();

            // Handle Admin User with Parameters and Programmatic Hash
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<object>();
            var passwordHash = hasher.HashPassword(new object(), "Admin@123");

            var adminQuery = @"
                IF NOT EXISTS (SELECT * FROM Users WHERE UserName = 'admin')
                BEGIN
                    INSERT INTO Users (UserName, NormalizedUserName, Email, NormalizedEmail, PasswordHash, IsActive, CreatedDate, SecurityStamp)
                    VALUES ('admin', 'ADMIN', @Email, @NormalizedEmail, @PasswordHash, 1, GETUTCDATE(), NEWID());
                END
                ELSE
                BEGIN
                    -- Repair corrupted hash or migrate plain email
                    UPDATE Users 
                    SET Email = @Email, 
                        NormalizedEmail = @NormalizedEmail,
                        PasswordHash = CASE WHEN PasswordHash LIKE '%/%/%/%/%' THEN @PasswordHash ELSE PasswordHash END
                    WHERE UserName = 'admin' AND (Email = 'admin@dms.local' OR PasswordHash LIKE '%/%/%/%/%');
                END";

            using var adminCmd = new SqlCommand(adminQuery, connection);
            adminCmd.Parameters.Add("@Email", SqlDbType.NVarChar).Value = encryptedEmail;
            adminCmd.Parameters.Add("@NormalizedEmail", SqlDbType.NVarChar).Value = hashedNormalizedEmail;
            adminCmd.Parameters.Add("@PasswordHash", SqlDbType.NVarChar).Value = passwordHash;
            await adminCmd.ExecuteNonQueryAsync();

            command.CommandText = @"
                -- Migrate existing Documents table if needed (adding OCR columns)
                IF EXISTS (SELECT * FROM sysobjects WHERE name='Documents' AND xtype='U')
                BEGIN
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'IsOcrProcessed')
                        ALTER TABLE Documents ADD IsOcrProcessed BIT NOT NULL DEFAULT 0;
                    
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'OcrText')
                        ALTER TABLE Documents ADD OcrText NVARCHAR(MAX) NULL;
                    
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'OcrConfidence')
                        ALTER TABLE Documents ADD OcrConfidence DECIMAL(18,2) NULL;
                    
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'OcrEngine')
                        ALTER TABLE Documents ADD OcrEngine NVARCHAR(50) NULL;
                    
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'OcrProcessedDate')
                        ALTER TABLE Documents ADD OcrProcessedDate DATETIME2 NULL;
                    
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'HasExtractedText')
                        ALTER TABLE Documents ADD HasExtractedText BIT NOT NULL DEFAULT 0;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'LocationID')
                        ALTER TABLE Documents ADD LocationID INT NOT NULL DEFAULT 0;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'CompressionAlgorithm')
                        ALTER TABLE Documents ADD CompressionAlgorithm NVARCHAR(50) NULL;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'CompressedSize')
                        ALTER TABLE Documents ADD CompressedSize BIGINT NULL;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'BatchLabel')
                        ALTER TABLE Documents ADD BatchLabel NVARCHAR(200) NULL;

                    -- Fix truncation issues for long MIME types
                    ALTER TABLE Documents ALTER COLUMN FileType NVARCHAR(200);

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'CurrentVersion')
                        ALTER TABLE Documents ADD CurrentVersion INT NOT NULL DEFAULT 1;
                END

                -- Add MFA and admin-viewable password columns to Users table
                IF EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
                BEGIN
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'MfaSecret')
                        ALTER TABLE Users ADD MfaSecret NVARCHAR(100) NULL;
                    
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'MfaRecoveryCode')
                        ALTER TABLE Users ADD MfaRecoveryCode NVARCHAR(100) NULL;
                        
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'MfaSetupDate')
                        ALTER TABLE Users ADD MfaSetupDate DATETIME2 NULL;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'EncryptedPassword')
                        ALTER TABLE Users ADD EncryptedPassword NVARCHAR(MAX) NULL;
                END

                -- Create DocumentVersions Table
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='DocumentVersions' AND xtype='U')
                BEGIN
                    CREATE TABLE DocumentVersions (
                        VersionId INT IDENTITY(1,1) PRIMARY KEY,
                        DocumentId INT NOT NULL,
                        VersionNumber INT NOT NULL,
                        FilePath NVARCHAR(1000) NOT NULL,
                        FileName NVARCHAR(500) NOT NULL,
                        FileSize BIGINT NOT NULL DEFAULT 0,
                        CreatedDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        CreatedBy INT NOT NULL DEFAULT 0,
                        CONSTRAINT FK_DocumentVersions_Documents FOREIGN KEY (DocumentId) REFERENCES Documents(DocumentId)
                    );
                END

                -- Create AuditLogs Table
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AuditLogs' AND xtype='U')
                BEGIN
                    CREATE TABLE AuditLogs (
                        AuditId INT IDENTITY(1,1) PRIMARY KEY,
                        Action NVARCHAR(50) NOT NULL,
                        DocumentId INT NULL,
                        DocumentName NVARCHAR(500) NULL,
                        UserId INT NOT NULL,
                        UserName NVARCHAR(256) NOT NULL,
                        IpAddress NVARCHAR(50) NULL,
                        UserAgent NVARCHAR(500) NULL,
                        Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        Details NVARCHAR(MAX) NULL,
                        Success BIT NOT NULL DEFAULT 1,
                        ErrorMessage NVARCHAR(1000) NULL
                    );
                    
                    -- Create indexes for common queries
                    CREATE INDEX IX_AuditLogs_Timestamp ON AuditLogs(Timestamp DESC);
                    CREATE INDEX IX_AuditLogs_DocumentId ON AuditLogs(DocumentId);
                    CREATE INDEX IX_AuditLogs_UserId ON AuditLogs(UserId);
                    CREATE INDEX IX_AuditLogs_Action ON AuditLogs(Action);
                END

                -- Create UploadSessions Table for QR-based mobile uploads
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='UploadSessions' AND xtype='U')
                BEGIN
                    CREATE TABLE UploadSessions (
                        SessionId INT IDENTITY(1,1) PRIMARY KEY,
                        UserId INT NOT NULL,
                        Token NVARCHAR(100) NOT NULL,
                        PinHash NVARCHAR(100) NOT NULL,
                        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        ExpiresAt DATETIME2 NOT NULL,
                        MaxAttempts INT NOT NULL DEFAULT 3,
                        FailedAttempts INT NOT NULL DEFAULT 0,
                        MaxFiles INT NOT NULL DEFAULT 10,
                        FilesUploaded INT NOT NULL DEFAULT 0,
                        IsActive BIT NOT NULL DEFAULT 1,
                        IsPinVerified BIT NOT NULL DEFAULT 0,
                        CreatorIpAddress NVARCHAR(50) NULL,
                        UploaderIpAddress NVARCHAR(50) NULL,
                        LastAccessedAt DATETIME2 NULL,
                        DefaultCategoryId INT NULL,
                        Label NVARCHAR(200) NULL,
                        CONSTRAINT FK_UploadSessions_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
                    );
                    
                    CREATE INDEX IX_UploadSessions_Token ON UploadSessions(Token);
                    CREATE INDEX IX_UploadSessions_UserId ON UploadSessions(UserId);
                    CREATE INDEX IX_UploadSessions_ExpiresAt ON UploadSessions(ExpiresAt);
                    CREATE INDEX IX_UploadSessions_UserId ON UploadSessions(UserId);
                    CREATE INDEX IX_UploadSessions_ExpiresAt ON UploadSessions(ExpiresAt);
                END

                -- Create UserLogins Table for External Auth (Google)
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='UserLogins' AND xtype='U')
                BEGIN
                    CREATE TABLE UserLogins (
                        LoginProvider NVARCHAR(128) NOT NULL,
                        ProviderKey NVARCHAR(128) NOT NULL,
                        ProviderDisplayName NVARCHAR(MAX) NULL,
                        UserId INT NOT NULL,
                        CONSTRAINT PK_UserLogins PRIMARY KEY (LoginProvider, ProviderKey),
                        CONSTRAINT FK_UserLogins_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
                    );
                    
                    CREATE INDEX IX_UserLogins_UserId ON UserLogins(UserId);
                END
            ";
            command.ExecuteNonQuery();

            // ── Normalization Migration Block ──
            // Idempotent fixes for schema violations discovered during normalization analysis.
            command.CommandText = @"
                -- V4: Drop unused FileData BLOB column (CAS disk storage is used)
                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'FileData')
                    ALTER TABLE Documents DROP COLUMN FileData;

                -- V5: Drop orphan MyProperty column safely
                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Documents') AND name = 'MyProperty')
                BEGIN
                    DECLARE @ConstraintName nvarchar(200);
                    SELECT @ConstraintName = df.name 
                    FROM sys.default_constraints df
                    INNER JOIN sys.columns c ON df.parent_object_id = c.object_id AND df.parent_column_id = c.column_id
                    WHERE c.name = 'MyProperty' AND c.object_id = OBJECT_ID('Documents');
                    
                    IF @ConstraintName IS NOT NULL
                        EXEC('ALTER TABLE Documents DROP CONSTRAINT [' + @ConstraintName + ']');
                        
                    ALTER TABLE Documents DROP COLUMN MyProperty;
                END

                -- V8: Add FK constraints to Documents (safe: only if constraint doesn't exist)
                IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Documents_Categories')
                AND EXISTS (SELECT * FROM sysobjects WHERE name='Categories' AND xtype='U')
                    ALTER TABLE Documents WITH NOCHECK ADD CONSTRAINT FK_Documents_Categories 
                        FOREIGN KEY (CategoryID) REFERENCES Categories(CategoryId);

                IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Documents_Departments')
                AND EXISTS (SELECT * FROM sysobjects WHERE name='Departments' AND xtype='U')
                    ALTER TABLE Documents WITH NOCHECK ADD CONSTRAINT FK_Documents_Departments 
                        FOREIGN KEY (DepartmentID) REFERENCES Departments(DepartmentId);

                IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Documents_Locations')
                AND EXISTS (SELECT * FROM sysobjects WHERE name='Locations' AND xtype='U')
                    ALTER TABLE Documents WITH NOCHECK ADD CONSTRAINT FK_Documents_Locations 
                        FOREIGN KEY (LocationID) REFERENCES Locations(LocationId);

                IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Documents_UploadedBy')
                AND EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
                    ALTER TABLE Documents WITH NOCHECK ADD CONSTRAINT FK_Documents_UploadedBy 
                        FOREIGN KEY (UploadedBy) REFERENCES Users(Id);

                IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Documents_UpdatedBy')
                AND EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
                    ALTER TABLE Documents WITH NOCHECK ADD CONSTRAINT FK_Documents_UpdatedBy 
                        FOREIGN KEY (UpdatedBy) REFERENCES Users(Id);

                -- V9: Add GroupId column to UserDocumentRights
                IF EXISTS (SELECT * FROM sysobjects WHERE name='UserDocumentRights' AND xtype='U')
                BEGIN
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('UserDocumentRights') AND name = 'GroupId')
                        ALTER TABLE UserDocumentRights ADD GroupId INT NULL;

                    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_UDR_UserGroups')
                    AND EXISTS (SELECT * FROM sysobjects WHERE name='UserGroups' AND xtype='U')
                        ALTER TABLE UserDocumentRights WITH NOCHECK ADD CONSTRAINT FK_UDR_UserGroups 
                            FOREIGN KEY (GroupId) REFERENCES UserGroups(GroupId);

                    -- Add FK to Documents
                    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_UDR_Documents')
                        ALTER TABLE UserDocumentRights WITH NOCHECK ADD CONSTRAINT FK_UDR_Documents 
                            FOREIGN KEY (DocumentId) REFERENCES Documents(DocumentId);

                    -- Add FK to Users
                    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_UDR_Users')
                        ALTER TABLE UserDocumentRights WITH NOCHECK ADD CONSTRAINT FK_UDR_Users 
                            FOREIGN KEY (UserId) REFERENCES Users(Id);
                END

                -- V6: Add DepartmentId FK to Users table
                IF EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
                BEGIN
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'DepartmentId')
                        ALTER TABLE Users ADD DepartmentId INT NULL;

                    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Users_Departments')
                    AND EXISTS (SELECT * FROM sysobjects WHERE name='Departments' AND xtype='U')
                        ALTER TABLE Users WITH NOCHECK ADD CONSTRAINT FK_Users_Departments 
                            FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId);
                END

                -- V7: Consolidate UserGroups permission columns into DefaultRights bitmask
                IF EXISTS (SELECT * FROM sysobjects WHERE name='UserGroups' AND xtype='U')
                BEGIN
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('UserGroups') AND name = 'DefaultRights')
                    BEGIN
                        ALTER TABLE UserGroups ADD DefaultRights INT NOT NULL DEFAULT 0;

                        -- Migrate existing data: CanRead=1, CanWrite=2, CanDelete=4
                        EXEC('UPDATE UserGroups 
                        SET DefaultRights = 
                            (CASE WHEN CanRead > 0 THEN 1 ELSE 0 END) |
                            (CASE WHEN CanWrite > 0 THEN 2 ELSE 0 END) |
                            (CASE WHEN CanDelete > 0 THEN 4 ELSE 0 END)');
                    END
                END

                -- Add missing Categories columns (exist in model but not DDL)
                IF EXISTS (SELECT * FROM sysobjects WHERE name='Categories' AND xtype='U')
                BEGIN
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Categories') AND name = 'IsDeleted')
                        ALTER TABLE Categories ADD IsDeleted BIT NOT NULL DEFAULT 0;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Categories') AND name = 'Description')
                        ALTER TABLE Categories ADD Description NVARCHAR(1000) NULL;
                END

                -- Add FK on tblComment to Documents (COMMENTED OUT DUE TO DATA TYPE MISMATCH)
                /*
                IF EXISTS (SELECT * FROM sysobjects WHERE name='tblComment' AND xtype='U')
                BEGIN
                    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Comment_Documents')
                        ALTER TABLE tblComment WITH NOCHECK ADD CONSTRAINT FK_Comment_Documents 
                            FOREIGN KEY (DocumentID) REFERENCES Documents(DocumentId);

                    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Comment_Users')
                        ALTER TABLE tblComment WITH NOCHECK ADD CONSTRAINT FK_Comment_Users 
                            FOREIGN KEY (CommentBy) REFERENCES Users(Id);
                END
                */

                -- Add FK on DocumentVersions to Users (CreatedBy)
                IF EXISTS (SELECT * FROM sysobjects WHERE name='DocumentVersions' AND xtype='U')
                BEGIN
                    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_DocVersions_Users')
                        ALTER TABLE DocumentVersions WITH NOCHECK ADD CONSTRAINT FK_DocVersions_Users 
                            FOREIGN KEY (CreatedBy) REFERENCES Users(Id);
                END

                -- Add CreatedBy column to Categories for per-user scoping
                IF EXISTS (SELECT * FROM sysobjects WHERE name='Categories' AND xtype='U')
                BEGIN
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Categories') AND name = 'CreatedBy')
                        ALTER TABLE Categories ADD CreatedBy INT NULL;
                END
            ";
            command.ExecuteNonQuery();
            _logger.LogInformation("Normalization migration applied successfully.");

            _logger.LogInformation("Database schema verified/created successfully via ADO.NET.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Critical error during database schema initialization.");
            throw; // Re-throw because the app cannot function without the database schema
        }
    }
}
