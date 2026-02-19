using DocumentManagementSystem.Models;
using DocumentManagementSystem.Services;
using Moq;
using Xunit;

namespace DocumentManagementSystem.Tests.Services;

public class UserGroupServiceTests
{
    private readonly Mock<IUserGroupService> _mockService;

    public UserGroupServiceTests()
    {
        _mockService = new Mock<IUserGroupService>();
    }

    [Fact]
    public async Task CreateGroup_ShouldReturniFishResponse_OnSuccess()
    {
        // Arrange
        var newGroup = new UserGroup { GroupName = "Test Group" };
        int creatorId = 1;
        var expectedResponse = new iFishResponse { Result = true, RecordID = 101, Message = "Group created successfully" };

        _mockService.Setup(s => s.CreateGroupAsync(It.IsAny<UserGroup>(), creatorId))
                    .ReturnsAsync(expectedResponse);

        // Act
        var result = await _mockService.Object.CreateGroupAsync(newGroup, creatorId);

        // Assert
        Assert.True(result.Result);
        Assert.Equal(101, result.RecordID);
        Assert.Equal("Group created successfully", result.Message);
        _mockService.Verify(s => s.CreateGroupAsync(It.Is<UserGroup>(g => g.GroupName == "Test Group"), creatorId), Times.Once);
    }

    [Fact]
    public async Task AddMember_ShouldReturniFishResponse_WhenAddedSuccessfully()
    {
        // Arrange
        int groupId = 101;
        int userId = 5;
        var expectedResponse = new iFishResponse { Result = true, Message = "User added to group successfully" };

        _mockService.Setup(s => s.AddMemberAsync(groupId, userId))
                    .ReturnsAsync(expectedResponse);

        // Act
        var result = await _mockService.Object.AddMemberAsync(groupId, userId);

        // Assert
        Assert.True(result.Result);
        Assert.Equal(0, result.ReturnCode); // 0 = newly added
        _mockService.Verify(s => s.AddMemberAsync(groupId, userId), Times.Once);
    }

    [Fact]
    public async Task GetGroupMembers_ShouldReturnCorrectCount()
    {
        // Arrange
        int groupId = 101;
        var mockMembers = new List<UserGroupMember>
        {
            new UserGroupMember { GroupId = groupId, UserId = 1, UserName = "User 1" },
            new UserGroupMember { GroupId = groupId, UserId = 2, UserName = "User 2" }
        };
        _mockService.Setup(s => s.GetGroupMembersDetailedAsync(groupId))
                    .ReturnsAsync(mockMembers);

        // Act
        var result = await _mockService.Object.GetGroupMembersDetailedAsync(groupId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        Assert.Contains(result, m => m.UserName == "User 1");
    }

    [Fact]
    public async Task DocumentSharing_ShouldCallService_WithCorrectIds()
    {
        // Arrange
        var mockDocService = new Mock<IDocumentService>();
        int docId = 505;
        int groupId = 101;
        
        // Act
        await mockDocService.Object.GrantGroupAccessAsync(docId, groupId, 1);

        // Assert
        mockDocService.Verify(s => s.GrantGroupAccessAsync(docId, groupId, 1), Times.Once);
    }
}
