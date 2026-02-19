using DocumentManagementSystem.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Cryptography;
using System.Text;


namespace DocumentManagementSystem.Services;

/// <summary>
/// Service for managing secure QR-based upload sessions
/// </summary>
public interface IUploadSessionService
{
    /// <summary>
    /// Create a new upload session with generated token and PIN (Synchronous)
    /// </summary>
    (UploadSession session, string pin) CreateSession(int userId, int expiryMinutes = 15, int maxFiles = 10, int? categoryId = null, string? label = null, string? ipAddress = null);

    /// <summary>
    /// Create a new upload session with generated token and PIN
    /// </summary>
    Task<(UploadSession session, string pin)> CreateSessionAsync(int userId, int expiryMinutes = 15, int maxFiles = 10, int? categoryId = null, string? label = null, string? ipAddress = null);
    
    /// <summary>
    /// Get session by token (Synchronous)
    /// </summary>
    UploadSession? GetByToken(string token);

    /// <summary>
    /// Get session by token
    /// </summary>
    Task<UploadSession?> GetByTokenAsync(string token);
    
    /// <summary>
    /// Verify PIN for a session (Synchronous)
    /// </summary>
    bool VerifyPin(string token, string pin, string? ipAddress = null);

    /// <summary>
    /// Verify PIN for a session
    /// </summary>
    Task<bool> VerifyPinAsync(string token, string pin, string? ipAddress = null);
    
    /// <summary>
    /// Increment file count for session (Synchronous)
    /// </summary>
    bool IncrementFileCount(string token);

    /// <summary>
    /// Increment file count for session
    /// </summary>
    Task<bool> IncrementFileCountAsync(string token);
    
    /// <summary>
    /// Revoke/deactivate a session (Synchronous)
    /// </summary>
    bool RevokeSession(int sessionId, int userId);

    /// <summary>
    /// Revoke/deactivate a session
    /// </summary>
    Task<bool> RevokeSessionAsync(int sessionId, int userId);
    
    /// <summary>
    /// Get all sessions for a user (Synchronous)
    /// </summary>
    IEnumerable<UploadSession> GetUserSessions(int userId);

    /// <summary>
    /// Get all sessions for a user
    /// </summary>
    Task<IEnumerable<UploadSession>> GetUserSessionsAsync(int userId);
    
    /// <summary>
    /// Clean up expired sessions (Synchronous)
    /// </summary>
    void CleanupExpiredSessions();

    /// <summary>
    /// Clean up expired sessions
    /// </summary>
    Task CleanupExpiredSessionsAsync();
}

public class UploadSessionService : IUploadSessionService
{
    private readonly string _connectionString;
    private readonly ILogger<UploadSessionService> _logger;
    private const int PinLength = 6;
    private const int TokenLength = 32;

    #region SQL Queries

    private const string SQL_INSERT_SESSION = @"
        INSERT INTO UploadSessions (Token, UserId, CreatedAt, ExpiresAt, MaxFiles, FilesUploaded, IsActive, PinHash, DefaultCategoryId, Label, CreatorIpAddress)
        OUTPUT INSERTED.SessionId
        VALUES (@Token, @UserId, @CreatedAt, @ExpiresAt, @MaxFiles, @FilesUploaded, @IsActive, @PinHash, @DefaultCategoryId, @Label, @CreatorIpAddress)";

    private const string SQL_SELECT_SESSION_BY_TOKEN = @"
        SELECT s.*, u.UserName 
        FROM UploadSessions s
        JOIN Users u ON s.UserId = u.Id
        WHERE s.Token = @Token AND s.IsActive = 1 AND s.ExpiresAt > @Now";

    private const string SQL_SELECT_USER_SESSIONS = @"
        SELECT s.*, u.UserName 
        FROM UploadSessions s
        JOIN Users u ON s.UserId = u.Id
        WHERE s.UserId = @UserId
        ORDER BY s.CreatedAt DESC";

    private const string SQL_VERIFY_PIN = "SELECT PinHash, FailedAttempts, MaxAttempts, IsActive, ExpiresAt FROM UploadSessions WHERE Token = @Token";

    private const string SQL_INCREMENT_FILE_COUNT = @"
        UPDATE UploadSessions 
        SET FilesUploaded = FilesUploaded + 1, LastAccessedAt = @Now
        WHERE Token = @Token AND IsActive = 1 AND FilesUploaded < MaxFiles AND ExpiresAt > @Now";

    private const string SQL_REVOKE_SESSION = "UPDATE UploadSessions SET IsActive = 0 WHERE SessionId = @SessionId AND UserId = @UserId";

    private const string SQL_CLEANUP_EXPIRED = "UPDATE UploadSessions SET IsActive = 0 WHERE ExpiresAt < @Now AND IsActive = 1";

    private const string SQL_UPDATE_SESSION = @"
        UPDATE UploadSessions 
        SET IsActive = CASE WHEN @Verified = 1 THEN 1 ELSE IsActive END,
            IpAddress = CASE WHEN @Verified = 1 THEN @IpAddress ELSE IpAddress END,
            LastAccess = @Now
        WHERE Token = @Token";

    private const string SQL_INCREMENT_FAILED = "UPDATE UploadSessions SET FailedAttempts = FailedAttempts + 1 WHERE Token = @Token";

    #endregion

    public UploadSessionService(IConfiguration configuration, ILogger<UploadSessionService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string not configured");
        _logger = logger;
    }

    public (UploadSession session, string pin) CreateSession(int userId, int expiryMinutes = 15, int maxFiles = 10, int? categoryId = null, string? label = null, string? ipAddress = null)
    {
        var token = Guid.NewGuid().ToString("N");
        var pin = new Random().Next(100000, 999999).ToString(); // 6-digit PIN
        var hashedPin = ComputeSha256Hash(pin);
        
        var session = new UploadSession
        {
            Token = token,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes),
            MaxFiles = maxFiles,
            FilesUploaded = 0,
            IsActive = true,
            PinHash = hashedPin,
            DefaultCategoryId = categoryId,
            Label = label,
            CreatorIpAddress = ipAddress
        };

        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(SQL_INSERT_SESSION, connection);
            command.Parameters.Add("@Token", SqlDbType.NVarChar).Value = session.Token;
            command.Parameters.Add("@UserId", SqlDbType.Int).Value = session.UserId;
            command.Parameters.Add("@CreatedAt", SqlDbType.DateTime).Value = session.CreatedAt;
            command.Parameters.Add("@ExpiresAt", SqlDbType.DateTime).Value = session.ExpiresAt;
            command.Parameters.Add("@MaxFiles", SqlDbType.Int).Value = session.MaxFiles;
            command.Parameters.Add("@FilesUploaded", SqlDbType.Int).Value = session.FilesUploaded;
            command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = session.IsActive;
            command.Parameters.Add("@PinHash", SqlDbType.NVarChar).Value = session.PinHash;
            command.Parameters.Add("@DefaultCategoryId", SqlDbType.Int).Value = (object?)session.DefaultCategoryId ?? DBNull.Value;
            command.Parameters.Add("@Label", SqlDbType.NVarChar).Value = (object?)session.Label ?? DBNull.Value;
            command.Parameters.Add("@CreatorIpAddress", SqlDbType.NVarChar).Value = (object?)session.CreatorIpAddress ?? DBNull.Value;

            session.SessionId = (int)(command.ExecuteScalar() ?? 0);

            _logger.LogInformation("Created upload session {SessionId} for user {UserId}", session.SessionId, userId);
            
            return (session, pin);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating upload session");
            throw;
        }
    }

    public Task<(UploadSession session, string pin)> CreateSessionAsync(int userId, int expiryMinutes = 15, int maxFiles = 10, int? categoryId = null, string? label = null, string? ipAddress = null)
    {
        return Task.Run(() => CreateSession(userId, expiryMinutes, maxFiles, categoryId, label, ipAddress));
    }

    public UploadSession? GetByToken(string token)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            
            using var command = new SqlCommand(SQL_SELECT_SESSION_BY_TOKEN, connection);
            command.Parameters.Add("@Token", SqlDbType.NVarChar).Value = token;
            command.Parameters.Add("@Now", SqlDbType.DateTime).Value = DateTime.UtcNow;

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return MapSession(reader);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving session by token");
        }

        return null;
    }

    public Task<UploadSession?> GetByTokenAsync(string token)
    {
        return Task.Run(() => GetByToken(token));
    }

    public bool VerifyPin(string token, string pin, string? ipAddress = null)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            
            using var command = new SqlCommand(SQL_VERIFY_PIN, connection);
            command.Parameters.Add("@Token", SqlDbType.NVarChar).Value = token;

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var storedHash = reader["PinHash"] == DBNull.Value ? null : Convert.ToString(reader["PinHash"]);
                var failedAttempts = Convert.ToInt32(reader["FailedAttempts"]);
                var maxAttempts = Convert.ToInt32(reader["MaxAttempts"]);
                var isActive = Convert.ToBoolean(reader["IsActive"]);
                var expiresAt = Convert.ToDateTime(reader["ExpiresAt"]);

                if (!isActive || DateTime.UtcNow >= expiresAt || failedAttempts >= maxAttempts)
                {
                    return false; // Session inactive, expired, or locked out
                }

                if (storedHash != null && ComputeSha256Hash(pin) == storedHash)
                {
                    // PIN correct - mark as verified and update IP
                    UpdateSession(token, verified: true, ipAddress: ipAddress);
                    _logger.LogInformation("PIN verified for session with token {Token}", token);
                    return true;
                }
                else
                {
                    // Increment failed attempts
                    IncrementFailedAttempts(token);
                    _logger.LogWarning("Invalid PIN attempt for session with token {Token}", token);
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying PIN");
        }

        return false;
    }

    public Task<bool> VerifyPinAsync(string token, string pin, string? ipAddress = null)
    {
        return Task.Run(() => VerifyPin(token, pin, ipAddress));
    }

    public bool IncrementFileCount(string token)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(SQL_INCREMENT_FILE_COUNT, connection);
            command.Parameters.Add("@Token", SqlDbType.NVarChar).Value = token;
            command.Parameters.Add("@Now", SqlDbType.DateTime).Value = DateTime.UtcNow;

            var rowsAffected = command.ExecuteNonQuery();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing file count");
            return false;
        }
    }

    public Task<bool> IncrementFileCountAsync(string token)
    {
        return Task.Run(() => IncrementFileCount(token));
    }

    public bool RevokeSession(int sessionId, int userId)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(SQL_REVOKE_SESSION, connection);
            command.Parameters.Add("@SessionId", SqlDbType.Int).Value = sessionId;
            command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

            var rowsAffected = command.ExecuteNonQuery();
            if (rowsAffected > 0)
            {
                _logger.LogInformation("Revoked session {SessionId} for user {UserId}", sessionId, userId);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking session");
            return false;
        }
    }

    public Task<bool> RevokeSessionAsync(int sessionId, int userId)
    {
        return Task.Run(() => RevokeSession(sessionId, userId));
    }

    public IEnumerable<UploadSession> GetUserSessions(int userId)
    {
        var sessions = new List<UploadSession>();

        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(SQL_SELECT_USER_SESSIONS, connection);
            command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                sessions.Add(MapSession(reader));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user sessions");
        }

        return sessions;
    }

    public Task<IEnumerable<UploadSession>> GetUserSessionsAsync(int userId)
    {
        return Task.Run(() => GetUserSessions(userId));
    }

    public void CleanupExpiredSessions()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(SQL_CLEANUP_EXPIRED, connection);
            command.Parameters.Add("@Now", SqlDbType.DateTime).Value = DateTime.UtcNow;

            var rowsAffected = command.ExecuteNonQuery();
            if (rowsAffected > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired upload sessions", rowsAffected);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired sessions");
        }
    }

    public Task CleanupExpiredSessionsAsync()
    {
        return Task.Run(() => CleanupExpiredSessions());
    }

    private void UpdateSession(string token, bool verified, string? ipAddress)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = new SqlCommand(SQL_UPDATE_SESSION, connection);
        command.Parameters.Add("@Token", SqlDbType.NVarChar).Value = token;
        command.Parameters.Add("@Verified", SqlDbType.Bit).Value = verified;
        command.Parameters.Add("@IpAddress", SqlDbType.NVarChar).Value = (object?)ipAddress ?? DBNull.Value;
        command.Parameters.Add("@Now", SqlDbType.DateTime).Value = DateTime.UtcNow;

        command.ExecuteNonQuery();
    }

    private void IncrementFailedAttempts(string token)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = new SqlCommand(SQL_INCREMENT_FAILED, connection);
        command.Parameters.Add("@Token", SqlDbType.NVarChar).Value = token;

        command.ExecuteNonQuery();
    }

    private static string GeneratePin()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        var num = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1000000;
        return num.ToString("D6"); // 6 digits with leading zeros
    }

    private static string HashPin(string pin)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(pin + "DMS_SALT_2024");
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private static UploadSession MapSession(SqlDataReader reader)
    {
        return new UploadSession
        {
            SessionId = Convert.ToInt32(reader["SessionId"]),
            Token = reader["Token"] == DBNull.Value ? "" : Convert.ToString(reader["Token"]) ?? "",
            UserId = Convert.ToInt32(reader["UserId"]),
            CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
            ExpiresAt = Convert.ToDateTime(reader["ExpiresAt"]),
            MaxFiles = Convert.ToInt32(reader["MaxFiles"]),
            FilesUploaded = Convert.ToInt32(reader["FilesUploaded"]),
            IsActive = Convert.ToBoolean(reader["IsActive"]),
            IsPinVerified = Convert.ToBoolean(reader["IsPinVerified"]),
            CreatorIpAddress = reader["CreatorIpAddress"] == DBNull.Value ? null : Convert.ToString(reader["CreatorIpAddress"]),
            UploaderIpAddress = reader["UploaderIpAddress"] == DBNull.Value ? null : Convert.ToString(reader["UploaderIpAddress"]),
            LastAccessedAt = reader["LastAccessedAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["LastAccessedAt"]),
            DefaultCategoryId = reader["DefaultCategoryId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["DefaultCategoryId"]),
            Label = reader["Label"] == DBNull.Value ? null : Convert.ToString(reader["Label"]),
            UserName = reader["UserName"] == DBNull.Value ? null : Convert.ToString(reader["UserName"])
        };
    }

    private static string ComputeSha256Hash(string rawData)
    {
        using (SHA256 sha256Hash = SHA256.Create())
        {
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
    }
}
