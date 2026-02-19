using Microsoft.Practices.EnterpriseLibrary.Data;
using DocumentManagementSystem.Models;
using System.Data;
using System.Data.Common;

namespace DocumentManagementSystem.Services;

/// <summary>
/// Professional-grade Service for User Group and Membership Management.
/// All mutating operations return iFishResponse (legacy iBusinessFlex pattern).
/// </summary>
public class UserGroupService : IUserGroupService
{
    private readonly Database _db;

    public UserGroupService(Database db)
    {
        _db = db;
    }

    // ==================== READ OPERATIONS ====================

    public async Task<IEnumerable<UserGroup>> GetAllGroupsAsync()
    {
        var sql = @"
            SELECT g.*, u.FName + ' ' + u.LName as CreatorName,
                   (SELECT COUNT(1) FROM UserGroupMembers WHERE GroupId = g.GroupId) as MemberCount
            FROM UserGroups g
            LEFT JOIN Users u ON g.CreatedBy = u.UserID
            ORDER BY g.GroupName";
        
        DbCommand command = _db.GetSqlStringCommand(sql);
        var list = new List<UserGroup>();
        using var reader = _db.ExecuteReader(command);
        while (reader.Read())
        {
            list.Add(MapUserGroup(reader));
        }
        return list;
    }

    public async Task<UserGroup?> GetGroupByIdAsync(int groupId)
    {
        var sql = @"
            SELECT g.*, u.FName + ' ' + u.LName as CreatorName,
                   (SELECT COUNT(1) FROM UserGroupMembers WHERE GroupId = g.GroupId) as MemberCount
            FROM UserGroups g
            LEFT JOIN Users u ON g.CreatedBy = u.UserID
            WHERE g.GroupId = @GroupId";
        
        DbCommand command = _db.GetSqlStringCommand(sql);
        _db.AddInParameter(command, "@GroupId", DbType.Int32, groupId);

        using var reader = _db.ExecuteReader(command);
        if (reader.Read())
        {
            return MapUserGroup(reader);
        }
        return null;
    }

    public async Task<IEnumerable<DmsUser>> GetUsersInGroupAsync(int groupId)
    {
        var sql = @"
            SELECT u.* 
            FROM Users u
            INNER JOIN UserGroupMembers gm ON u.UserID = gm.UserId
            WHERE gm.GroupId = @GroupId";

        DbCommand command = _db.GetSqlStringCommand(sql);
        _db.AddInParameter(command, "@GroupId", DbType.Int32, groupId);

        var list = new List<DmsUser>();
        using var reader = _db.ExecuteReader(command);
        while (reader.Read())
        {
            list.Add(MapDmsUser(reader));
        }
        return list;
    }

    public async Task<IEnumerable<UserGroupMember>> GetGroupMembersDetailedAsync(int groupId)
    {
        var sql = @"
            SELECT gm.*, u.FName + ' ' + u.LName as UserName, u.LoginName as Email
            FROM UserGroupMembers gm
            INNER JOIN Users u ON gm.UserId = u.UserID
            WHERE gm.GroupId = @GroupId";

        DbCommand command = _db.GetSqlStringCommand(sql);
        _db.AddInParameter(command, "@GroupId", DbType.Int32, groupId);

        var list = new List<UserGroupMember>();
        using var reader = _db.ExecuteReader(command);
        while (reader.Read())
        {
            list.Add(MapUserGroupMember(reader));
        }
        return list;
    }

    public async Task<IEnumerable<UserGroup>> GetGroupsForUserAsync(int userId)
    {
        var sql = @"
            SELECT g.*
            FROM UserGroups g
            INNER JOIN UserGroupMembers gm ON g.GroupId = gm.GroupId
            WHERE gm.UserId = @UserId";

        DbCommand command = _db.GetSqlStringCommand(sql);
        _db.AddInParameter(command, "@UserId", DbType.Int32, userId);

        var list = new List<UserGroup>();
        using var reader = _db.ExecuteReader(command);
        while (reader.Read())
        {
            list.Add(MapUserGroup(reader));
        }
        return list;
    }

    public async Task<IEnumerable<ApplicationUser>> SearchUsersAsync(string searchTerm)
    {
        var sql = @"
            SELECT UserID as Id, LoginName as UserName, FName as FirstName, LName as LastName
            FROM Users
            WHERE FName LIKE @Term OR LName LIKE @Term OR LoginName LIKE @Term";
        
        DbCommand command = _db.GetSqlStringCommand(sql);
        _db.AddInParameter(command, "@Term", DbType.String, $"%{searchTerm}%");

        var list = new List<ApplicationUser>();
        using var reader = _db.ExecuteReader(command);
        while (reader.Read())
        {
            list.Add(new ApplicationUser {
                Id = reader.GetInt32(0),
                UserName = reader.GetString(1),
                FirstName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                LastName = reader.IsDBNull(3) ? "" : reader.GetString(3)
            });
        }
        return list;
    }

    // ==================== WRITE OPERATIONS (iFishResponse) ====================

    public async Task<iFishResponse> CreateGroupAsync(UserGroup group, int createdByUserId)
    {
        var response = new iFishResponse();
        try
        {
            group.CreatedBy = createdByUserId;
            group.CreatedDate = DateTime.UtcNow;

            var sql = @"
                INSERT INTO UserGroups (GroupName, CreatedBy, CreatedDate, CanRead, CanWrite, CanDelete)
                VALUES (@GroupName, @CreatedBy, @CreatedDate, @CanRead, @CanWrite, @CanDelete);
                SELECT CAST(SCOPE_IDENTITY() as int);";

            DbCommand command = _db.GetSqlStringCommand(sql);
            _db.AddInParameter(command, "@GroupName", DbType.String, group.GroupName);
            _db.AddInParameter(command, "@CreatedBy", DbType.Int32, group.CreatedBy);
            _db.AddInParameter(command, "@CreatedDate", DbType.DateTime, group.CreatedDate);
            _db.AddInParameter(command, "@CanRead", DbType.Int32, group.CanRead);
            _db.AddInParameter(command, "@CanWrite", DbType.Int32, group.CanWrite);
            _db.AddInParameter(command, "@CanDelete", DbType.Int32, group.CanDelete);

            var newId = Convert.ToInt32(_db.ExecuteScalar(command));
            response.Result = true;
            response.RecordID = newId;
            response.Message = "Group created successfully";
        }
        catch (Exception ex)
        {
            response.Result = false;
            response.ReturnCode = -1;
            response.Message = ex.Message;
        }
        return response;
    }

    public async Task<iFishResponse> UpdateGroupAsync(UserGroup group, int updatedByUserId)
    {
        var response = new iFishResponse();
        try
        {
            var sql = @"
                UPDATE UserGroups 
                SET GroupName = @GroupName, 
                    CanRead = @CanRead, 
                    CanWrite = @CanWrite, 
                    CanDelete = @CanDelete
                WHERE GroupId = @GroupId";

            DbCommand command = _db.GetSqlStringCommand(sql);
            _db.AddInParameter(command, "@GroupName", DbType.String, group.GroupName);
            _db.AddInParameter(command, "@CanRead", DbType.Int32, group.CanRead);
            _db.AddInParameter(command, "@CanWrite", DbType.Int32, group.CanWrite);
            _db.AddInParameter(command, "@CanDelete", DbType.Int32, group.CanDelete);
            _db.AddInParameter(command, "@GroupId", DbType.Int32, group.GroupId);

            _db.ExecuteNonQuery(command);

            response.Result = true;
            response.RecordID = group.GroupId;
            response.Message = "Group updated successfully";
        }
        catch (Exception ex)
        {
            response.Result = false;
            response.ReturnCode = -1;
            response.Message = ex.Message;
        }
        return response;
    }

    public async Task<iFishResponse> DeleteGroupAsync(int groupId, int deletedByUserId)
    {
        var response = new iFishResponse();
        try
        {
            var sql = @"DELETE FROM UserGroups WHERE GroupId = @GroupId";
            DbCommand command = _db.GetSqlStringCommand(sql);
            _db.AddInParameter(command, "@GroupId", DbType.Int32, groupId);
            _db.ExecuteNonQuery(command);

            response.Result = true;
            response.RecordID = groupId;
            response.Message = "Group deleted successfully";
        }
        catch (Exception ex)
        {
            response.Result = false;
            response.ReturnCode = -1;
            response.Message = ex.Message;
        }
        return response;
    }

    public async Task<iFishResponse> AddUserToGroupAsync(int userId, int groupId)
    {
        var response = new iFishResponse();
        try
        {
            // Idempotency check to prevent primary key violations
            DbCommand checkCmd = _db.GetSqlStringCommand("SELECT COUNT(1) FROM UserGroupMembers WHERE UserId = @UserId AND GroupId = @GroupId");
            _db.AddInParameter(checkCmd, "@UserId", DbType.Int32, userId);
            _db.AddInParameter(checkCmd, "@GroupId", DbType.Int32, groupId);
            
            var exists = Convert.ToInt32(_db.ExecuteScalar(checkCmd));

            if (exists == 0)
            {
                DbCommand insertCmd = _db.GetSqlStringCommand("INSERT INTO UserGroupMembers (UserId, GroupId) VALUES (@UserId, @GroupId)");
                _db.AddInParameter(insertCmd, "@UserId", DbType.Int32, userId);
                _db.AddInParameter(insertCmd, "@GroupId", DbType.Int32, groupId);
                _db.ExecuteNonQuery(insertCmd);

                response.Result = true;
                response.Message = "User added to group successfully";
            }
            else
            {
                response.Result = true;
                response.ReturnCode = 1; // Already exists
                response.Message = "User is already a member of this group";
            }
        }
        catch (Exception ex)
        {
            response.Result = false;
            response.ReturnCode = -1;
            response.Message = ex.Message;
        }
        return response;
    }

    public async Task<iFishResponse> RemoveUserFromGroupAsync(int userId, int groupId)
    {
        var response = new iFishResponse();
        try
        {
            DbCommand command = _db.GetSqlStringCommand("DELETE FROM UserGroupMembers WHERE UserId = @UserId AND GroupId = @GroupId");
            _db.AddInParameter(command, "@UserId", DbType.Int32, userId);
            _db.AddInParameter(command, "@GroupId", DbType.Int32, groupId);
            _db.ExecuteNonQuery(command);

            response.Result = true;
            response.Message = "User removed from group successfully";
        }
        catch (Exception ex)
        {
            response.Result = false;
            response.ReturnCode = -1;
            response.Message = ex.Message;
        }
        return response;
    }

    // ==================== MAPPER HELPERS ====================

    private UserGroup MapUserGroup(IDataReader reader)
    {
        return new UserGroup
        {
            GroupId = GetInt(reader, "GroupId"),
            GroupName = GetString(reader, "GroupName"),
            CreatedBy = GetInt(reader, "CreatedBy"),
            CreatedDate = GetDateTime(reader, "CreatedDate"),
            CanRead = GetInt(reader, "CanRead"),
            CanWrite = GetInt(reader, "CanWrite"),
            CanDelete = GetInt(reader, "CanDelete"),
            CreatorName = GetString(reader, "CreatorName"),
            MemberCount = GetInt(reader, "MemberCount")
        };
    }

    private DmsUser MapDmsUser(IDataReader reader)
    {
        return new DmsUser
        {
            UserID = GetInt(reader, "UserID"),
            LoginName = GetString(reader, "LoginName"),
            FName = GetString(reader, "FName"),
            LName = reader.IsDBNull(reader.GetOrdinal("LName")) ? null : reader.GetString(reader.GetOrdinal("LName")),
            Email = GetString(reader, "Email")
        };
    }

    private UserGroupMember MapUserGroupMember(IDataReader reader)
    {
        return new UserGroupMember
        {
            GroupId = GetInt(reader, "GroupId"),
            UserId = GetInt(reader, "UserId"),
            Role = GetString(reader, "Role"),
            JoinedDate = GetDateTime(reader, "JoinedDate"),
            UserName = GetString(reader, "UserName"),
            Email = GetString(reader, "Email")
        };
    }

    // Performance & Safety Helpers
    private string GetString(IDataReader reader, string column) => reader.IsDBNull(reader.GetOrdinal(column)) ? string.Empty : reader.GetString(reader.GetOrdinal(column));
    private int GetInt(IDataReader reader, string column) => reader.IsDBNull(reader.GetOrdinal(column)) ? 0 : reader.GetInt32(reader.GetOrdinal(column));
    private DateTime GetDateTime(IDataReader reader, string column) => reader.IsDBNull(reader.GetOrdinal(column)) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal(column));
}
