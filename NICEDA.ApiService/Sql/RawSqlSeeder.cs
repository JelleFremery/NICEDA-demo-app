using System.Data;
using Microsoft.Data.SqlClient;

namespace NICEDA.ApiService.Sql;

/// <summary>
/// Creates and seeds the "Before" SQL shape: an over-normalized <c>raw</c>
/// schema where even a simple business question ("is this customer at
/// risk, and for how much money?") requires joining Customer, Contract,
/// ContractLine, and Status. No business-friendly projection exists here
/// on purpose — that's what the optimized schema (a later slice) adds.
/// </summary>
public sealed class RawSqlSeeder
{
    private static readonly string[] SchemaStatements =
    [
        "IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'raw') EXEC('CREATE SCHEMA raw');",
        """
        IF OBJECT_ID('raw.Status', 'U') IS NULL
        CREATE TABLE raw.Status (
            StatusId INT NOT NULL PRIMARY KEY,
            StatusCode NVARCHAR(50) NOT NULL
        );
        """,
        """
        IF OBJECT_ID('raw.Customer', 'U') IS NULL
        CREATE TABLE raw.Customer (
            CustomerId NVARCHAR(50) NOT NULL PRIMARY KEY,
            CustomerName NVARCHAR(200) NOT NULL,
            Region NVARCHAR(50) NOT NULL,
            SalesRepEmail NVARCHAR(200) NOT NULL
        );
        """,
        """
        IF OBJECT_ID('raw.Contract', 'U') IS NULL
        CREATE TABLE raw.Contract (
            ContractId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            CustomerId NVARCHAR(50) NOT NULL REFERENCES raw.Customer(CustomerId),
            StatusId INT NOT NULL REFERENCES raw.Status(StatusId),
            StartDate DATE NOT NULL,
            EndDate DATE NOT NULL,
            AnnualValue DECIMAL(12,2) NOT NULL
        );
        """,
        """
        IF OBJECT_ID('raw.ContractLine', 'U') IS NULL
        CREATE TABLE raw.ContractLine (
            ContractLineId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            ContractId INT NOT NULL REFERENCES raw.Contract(ContractId),
            ProductCode NVARCHAR(50) NOT NULL,
            Description NVARCHAR(200) NOT NULL,
            Quantity INT NOT NULL,
            UnitPrice DECIMAL(12,2) NOT NULL
        );
        """,
    ];

    // CustomerId here matches NICEDA.ApiService.Crm.CrmCustomer.Id so the
    // same fictional customers (Betabit, Fenetre, Techorama, Contoso,
    // Fabrikam) can be cross-referenced across CRM/SQL/Cosmos/SharePoint.
    internal static readonly (string Id, string Name, string Region, string Rep, string Status, decimal AnnualValue)[] SeedCustomers =
    [
        ("betabit", "Betabit", "EU", "jane.okafor@niceda.example", "AtRisk", 180000m),
        ("fenetre", "Fenetre", "EU", "jane.okafor@niceda.example", "Renewed", 60000m),
        ("techorama", "Techorama", "EU", "marcus.lindqvist@niceda.example", "Active", 95000m),
        ("contoso", "Contoso", "NA", "marcus.lindqvist@niceda.example", "Active", 310000m),
        ("fabrikam", "Fabrikam", "NA", "jane.okafor@niceda.example", "AtRisk", 45000m),
    ];

    private static readonly (int Id, string Code)[] Statuses =
    [
        (1, "Active"),
        (2, "AtRisk"),
        (3, "Renewed"),
        (4, "Expiring"),
        (5, "Cancelled"),
    ];

    public async Task EnsureSeededAsync(SqlConnection connection, CancellationToken cancellationToken = default)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        foreach (var statement in SchemaStatements)
        {
            await using var command = new SqlCommand(statement, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await SeedIfEmptyAsync(connection, cancellationToken);
    }

    private static async Task SeedIfEmptyAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using (var countCommand = new SqlCommand("SELECT COUNT(*) FROM raw.Customer;", connection))
        {
            var existing = (int)(await countCommand.ExecuteScalarAsync(cancellationToken))!;
            if (existing > 0)
            {
                return;
            }
        }

        foreach (var (id, code) in Statuses)
        {
            await using var command = new SqlCommand(
                "INSERT INTO raw.Status (StatusId, StatusCode) VALUES (@id, @code);", connection);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@code", code);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var customer in SeedCustomers)
        {
            await using (var customerCommand = new SqlCommand(
                "INSERT INTO raw.Customer (CustomerId, CustomerName, Region, SalesRepEmail) VALUES (@id, @name, @region, @rep);",
                connection))
            {
                customerCommand.Parameters.AddWithValue("@id", customer.Id);
                customerCommand.Parameters.AddWithValue("@name", customer.Name);
                customerCommand.Parameters.AddWithValue("@region", customer.Region);
                customerCommand.Parameters.AddWithValue("@rep", customer.Rep);
                await customerCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            var statusId = Statuses.First(s => s.Code == customer.Status).Id;
            int contractId;
            await using (var contractCommand = new SqlCommand(
                """
                INSERT INTO raw.Contract (CustomerId, StatusId, StartDate, EndDate, AnnualValue)
                OUTPUT INSERTED.ContractId
                VALUES (@customerId, @statusId, @startDate, @endDate, @annualValue);
                """,
                connection))
            {
                contractCommand.Parameters.AddWithValue("@customerId", customer.Id);
                contractCommand.Parameters.AddWithValue("@statusId", statusId);
                contractCommand.Parameters.AddWithValue("@startDate", DateTime.UtcNow.AddYears(-2).Date);
                contractCommand.Parameters.AddWithValue("@endDate", DateTime.UtcNow.AddYears(1).Date);
                contractCommand.Parameters.AddWithValue("@annualValue", customer.AnnualValue);
                contractId = (int)(await contractCommand.ExecuteScalarAsync(cancellationToken))!;
            }

            await using var lineCommand = new SqlCommand(
                """
                INSERT INTO raw.ContractLine (ContractId, ProductCode, Description, Quantity, UnitPrice)
                VALUES (@contractId, @productCode, @description, @quantity, @unitPrice);
                """,
                connection);
            lineCommand.Parameters.AddWithValue("@contractId", contractId);
            lineCommand.Parameters.AddWithValue("@productCode", "CORE-PLATFORM");
            lineCommand.Parameters.AddWithValue("@description", "Core platform subscription");
            lineCommand.Parameters.AddWithValue("@quantity", 1);
            lineCommand.Parameters.AddWithValue("@unitPrice", customer.AnnualValue);
            await lineCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
