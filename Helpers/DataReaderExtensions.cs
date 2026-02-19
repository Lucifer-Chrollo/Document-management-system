using System.Data;

namespace DocumentManagementSystem.Helpers;

/// <summary>
/// Extension methods for IDataReader to eliminate repetitive null-check boilerplate.
/// </summary>
/// <remarks>
/// <b>Before:</b> <c>reader["Column"] == DBNull.Value ? "" : Convert.ToString(reader["Column"]) ?? ""</c>
/// <br/>
/// <b>After:</b> <c>reader.GetStringSafe("Column")</c>
/// <para>
/// This reduces ~35-line mapper methods to ~20 lines while being more readable and less error-prone.
/// </para>
/// </remarks>
public static class DataReaderExtensions
{
    public static string GetStringSafe(this IDataReader reader, string column, string fallback = "")
        => reader[column] == DBNull.Value ? fallback : Convert.ToString(reader[column]) ?? fallback;

    public static string? GetStringNullable(this IDataReader reader, string column)
        => reader[column] == DBNull.Value ? null : Convert.ToString(reader[column]);

    public static int GetInt32Safe(this IDataReader reader, string column, int fallback = 0)
        => reader[column] == DBNull.Value ? fallback : Convert.ToInt32(reader[column]);

    public static int? GetInt32Nullable(this IDataReader reader, string column)
        => reader[column] == DBNull.Value ? null : Convert.ToInt32(reader[column]);

    public static long GetInt64Safe(this IDataReader reader, string column, long fallback = 0)
        => reader[column] == DBNull.Value ? fallback : Convert.ToInt64(reader[column]);

    public static long? GetInt64Nullable(this IDataReader reader, string column)
        => reader[column] == DBNull.Value ? null : Convert.ToInt64(reader[column]);

    public static decimal GetDecimalSafe(this IDataReader reader, string column, decimal fallback = 0)
        => reader[column] == DBNull.Value ? fallback : Convert.ToDecimal(reader[column]);

    public static decimal? GetDecimalNullable(this IDataReader reader, string column)
        => reader[column] == DBNull.Value ? null : Convert.ToDecimal(reader[column]);

    public static bool GetBoolSafe(this IDataReader reader, string column, bool fallback = false)
        => reader[column] == DBNull.Value ? fallback : Convert.ToBoolean(reader[column]);

    public static DateTime GetDateTimeSafe(this IDataReader reader, string column)
        => reader[column] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader[column]);

    public static DateTime? GetDateTimeNullable(this IDataReader reader, string column)
        => reader[column] == DBNull.Value ? null : Convert.ToDateTime(reader[column]);
}
