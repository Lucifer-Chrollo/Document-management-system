using DocumentManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using DocumentManagementSystem.Services;

namespace DocumentManagementSystem.Data;

public class DmsUserStore : IUserStore<ApplicationUser>, IUserPasswordStore<ApplicationUser>, IUserEmailStore<ApplicationUser>, IUserLoginStore<ApplicationUser>
{
    private readonly string _connectionString;
    private readonly ILogger<DmsUserStore> _logger;
    private readonly IEncryptionService _encryptionService;

    #region SQL Queries

    private const string SQL_INSERT_USER = @"
        INSERT INTO Users (UserName, NormalizedUserName, Email, NormalizedEmail, 
            PasswordHash, SecurityStamp, ConcurrencyStamp, FirstName, LastName, Department)
        VALUES (@UserName, @NormalizedUserName, @Email, @NormalizedEmail, 
            @PasswordHash, @SecurityStamp, @ConcurrencyStamp, @FirstName, @LastName, @Department);
        SELECT CAST(SCOPE_IDENTITY() as int);";

    private const string SQL_DELETE_USER = "DELETE FROM Users WHERE Id = @Id";

    private const string SQL_SELECT_USER_BY_ID = "SELECT * FROM Users WHERE Id = @Id";

    private const string SQL_SELECT_USER_BY_NAME = "SELECT * FROM Users WHERE NormalizedUserName = @Name";

    private const string SQL_SELECT_USER_BY_EMAIL = "SELECT * FROM Users WHERE NormalizedEmail = @Email";

    private const string SQL_UPDATE_USER = @"
        UPDATE Users SET 
            UserName = @UserName, NormalizedUserName = @NormalizedUserName, 
            Email = @Email, NormalizedEmail = @NormalizedEmail, 
            PasswordHash = @PasswordHash, SecurityStamp = @SecurityStamp, 
            ConcurrencyStamp = @ConcurrencyStamp, FirstName = @FirstName, 
            LastName = @LastName, Department = @Department
        WHERE Id = @Id";

    private const string SQL_INSERT_LOGIN = "INSERT INTO UserLogins (LoginProvider, ProviderKey, ProviderDisplayName, UserId) VALUES (@LoginProvider, @ProviderKey, @ProviderDisplayName, @UserId)";

    private const string SQL_DELETE_LOGIN = "DELETE FROM UserLogins WHERE UserId = @UserId AND LoginProvider = @LoginProvider AND ProviderKey = @ProviderKey";

    private const string SQL_SELECT_LOGINS = "SELECT * FROM UserLogins WHERE UserId = @UserId";

    private const string SQL_SELECT_USER_BY_LOGIN = @"
        SELECT u.* 
        FROM Users u 
        JOIN UserLogins ul ON u.Id = ul.UserId 
        WHERE ul.LoginProvider = @LoginProvider AND ul.ProviderKey = @ProviderKey";

    #endregion

    public DmsUserStore(IConfiguration configuration, ILogger<DmsUserStore> logger, IEncryptionService encryptionService)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string not found");
        _logger = logger;
        _encryptionService = encryptionService;
    }

    public async Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = new SqlCommand(SQL_INSERT_USER, connection);
            command.Parameters.Add("@UserName", SqlDbType.NVarChar).Value = user.UserName;
            command.Parameters.Add("@NormalizedUserName", SqlDbType.NVarChar).Value = user.NormalizedUserName;
            
            // Secure Email Storage
            var encryptedEmail = await _encryptionService.EncryptTextAsync(user.Email ?? "");
            command.Parameters.Add("@Email", SqlDbType.NVarChar).Value = string.IsNullOrEmpty(user.Email) ? DBNull.Value : encryptedEmail;
            
            // NormalizedEmail is already Hashed by SetNormalizedEmailAsync, but if it wasn't rely on this
            // However, SetNormalizedEmailAsync is called by UserManager before CreateAsync.
            // But let's be safe: The property on User object SHOULD be the Hash.
            // So we just pass it.
            command.Parameters.Add("@NormalizedEmail", SqlDbType.NVarChar).Value = (object?)user.NormalizedEmail ?? DBNull.Value;
            
            command.Parameters.Add("@PasswordHash", SqlDbType.NVarChar).Value = (object?)user.PasswordHash ?? DBNull.Value;
            command.Parameters.Add("@SecurityStamp", SqlDbType.NVarChar).Value = (object?)user.SecurityStamp ?? DBNull.Value;
            command.Parameters.Add("@ConcurrencyStamp", SqlDbType.NVarChar).Value = (object?)user.ConcurrencyStamp ?? DBNull.Value;
            command.Parameters.Add("@FirstName", SqlDbType.NVarChar).Value = user.FirstName;
            command.Parameters.Add("@LastName", SqlDbType.NVarChar).Value = user.LastName;
            command.Parameters.Add("@Department", SqlDbType.NVarChar).Value = (object?)user.Department ?? DBNull.Value;

            var newItemId = await command.ExecuteScalarAsync(cancellationToken);
            if (newItemId != null)
            {
                user.Id = (int)newItemId;
                return IdentityResult.Success;
            }
            return IdentityResult.Failed(new IdentityError { Description = "Failed to insert user." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {UserName}", user.UserName);
            return IdentityResult.Failed(new IdentityError { Description = $"Error creating user: {ex.Message}" });
        }
    }

    public async Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            using var command = new SqlCommand(SQL_DELETE_USER, connection);
            command.Parameters.Add("@Id", SqlDbType.Int).Value = user.Id;
            await command.ExecuteNonQueryAsync(cancellationToken);
            
            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", user.Id);
            return IdentityResult.Failed(new IdentityError { Description = $"Error deleting user: {ex.Message}" });
        }
    }

    public void Dispose()
    {
        // Nothing to dispose
    }

    public async Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            if (!int.TryParse(userId, out int id)) return null;

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = new SqlCommand(SQL_SELECT_USER_BY_ID, connection);
            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return await MapUserAsync(reader);
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding user by id {UserId}", userId);
            return null;
        }
    }

    public async Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = new SqlCommand(SQL_SELECT_USER_BY_NAME, connection);
            command.Parameters.Add("@Name", SqlDbType.NVarChar).Value = normalizedUserName;

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return await MapUserAsync(reader);
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding user by name {Name}", normalizedUserName);
            return null;
        }
    }

    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.NormalizedUserName);
    }

    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Id.ToString());
    }

    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.UserName);
    }

    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
    {
        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
    {
        user.UserName = userName;
        return Task.CompletedTask;
    }

    public async Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Update Email: Encrypt plain email, Store Hash for index
            var encryptedEmail = await _encryptionService.EncryptTextAsync(user.Email ?? "");

            using var command = new SqlCommand(SQL_UPDATE_USER, connection);
            command.Parameters.Add("@UserName", SqlDbType.NVarChar).Value = user.UserName;
            command.Parameters.Add("@NormalizedUserName", SqlDbType.NVarChar).Value = user.NormalizedUserName;
            command.Parameters.Add("@Email", SqlDbType.NVarChar).Value = string.IsNullOrEmpty(user.Email) ? DBNull.Value : encryptedEmail;
            command.Parameters.Add("@NormalizedEmail", SqlDbType.NVarChar).Value = (object?)user.NormalizedEmail ?? DBNull.Value; 
            command.Parameters.Add("@PasswordHash", SqlDbType.NVarChar).Value = (object?)user.PasswordHash ?? DBNull.Value;
            command.Parameters.Add("@SecurityStamp", SqlDbType.NVarChar).Value = (object?)user.SecurityStamp ?? DBNull.Value;
            command.Parameters.Add("@ConcurrencyStamp", SqlDbType.NVarChar).Value = (object?)user.ConcurrencyStamp ?? DBNull.Value;
            command.Parameters.Add("@FirstName", SqlDbType.NVarChar).Value = user.FirstName;
            command.Parameters.Add("@LastName", SqlDbType.NVarChar).Value = user.LastName;
            command.Parameters.Add("@Department", SqlDbType.NVarChar).Value = (object?)user.Department ?? DBNull.Value;
            command.Parameters.Add("@Id", SqlDbType.Int).Value = user.Id;

            await command.ExecuteNonQueryAsync(cancellationToken);
            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", user.Id);
            return IdentityResult.Failed(new IdentityError { Description = $"Error updating user: {ex.Message}" });
        }
    }

    // Password Store methods
    public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.PasswordHash);
    }

    public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));
    }

    public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken cancellationToken)
    {
        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }
    
    // Email Store methods
    public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken cancellationToken)
    {
        user.Email = email; // Set plain text on memory object
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Email); // Return plain text from memory object
    }

    public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.EmailConfirmed);
    }

    public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken)
    {
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public async Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        try
        {
            // normalizedEmail is passed in. We Hash it to find content.
            // NOTE: Identity passes the NORMALIZED email string here.
            var emailHash = _encryptionService.Hash(normalizedEmail);

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = new SqlCommand(SQL_SELECT_USER_BY_EMAIL, connection);
            command.Parameters.Add("@Email", SqlDbType.NVarChar).Value = emailHash;

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return await MapUserAsync(reader);
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding user by email {Email}", normalizedEmail);
            return null;
        }
    }

    public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.NormalizedEmail);
    }

    public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        // Store Hashed Index on the user object
        if (normalizedEmail != null)
        {
            user.NormalizedEmail = _encryptionService.Hash(normalizedEmail);
        }
        else
        {
            user.NormalizedEmail = null;
        }
        return Task.CompletedTask;
    }

    private async Task<ApplicationUser> MapUserAsync(SqlDataReader reader)
    {
        var user = new ApplicationUser
        {
            Id = Convert.ToInt32(reader["Id"]),
            UserName = reader["UserName"] == DBNull.Value ? null : Convert.ToString(reader["UserName"]),
            NormalizedUserName = reader["NormalizedUserName"] == DBNull.Value ? null : Convert.ToString(reader["NormalizedUserName"]),
            // Email is Encrypted in DB, Decrypt it for the Object
            Email = await _encryptionService.DecryptTextAsync(reader["Email"] == DBNull.Value ? "" : Convert.ToString(reader["Email"]) ?? ""), 
            NormalizedEmail = reader["NormalizedEmail"] == DBNull.Value ? null : Convert.ToString(reader["NormalizedEmail"]), // This is the Hash, kept as is
            PasswordHash = reader["PasswordHash"] == DBNull.Value ? null : Convert.ToString(reader["PasswordHash"]),
            SecurityStamp = reader["SecurityStamp"] == DBNull.Value ? null : Convert.ToString(reader["SecurityStamp"]),
            ConcurrencyStamp = reader["ConcurrencyStamp"] == DBNull.Value ? null : Convert.ToString(reader["ConcurrencyStamp"]),
            FirstName = reader["FirstName"] == DBNull.Value ? "" : Convert.ToString(reader["FirstName"]) ?? "",
            LastName = reader["LastName"] == DBNull.Value ? "" : Convert.ToString(reader["LastName"]) ?? "",
            Department = reader["Department"] == DBNull.Value ? null : Convert.ToString(reader["Department"])
        };
        return user;
    }

    // IUserLoginStore Methods
    public async Task AddLoginAsync(ApplicationUser user, UserLoginInfo login, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(SQL_INSERT_LOGIN, connection);
        command.Parameters.Add("@LoginProvider", SqlDbType.NVarChar).Value = login.LoginProvider;
        command.Parameters.Add("@ProviderKey", SqlDbType.NVarChar).Value = login.ProviderKey;
        command.Parameters.Add("@ProviderDisplayName", SqlDbType.NVarChar).Value = login.ProviderDisplayName ?? "";
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = user.Id;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemoveLoginAsync(ApplicationUser user, string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(SQL_DELETE_LOGIN, connection);
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = user.Id;
        command.Parameters.Add("@LoginProvider", SqlDbType.NVarChar).Value = loginProvider;
        command.Parameters.Add("@ProviderKey", SqlDbType.NVarChar).Value = providerKey;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IList<UserLoginInfo>> GetLoginsAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var logins = new List<UserLoginInfo>(); // Standard UserLoginInfo
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(SQL_SELECT_LOGINS, connection);
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = user.Id;

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            logins.Add(new UserLoginInfo(
                Convert.ToString(reader["LoginProvider"]) ?? "",
                Convert.ToString(reader["ProviderKey"]) ?? "",
                Convert.ToString(reader["ProviderDisplayName"])));
        }

        return logins;
    }

    public async Task<ApplicationUser?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(SQL_SELECT_USER_BY_LOGIN, connection);
        command.Parameters.Add("@LoginProvider", SqlDbType.NVarChar).Value = loginProvider;
        command.Parameters.Add("@ProviderKey", SqlDbType.NVarChar).Value = providerKey;

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return await MapUserAsync(reader);
        }
        return null;
    }
}
