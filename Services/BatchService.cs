using System.Data;
using System.Data.Common;
using DocumentManagementSystem.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Practices.EnterpriseLibrary.Data;

namespace DocumentManagementSystem.Services;

public class BatchService : IBatchService
{
    private readonly Database _db;
    private readonly ILogger<BatchService> _logger;

    public BatchService(Database db, ILogger<BatchService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IEnumerable<string>> GetBatchLabelsAsync(int? categoryId = null)
    {
        try
        {
            return ResiliencyPolicies.GetSqlRetryPolicy(_logger, "GetBatchLabels").Execute(() =>
            {
                var labels = new List<string>();

                var query = "SELECT DISTINCT BatchLabel FROM Documents WHERE BatchLabel IS NOT NULL AND IsDeleted = 0";
                if (categoryId.HasValue)
                    query += " AND CategoryID = @CategoryId";

                DbCommand command = _db.GetSqlStringCommand(query);
                if (categoryId.HasValue)
                    _db.AddInParameter(command, "@CategoryId", DbType.Int32, categoryId.Value);

                using (IDataReader reader = _db.ExecuteReader(command))
                {
                    while (reader.Read())
                    {
                        labels.Add(reader.GetString(0));
                    }
                }
                return (IEnumerable<string>)labels;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting batch labels after retries");
            return Enumerable.Empty<string>();
        }
    }
}
