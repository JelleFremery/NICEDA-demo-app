using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.Cosmos;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NICEDA.ApiService.Crm;
using NICEDA.ApiService.DataLifecycle;
using NICEDA.ApiService.Feedback;
using NICEDA.ApiService.Legal;
using NICEDA.ApiService.Rag;

namespace NICEDA.Tests;

public sealed class CustomerDeletionReconciliationTests
{
    [Fact]
    public async Task ReconcileMarkedCustomers_RetriesPartialDeletionAfterRestartAndIsIdempotent()
    {
        var markedCustomerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceRowsRemain = true;
        var supportDocumentsRemain = true;
        var feedbackRemains = true;
        var mockProjectionsRemain = true;
        var vectorsRemain = true;
        var failAfterSqlOnce = true;
        var attempts = 0;
        markedCustomerIds.Add("scratch-retry");

        Task<IReadOnlyList<string>> ReadMarkersAsync(CancellationToken _) =>
            Task.FromResult<IReadOnlyList<string>>(markedCustomerIds.ToArray());

        Task DeleteCustomerAsync(string customerId, CancellationToken _)
        {
            attempts++;
            markedCustomerIds.Add(customerId);
            mockProjectionsRemain = false;
            vectorsRemain = false;
            sourceRowsRemain = false;
            if (failAfterSqlOnce)
            {
                failAfterSqlOnce = false;
                throw new InvalidOperationException("Simulated Cosmos failure after SQL committed.");
            }

            supportDocumentsRemain = false;
            feedbackRemains = false;
            return Task.CompletedTask;
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CustomerDataLifecycle.ReconcileMarkedCustomersAsync(
                ReadMarkersAsync, DeleteCustomerAsync, CancellationToken.None));

        Assert.Contains("scratch-retry", markedCustomerIds);
        Assert.False(sourceRowsRemain);
        Assert.True(supportDocumentsRemain);
        Assert.True(feedbackRemains);
        Assert.False(mockProjectionsRemain);
        Assert.False(vectorsRemain);

        // A new process reads the durable marker and resumes the same idempotent deletion.
        await CustomerDataLifecycle.ReconcileMarkedCustomersAsync(
            ReadMarkersAsync, DeleteCustomerAsync, CancellationToken.None);

        Assert.False(sourceRowsRemain);
        Assert.False(supportDocumentsRemain);
        Assert.False(feedbackRemains);
        Assert.False(mockProjectionsRemain);
        Assert.False(vectorsRemain);

        // A later restart repeats cleanup safely; no store is repopulated.
        await CustomerDataLifecycle.ReconcileMarkedCustomersAsync(
            ReadMarkersAsync, DeleteCustomerAsync, CancellationToken.None);
        Assert.Equal(3, attempts);
        Assert.False(sourceRowsRemain);
        Assert.False(supportDocumentsRemain);
        Assert.False(feedbackRemains);
        Assert.False(mockProjectionsRemain);
        Assert.False(vectorsRemain);
    }
}

[Collection(nameof(AppHostCollection))]
public class CustomerDeletionTests(AppHostFixture fixture)
{
    [DockerFact]
    public async Task ReconcileMarkedCustomer_RemovesDataFromFullLifecycleAndSurvivesRestart()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var cancellationToken = deadline.Token;
        var scratchCustomerId = $"scratch-{Guid.NewGuid():N}";
        var sqlConnectionString = await fixture.GetSqlConnectionStringAsync();
        var cosmosConnectionString = await fixture.GetCosmosConnectionStringAsync();
        Assert.NotNull(sqlConnectionString);
        Assert.NotNull(cosmosConnectionString);

        var contentRoot = CreateScratchMockData(scratchCustomerId);
        try
        {
            await using var sqlConnection = new SqlConnection(sqlConnectionString);
            await sqlConnection.OpenAsync(cancellationToken);
            using var cosmosClient = new CosmosClient(cosmosConnectionString, new CosmosClientOptions
            {
                UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions(),
                ConnectionMode = ConnectionMode.Gateway,
                RequestTimeout = TimeSpan.FromSeconds(15),
                MaxRetryAttemptsOnRateLimitedRequests = 0,
            });

            await SeedScratchCustomerAsync(sqlConnection, cosmosClient, scratchCustomerId, cancellationToken);
            using var feedbackServiceProvider = new ServiceCollection()
                .AddScoped(_ => new SqlConnection(sqlConnectionString))
                .BuildServiceProvider();
            var feedback = new FeedbackService(new CosmosFeedbackStore(
                cosmosClient, feedbackServiceProvider.GetRequiredService<IServiceScopeFactory>()));
            await feedback.RecordAsync(
                new FeedbackSubmission("admin", "after", "chat", "customer", CustomerId: scratchCustomerId),
                cancellationToken);
            Assert.Equal(1, await GetFeedbackCountAsync(cosmosClient, scratchCustomerId, cancellationToken));

            var crm = new CrmRepository(new ScratchWebHostEnvironment(contentRoot));
            var legal = new LegalRepository(new ScratchWebHostEnvironment(contentRoot));
            var knowledge = CreateKnowledgeSearch(scratchCustomerId);
            Assert.NotNull(crm.GetById(scratchCustomerId));
            Assert.Contains(legal.GetAll(), document =>
                LegalKnowledgeProjection.Project(document).CustomerId == scratchCustomerId);
            Assert.Single(await knowledge.SearchAsync("scratch evidence", "before", "admin", cancellationToken));

            var lifecycle = CreateLifecycle(
                sqlConnection, cosmosClient, crm, legal, knowledge, feedback);
            await Assert.ThrowsAsync<InvalidOperationException>(() => lifecycle.DeleteAsync(
                scratchCustomerId,
                _ => Task.FromException(new InvalidOperationException(
                    "Simulated process failure immediately after the SQL deletion transaction committed.")),
                cancellationToken));

            await AssertSqlCustomerDataRemovedAsync(sqlConnection, scratchCustomerId, cancellationToken);
            Assert.Equal(1, await GetSupportDocumentCountAsync(
                cosmosClient, "raw-support", scratchCustomerId, cancellationToken));
            Assert.Equal(1, await GetSupportDocumentCountAsync(
                cosmosClient, "optimized-support", scratchCustomerId, cancellationToken));
            Assert.Equal(1, await GetFeedbackCountAsync(cosmosClient, scratchCustomerId, cancellationToken));
            Assert.Null(crm.GetById(scratchCustomerId));
            Assert.DoesNotContain(legal.GetAll(), document =>
                LegalKnowledgeProjection.Project(document).CustomerId == scratchCustomerId);
            Assert.Empty(await knowledge.SearchAsync("scratch evidence", "before", "admin", cancellationToken));

            await Assert.ThrowsAsync<InvalidOperationException>(() => feedback.RecordAsync(
                new FeedbackSubmission("admin", "after", "chat", "customer", CustomerId: scratchCustomerId),
                cancellationToken));

            // Restart from freshly-loaded mocks and a warm vector index. The
            // durable marker drives the full lifecycle through every store.
            var restartedCrm = new CrmRepository(new ScratchWebHostEnvironment(contentRoot));
            var restartedLegal = new LegalRepository(new ScratchWebHostEnvironment(contentRoot));
            var restartedKnowledge = CreateKnowledgeSearch(scratchCustomerId);
            Assert.NotNull(restartedCrm.GetById(scratchCustomerId));
            Assert.Single(await restartedKnowledge.SearchAsync(
                "scratch evidence", "before", "admin", cancellationToken));
            var restartedLifecycle = CreateLifecycle(
                sqlConnection, cosmosClient, restartedCrm, restartedLegal,
                restartedKnowledge, feedback);
            await restartedLifecycle.ReconcilePendingDeletionsAsync(cancellationToken);

            Assert.Null(restartedCrm.GetById(scratchCustomerId));
            Assert.DoesNotContain(restartedLegal.GetAll(), document =>
                LegalKnowledgeProjection.Project(document).CustomerId == scratchCustomerId);
            Assert.Empty(await restartedKnowledge.SearchAsync(
                "scratch evidence", "before", "admin", cancellationToken));
            await AssertCustomerDataRemovedAsync(
                sqlConnection, cosmosClient, scratchCustomerId, cancellationToken);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    private static CustomerDataLifecycle CreateLifecycle(
        SqlConnection sqlConnection,
        CosmosClient cosmosClient,
        CrmRepository crm,
        LegalRepository legal,
        KnowledgeSearchService knowledge,
        FeedbackService feedback) =>
        new(sqlConnection, cosmosClient, crm, legal, knowledge, feedback,
            NullLogger<CustomerDataLifecycle>.Instance);

    private static KnowledgeSearchService CreateKnowledgeSearch(string customerId) =>
        new(new ScratchKnowledgeSource(customerId), new ScratchEmbeddingService());

    private static async Task SeedScratchCustomerAsync(
        SqlConnection sqlConnection,
        CosmosClient cosmosClient,
        string customerId,
        CancellationToken cancellationToken)
    {
        await using (var customerCommand = new SqlCommand(
            "INSERT INTO raw.Customer (CustomerId, CustomerName, Region, SalesRepEmail) VALUES (@id, @name, @region, @rep);",
            sqlConnection))
        {
            customerCommand.Parameters.AddWithValue("@id", customerId);
            customerCommand.Parameters.AddWithValue("@name", "Scratch Deletion Test Co");
            customerCommand.Parameters.AddWithValue("@region", "EU");
            customerCommand.Parameters.AddWithValue("@rep", "test@niceda.example");
            await customerCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        int contractId;
        await using (var contractCommand = new SqlCommand(
            """
            INSERT INTO raw.Contract (CustomerId, StatusId, StartDate, EndDate, AnnualValue)
            OUTPUT INSERTED.ContractId
            VALUES (@customerId, 1, @startDate, @endDate, 1000);
            """,
            sqlConnection))
        {
            contractCommand.Parameters.AddWithValue("@customerId", customerId);
            contractCommand.Parameters.AddWithValue("@startDate", DateTime.UtcNow.Date);
            contractCommand.Parameters.AddWithValue("@endDate", DateTime.UtcNow.AddYears(1).Date);
            contractId = (int)(await contractCommand.ExecuteScalarAsync(cancellationToken))!;
        }

        await using (var lineCommand = new SqlCommand(
            """
            INSERT INTO raw.ContractLine (ContractId, ProductCode, Description, Quantity, UnitPrice)
            VALUES (@contractId, 'TEST', @description, 1, 1000);
            """,
            sqlConnection))
        {
            lineCommand.Parameters.AddWithValue("@contractId", contractId);
            lineCommand.Parameters.AddWithValue("@description", $"Scratch test line {customerId}");
            await lineCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await NICEDA.ApiService.Sql.FollowUpTaskCommands.CreateFollowUpTaskAsync(sqlConnection,
            new(customerId, "Scratch deletion check",
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), "tests"), cancellationToken);

        foreach (var containerId in new[] { "raw-support", "optimized-support" })
        {
            var container = cosmosClient.GetContainer("niceda", containerId);
            var document = new System.Text.Json.Nodes.JsonObject
            {
                ["id"] = $"{containerId}-{customerId}",
                ["customerId"] = customerId,
                ["note"] = "Scratch document for deletion test.",
            };
            await container.UpsertItemAsync(
                document, new PartitionKey(customerId), cancellationToken: cancellationToken);
        }
    }

    private static async Task AssertCustomerDataRemovedAsync(
        SqlConnection sqlConnection,
        CosmosClient cosmosClient,
        string customerId,
        CancellationToken cancellationToken)
    {
        await AssertSqlCustomerDataRemovedAsync(sqlConnection, customerId, cancellationToken);

        foreach (var containerId in new[] { "raw-support", "optimized-support" })
        {
            Assert.Equal(0, await GetSupportDocumentCountAsync(
                cosmosClient, containerId, customerId, cancellationToken));
        }

        Assert.Equal(0, await GetFeedbackCountAsync(cosmosClient, customerId, cancellationToken));
    }

    private static async Task AssertSqlCustomerDataRemovedAsync(
        SqlConnection sqlConnection, string customerId, CancellationToken cancellationToken)
    {
        await using (var command = new SqlCommand(
            """
            SELECT (SELECT COUNT(*) FROM raw.Customer WHERE CustomerId=@id)
                 + (SELECT COUNT(*) FROM raw.Contract WHERE CustomerId=@id)
                 + (SELECT COUNT(*) FROM raw.ContractLine cl
                    WHERE cl.Description=@lineDescription)
                 + (SELECT COUNT(*) FROM optimized.FollowUpTask WHERE CustomerId=@id);
            """, sqlConnection))
        {
            command.Parameters.AddWithValue("@id", customerId);
            command.Parameters.AddWithValue("@lineDescription", $"Scratch test line {customerId}");
            Assert.Equal(0, (int)(await command.ExecuteScalarAsync(cancellationToken))!);
        }

        await using (var marker = new SqlCommand(
            "SELECT COUNT(*) FROM raw.DeletedCustomer WHERE CustomerId=@id;", sqlConnection))
        {
            marker.Parameters.AddWithValue("@id", customerId);
            Assert.Equal(1, (int)(await marker.ExecuteScalarAsync(cancellationToken))!);
        }
    }

    private static async Task<int> GetSupportDocumentCountAsync(
        CosmosClient cosmosClient, string containerId, string customerId, CancellationToken cancellationToken)
    {
        var container = cosmosClient.GetContainer("niceda", containerId);
        using var iterator = container.GetItemQueryIterator<int>(
            new QueryDefinition("SELECT VALUE COUNT(1) FROM c"),
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(customerId) });
        var page = await iterator.ReadNextAsync(cancellationToken);
        return page.FirstOrDefault();
    }

    private static async Task<int> GetFeedbackCountAsync(
        CosmosClient cosmosClient, string customerId, CancellationToken cancellationToken)
    {
        var container = cosmosClient.GetContainer(CosmosFeedbackStore.DatabaseId, CosmosFeedbackStore.ContainerId);
        using var iterator = container.GetItemQueryIterator<int>(
            new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.customerId = @customerId")
                .WithParameter("@customerId", customerId));
        var page = await iterator.ReadNextAsync(cancellationToken);
        return page.FirstOrDefault();
    }

    private static string CreateScratchMockData(string customerId)
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"niceda-deletion-{Guid.NewGuid():N}");
        var crmSeedPath = Path.Combine(contentRoot, "Crm", "Seed");
        var legalSeedPath = Path.Combine(contentRoot, "Legal", "Seed");
        Directory.CreateDirectory(crmSeedPath);
        Directory.CreateDirectory(legalSeedPath);

        var customer = new CrmCustomer(
            customerId, "Scratch Customer", "Test Contact", "Administrator", "test@niceda.example",
            "555-0100", "", "", "1 Test Street", "", "Testville", "00000", "US",
            "Test Manager", "Software", "EU", "Test", "2026-01-01", "2026-10-01",
            "2027-01-01", "1000", "Active", "Scratch sales note", "Scratch support note",
            "https://niceda.example", "en", "", "");
        var legalDocument = new LegalDocument(
            "scratch-legal-document", $"Customers/{customerId}/Contracts",
            "scratch-contract.pdf", 1, "2026-10-08");

        File.WriteAllText(Path.Combine(crmSeedPath, "crm-customers.json"),
            System.Text.Json.JsonSerializer.Serialize(new[] { customer },
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
        File.WriteAllText(Path.Combine(legalSeedPath, "legal-documents.json"),
            System.Text.Json.JsonSerializer.Serialize(new[] { legalDocument },
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
        return contentRoot;
    }

    private sealed class ScratchWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = nameof(CustomerDeletionTests);
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class ScratchKnowledgeSource(string customerId) : IKnowledgeDocumentSource
    {
        public Task<IReadOnlyList<KnowledgeDocument>> LoadAsync(
            string mode, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeDocument>>(
                [new("scratch-vector", customerId, "EU", "Confidential", "test", "Scratch evidence")]);
    }

    private sealed class ScratchEmbeddingService : IEmbeddingService
    {
        public bool IsAvailable => true;

        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult(new[] { 1f, 0f });
    }
}
