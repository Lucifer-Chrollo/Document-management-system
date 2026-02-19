using DocumentManagementSystem.Models;

namespace DocumentManagementSystem.Services;

public interface ILocationService
{
    Task<IEnumerable<Location>> GetLocationsAsync();
}
