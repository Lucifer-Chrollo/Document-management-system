using DocumentManagementSystem.Models;
using Microsoft.Practices.EnterpriseLibrary.Data;
using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;

namespace DocumentManagementSystem.Services;

public interface IAutoCategorizationService
{
    /// <summary>
    /// Categorizes a document by filename/metadata rules.
    /// Returns iFishResponse with RecordID = matched CategoryId (0 if no match).
    /// </summary>
    Task<iFishResponse> CategorizeAsync(Document document, string? filename);
}

public class AutoCategorizationService : IAutoCategorizationService
{
    private readonly Database _db;
    private readonly ILogger<AutoCategorizationService> _logger;

    public AutoCategorizationService(Database db, ILogger<AutoCategorizationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Analyzes a document and suggests a Category ID based on keyword rules.
    /// Returns iFishResponse: Result=true if a match was found, RecordID=CategoryId.
    /// </summary>
    public async Task<iFishResponse> CategorizeAsync(Document document, string? filename)
    {
        var response = new iFishResponse();
        try
        {
            var rules = await GetActiveRulesAsync();
            var targetText = (filename ?? document.DocumentName).ToUpperInvariant();

            // 1. Filename Matching (Highest Priority)
            foreach (var rule in rules)
            {
                if (targetText.Contains(rule.Keyword.ToUpperInvariant()))
                {
                    _logger.LogInformation("Auto-categorized '{Doc}' to Category {CatId} via filename keyword '{Key}'", 
                        document.DocumentName, rule.CategoryId, rule.Keyword);
                    
                    response.Result = true;
                    response.RecordID = rule.CategoryId;
                    response.Message = $"Matched keyword '{rule.Keyword}'";
                    return response;
                }
            }

            // 2. Batch/Context Matching
            if (!string.IsNullOrEmpty(document.BatchLabel))
            {
                int catId = 0;
                if (document.BatchLabel.Contains("INV", StringComparison.OrdinalIgnoreCase)) catId = 1;
                else if (document.BatchLabel.Contains("CONT", StringComparison.OrdinalIgnoreCase)) catId = 2;
                else if (document.BatchLabel.Contains("REP", StringComparison.OrdinalIgnoreCase)) catId = 3;

                if (catId > 0)
                {
                    response.Result = true;
                    response.RecordID = catId;
                    response.Message = $"Matched batch label '{document.BatchLabel}'";
                    return response;
                }
            }

            // No match found
            response.Result = true; // Not an error, just no match
            response.RecordID = 0;
            response.Message = "No categorization rule matched";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during auto-categorization");
            response.Result = false;
            response.ReturnCode = -1;
            response.Message = ex.Message;
        }
        return response;
    }

    private async Task<List<CategorizationRule>> GetActiveRulesAsync()
    {
        var rules = new List<CategorizationRule>();
        DbCommand command = _db.GetSqlStringCommand("SELECT * FROM CategorizationRules WHERE IsActive = 1");
        using (IDataReader reader = _db.ExecuteReader(command))
        {
            while (reader.Read())
            {
                rules.Add(new CategorizationRule
                {
                    RuleId = Convert.ToInt32(reader["RuleId"]),
                    Keyword = Convert.ToString(reader["Keyword"]) ?? "",
                    CategoryId = Convert.ToInt32(reader["CategoryId"])
                });
            }
        }
        return rules;
    }

    private class CategorizationRule
    {
        public int RuleId { get; set; }
        public string Keyword { get; set; } = string.Empty;
        public int CategoryId { get; set; }
    }
}
