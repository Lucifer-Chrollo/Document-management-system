using System.Collections.Generic;
using System.Threading.Tasks;
using DocumentManagementSystem.Models;

namespace DocumentManagementSystem.Services;

/// <summary>
/// Service interface for User Group and Membership Management.
/// All mutating operations return iFishResponse (legacy iBusinessFlex pattern).
/// </summary>
public interface IUserGroupService
{
    // Read Operations (return data directly)
    Task<IEnumerable<UserGroup>> GetAllGroupsAsync();
    Task<UserGroup?> GetGroupByIdAsync(int groupId);
    Task<IEnumerable<DmsUser>> GetUsersInGroupAsync(int groupId);
    Task<IEnumerable<UserGroupMember>> GetGroupMembersDetailedAsync(int groupId);
    Task<IEnumerable<UserGroup>> GetGroupsForUserAsync(int userId);
    Task<IEnumerable<UserGroup>> GetGroupsCreatedByUserAsync(int userId);
    Task<IEnumerable<ApplicationUser>> SearchUsersAsync(string searchTerm);

    // Write Operations (return iFishResponse)
    Task<iFishResponse> CreateGroupAsync(UserGroup group, int createdByUserId);
    Task<iFishResponse> UpdateGroupAsync(UserGroup group, int updatedByUserId);
    Task<iFishResponse> DeleteGroupAsync(int groupId, int deletedByUserId);
    Task<iFishResponse> AddUserToGroupAsync(int userId, int groupId);
    Task<iFishResponse> RemoveUserFromGroupAsync(int userId, int groupId);

    // Aliases for UI compatibility
    Task<UserGroup?> GetGroupAsync(int groupId) => GetGroupByIdAsync(groupId);
    Task<IEnumerable<UserGroupMember>> GetGroupMembersAsync(int groupId) => GetGroupMembersDetailedAsync(groupId);
    Task<iFishResponse> RemoveMemberAsync(int groupId, int userId) => RemoveUserFromGroupAsync(userId, groupId);
    Task<iFishResponse> AddMemberAsync(int groupId, int userId) => AddUserToGroupAsync(userId, groupId);
}
