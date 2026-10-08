using Microsoft.Data.SqlClient;

namespace NICEDA.ApiService.Sql;

/// <summary>
/// Read side of the CQRS split (NICEDA-PRACTICE-8): the only way to read
/// customer risk is this named, business-shaped query against
/// <c>optimized.CustomerHealthView</c> — never a raw table scan.
/// </summary>
public sealed record CustomerHealth(
    string CustomerId,
    string CustomerName,
    string Region,
    string StatusCode,
    decimal AnnualValue,
    DateTime EndDate,
    int RenewalInDays,
    string RiskLevel);

public static class CustomerHealthQueries
{
    public static async Task<List<CustomerHealth>> GetCustomerHealthAsync(
        SqlConnection connection, string? customerId = null, CancellationToken cancellationToken = default)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var query = "SELECT CustomerId, CustomerName, Region, StatusCode, AnnualValue, EndDate, RenewalInDays, RiskLevel FROM optimized.CustomerHealthView";
        if (customerId is not null)
        {
            query += " WHERE CustomerId = @customerId";
        }
        query += " ORDER BY CustomerName;";

        await using var command = new SqlCommand(query, connection);
        if (customerId is not null)
        {
            command.Parameters.AddWithValue("@customerId", customerId);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<CustomerHealth>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new CustomerHealth(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetDecimal(4),
                reader.GetDateTime(5),
                reader.GetInt32(6),
                reader.GetString(7)));
        }
        return results;
    }
}
