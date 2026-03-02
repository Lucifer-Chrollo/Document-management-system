using System.Data;
using System.Data.Common;
using DocumentManagementSystem.Helpers;
using DocumentManagementSystem.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Practices.EnterpriseLibrary.Data;

namespace DocumentManagementSystem.Services;

/// <summary>
/// Enterprise Library implementation of Category Service.
/// Read operations return data directly; write operations return iFishResponse (legacy pattern).
/// </summary>
public class CategoryService : ICategoryService
{
    private readonly Database _db;
    private readonly ILogger<CategoryService> _logger;

    private const string SQL_SELECT_ALL = "SELECT * FROM Categories";
    private const string SQL_SELECT_BY_USER = "SELECT * FROM Categories WHERE (CreatedBy = @UserId OR CategoryId IN (1, 2, 3)) AND (IsDeleted = 0 OR IsDeleted IS NULL) ORDER BY CategoryName";
    private const string SQL_SELECT_BY_ID = "SELECT * FROM Categories WHERE CategoryId = @CategoryId";
    private const string SQL_GET_MAX_ID = "SELECT MAX(CategoryId) FROM Categories";
    private const string SQL_INSERT = @"
        INSERT INTO Categories (CategoryId, CategoryName, CreatedDate, UpdatedDate, CreatedBy)
        VALUES (@CategoryId, @CategoryName, @CreatedDate, @UpdatedDate, @CreatedBy);";
    private const string SQL_UPDATE = @"
        UPDATE Categories 
        SET CategoryName = @CategoryName, UpdatedDate = @UpdatedDate
        WHERE CategoryId = @CategoryId";
    private const string SQL_DELETE = "DELETE FROM Categories WHERE CategoryId = @CategoryId";

    public CategoryService(Database db, ILogger<CategoryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IEnumerable<Category>> GetCategoriesAsync()
    {
        try
        {
            return ResiliencyPolicies.GetSqlRetryPolicy(_logger, "GetCategories").Execute(() =>
            {
                var categories = new List<Category>();
                DbCommand command = _db.GetSqlStringCommand(SQL_SELECT_ALL);
                using (IDataReader reader = _db.ExecuteReader(command))
                {
                    while (reader.Read())
                    {
                        categories.Add(MapCategory(reader));
                    }
                }
                return (IEnumerable<Category>)categories;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories after retries");
            return Enumerable.Empty<Category>();
        }
    }

    public async Task<IEnumerable<Category>> GetCategoriesByUserAsync(int userId)
    {
        try
        {
            return ResiliencyPolicies.GetSqlRetryPolicy(_logger, "GetCategoriesByUser").Execute(() =>
            {
                var categories = new List<Category>();
                DbCommand command = _db.GetSqlStringCommand(SQL_SELECT_BY_USER);
                _db.AddInParameter(command, "@UserId", DbType.Int32, userId);
                using (IDataReader reader = _db.ExecuteReader(command))
                {
                    while (reader.Read())
                    {
                        categories.Add(MapCategory(reader));
                    }
                }
                return (IEnumerable<Category>)categories;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories for user {UserId}", userId);
            return Enumerable.Empty<Category>();
        }
    }

    public async Task<Category?> GetCategoryByIdAsync(int categoryId)
    {
        try
        {
            return ResiliencyPolicies.GetSqlRetryPolicy(_logger, "GetCategoryById").Execute(() =>
            {
                DbCommand command = _db.GetSqlStringCommand(SQL_SELECT_BY_ID);
                _db.AddInParameter(command, "@CategoryId", DbType.Int32, categoryId);
                using (IDataReader reader = _db.ExecuteReader(command))
                {
                    if (reader.Read())
                    {
                        return MapCategory(reader);
                    }
                }
                return (Category?)null;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting category {CategoryId}", categoryId);
            return null;
        }
    }

    public async Task<iFishResponse> CreateCategoryAsync(Category category)
    {
        var response = new iFishResponse();
        try
        {
            ResiliencyPolicies.GetSqlRetryPolicy(_logger, "CreateCategory").Execute(() =>
            {
                category.CreatedDate = DateTime.UtcNow;
                category.UpdatedDate = DateTime.UtcNow;

                // Manual ID Generation (Legacy DB support)
                int nextId = 1;
                DbCommand maxCmd = _db.GetSqlStringCommand(SQL_GET_MAX_ID);
                object result = _db.ExecuteScalar(maxCmd);
                if (result != null && result != DBNull.Value)
                {
                    nextId = Convert.ToInt32(result) + 1;
                }
                category.CategoryId = nextId;

                DbCommand command = _db.GetSqlStringCommand(SQL_INSERT);
                _db.AddInParameter(command, "@CategoryId", DbType.Int32, category.CategoryId);
                _db.AddInParameter(command, "@CategoryName", DbType.String, category.CategoryName);
                _db.AddInParameter(command, "@CreatedDate", DbType.DateTime, category.CreatedDate);
                _db.AddInParameter(command, "@UpdatedDate", DbType.DateTime, category.UpdatedDate);
                _db.AddInParameter(command, "@CreatedBy", DbType.Int32, (object?)category.CreatedBy ?? DBNull.Value);

                _db.ExecuteNonQuery(command);
            });

            response.Result = true;
            response.RecordID = category.CategoryId;
            response.Message = "Category created successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating category");
            response.Result = false;
            response.ReturnCode = -1;
            response.Message = ex.Message;
        }
        return response;
    }

    public async Task<iFishResponse> UpdateCategoryAsync(Category category)
    {
        var response = new iFishResponse();
        try
        {
            ResiliencyPolicies.GetSqlRetryPolicy(_logger, "UpdateCategory").Execute(() =>
            {
                category.UpdatedDate = DateTime.UtcNow;

                DbCommand command = _db.GetSqlStringCommand(SQL_UPDATE);
                _db.AddInParameter(command, "@CategoryName", DbType.String, category.CategoryName);
                _db.AddInParameter(command, "@UpdatedDate", DbType.DateTime, category.UpdatedDate);
                _db.AddInParameter(command, "@CategoryId", DbType.Int32, category.CategoryId);

                _db.ExecuteNonQuery(command);
            });

            response.Result = true;
            response.RecordID = category.CategoryId;
            response.Message = "Category updated successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating category {CategoryId}", category.CategoryId);
            response.Result = false;
            response.ReturnCode = -1;
            response.Message = ex.Message;
        }
        return response;
    }

    public async Task<iFishResponse> DeleteCategoryAsync(int categoryId)
    {
        var response = new iFishResponse();
        try
        {
            ResiliencyPolicies.GetSqlRetryPolicy(_logger, "DeleteCategory").Execute(() =>
            {
                DbCommand command = _db.GetSqlStringCommand(SQL_DELETE);
                _db.AddInParameter(command, "@CategoryId", DbType.Int32, categoryId);
                _db.ExecuteNonQuery(command);
            });

            response.Result = true;
            response.RecordID = categoryId;
            response.Message = "Category deleted successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting category {CategoryId}", categoryId);
            response.Result = false;
            response.ReturnCode = -1;
            response.Message = ex.Message;
        }
        return response;
    }

    private Category MapCategory(IDataReader reader)
    {
        var cat = new Category
        {
            CategoryId = Convert.ToInt32(reader["CategoryId"]),
            CategoryName = reader["CategoryName"] == DBNull.Value ? "" : Convert.ToString(reader["CategoryName"]) ?? "",
            CreatedDate = reader["CreatedDate"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["CreatedDate"]),
            UpdatedDate = reader["UpdatedDate"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["UpdatedDate"])
        };
        
        // Try reading optional columns safely if they exist
        try { cat.Description = reader["Description"] == DBNull.Value ? null : Convert.ToString(reader["Description"]); } catch {}
        try { cat.CategoryOrder = reader["CategoryOrder"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CategoryOrder"]); } catch {}
        try { cat.IsDeleted = reader["IsDeleted"] == DBNull.Value ? false : Convert.ToBoolean(reader["IsDeleted"]); } catch {}
    try { cat.CreatedBy = reader["CreatedBy"] == DBNull.Value ? null : Convert.ToInt32(reader["CreatedBy"]); } catch {}

        return cat;
    }
}
