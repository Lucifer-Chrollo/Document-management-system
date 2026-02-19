using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Microsoft.Data.SqlClient;

namespace DocumentManagementSystem.Helpers;

/// <summary>
/// Provides central resiliency policies for handling transient failures.
/// </summary>
public static class ResiliencyPolicies
{
    /// <summary>
    /// Creates a retry policy for SQL Server transient errors.
    /// Retries 3 times with exponential backoff (2s, 4s, 8s).
    /// </summary>
    public static AsyncRetryPolicy GetSqlRetryPolicyAsync(ILogger logger, string operationName)
    {
        return Policy
            .Handle<SqlException>(ex => IsTransient(ex))
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    logger.LogWarning(
                        exception,
                        "Retry {RetryCount} for {Operation} after {Delay}ms due to {ErrorMessage}",
                        retryCount,
                        operationName,
                        timeSpan.TotalMilliseconds,
                        exception.Message);
                });
    }

    /// <summary>
    /// Synchronous version of the SQL retry policy.
    /// </summary>
    public static RetryPolicy GetSqlRetryPolicy(ILogger logger, string operationName)
    {
        return Policy
            .Handle<SqlException>(ex => IsTransient(ex))
            .WaitAndRetry(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    logger.LogWarning(
                        exception,
                        "Retry {RetryCount} for {Operation} after {Delay}ms due to {ErrorMessage}",
                        retryCount,
                        operationName,
                        timeSpan.TotalMilliseconds,
                        exception.Message);
                });
    }

    private static bool IsTransient(SqlException ex)
    {
        // Common SQL Transient Error Codes
        return ex.Number switch
        {
            1205 => true,  // Deadlock
            -2   => true,  // Timeout
            4060 => true,  // Database unavailable
            40197 => true, // Service error
            40501 => true, // Service busy
            40613 => true, // Database unavailable
            49918 => true, // Resource limits reached
            49919 => true, // Resource limits reached
            49920 => true, // Resource limits reached
            _ => false
        };
    }
}
