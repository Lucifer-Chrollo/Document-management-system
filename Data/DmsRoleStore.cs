using Microsoft.AspNetCore.Identity;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentManagementSystem.Data;

public class DmsRoleStore : IRoleStore<IdentityRole<int>>
{
    public DmsRoleStore() { }

    public Task<IdentityResult> CreateAsync(IdentityRole<int> role, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
    public Task<IdentityResult> DeleteAsync(IdentityRole<int> role, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
    public Task<IdentityRole<int>?> FindByIdAsync(string roleId, CancellationToken cancellationToken) => Task.FromResult<IdentityRole<int>?>(null);
    public Task<IdentityRole<int>?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken) => Task.FromResult<IdentityRole<int>?>(null);
    public Task<string?> GetNormalizedRoleNameAsync(IdentityRole<int> role, CancellationToken cancellationToken) => Task.FromResult(role.NormalizedName);
    public Task<string> GetRoleIdAsync(IdentityRole<int> role, CancellationToken cancellationToken) => Task.FromResult(role.Id.ToString());
    public Task<string?> GetRoleNameAsync(IdentityRole<int> role, CancellationToken cancellationToken) => Task.FromResult(role.Name);
    public Task SetNormalizedRoleNameAsync(IdentityRole<int> role, string? normalizedName, CancellationToken cancellationToken) { role.NormalizedName = normalizedName; return Task.CompletedTask; }
    public Task SetRoleNameAsync(IdentityRole<int> role, string? roleName, CancellationToken cancellationToken) { role.Name = roleName; return Task.CompletedTask; }
    public Task<IdentityResult> UpdateAsync(IdentityRole<int> role, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
    public void Dispose() { }
}
