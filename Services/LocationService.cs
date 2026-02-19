using System.Data;
using System.Data.Common;
using DocumentManagementSystem.Helpers;
using DocumentManagementSystem.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Practices.EnterpriseLibrary.Data;

namespace DocumentManagementSystem.Services;

public class LocationService : ILocationService
{
    private readonly Database _db;
    private readonly ILogger<LocationService> _logger;

    public LocationService(Database db, ILogger<LocationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IEnumerable<Location>> GetLocationsAsync()
    {
        try
        {
            return ResiliencyPolicies.GetSqlRetryPolicy(_logger, "GetLocations").Execute(() =>
            {
                var locations = new List<Location>();
                DbCommand command = _db.GetSqlStringCommand("SELECT * FROM Locations ORDER BY SortOrder");
                using (IDataReader reader = _db.ExecuteReader(command))
                {
                    while (reader.Read())
                    {
                        locations.Add(new Location
                        {
                            LocationId = Convert.ToInt32(reader["LocationId"]),
                            LocationName = reader["LocationName"] == DBNull.Value ? "" : Convert.ToString(reader["LocationName"]) ?? "",
                            SortOrder = Convert.ToInt32(reader["SortOrder"])
                        });
                    }
                }
                return (IEnumerable<Location>)locations;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting locations after retries");
            return Enumerable.Empty<Location>();
        }
    }
}
