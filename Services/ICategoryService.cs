using DocumentManagementSystem.Models;

namespace DocumentManagementSystem.Services;

/// <summary>
/// Service interface for Category management.
/// Read operations return data directly; write operations return iFishResponse (legacy pattern).
/// </summary>
public interface ICategoryService
{
    // Read Operations
    Task<IEnumerable<Category>> GetCategoriesAsync();
    Task<IEnumerable<Category>> GetCategoriesByUserAsync(int userId);
    Task<Category?> GetCategoryByIdAsync(int categoryId);

    // Write Operations (iFishResponse)
    Task<iFishResponse> CreateCategoryAsync(Category category);
    Task<iFishResponse> UpdateCategoryAsync(Category category);
    Task<iFishResponse> DeleteCategoryAsync(int categoryId);
}
