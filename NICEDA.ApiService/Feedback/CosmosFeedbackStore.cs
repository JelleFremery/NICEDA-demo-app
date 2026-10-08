using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using NICEDA.ApiService.DataLifecycle;

namespace NICEDA.ApiService.Feedback;

public sealed class CosmosFeedbackStore(
    CosmosClient cosmosClient,
    IServiceScopeFactory scopeFactory) : IFeedbackStore
{
    public const string DatabaseId = "niceda";
    public const string ContainerId = "feedback";

    public async Task SaveAsync(FeedbackRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.CustomerId is null)
        {
            await SaveToCosmosAsync(record, cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(record.CustomerId))
            throw new ArgumentException("CustomerId cannot be blank when supplied.", nameof(record));
        var normalizedRecord = record with { CustomerId = record.CustomerId.Trim().ToLowerInvariant() };

        // Keep a SQL transaction-owned customer lock until Cosmos confirms the
        // write. Deletion takes the same lock before committing its marker, so
        // either this feedback is persisted before deletion (and then cleaned
        // up), or it observes the marker and is rejected.
        await using var scope = scopeFactory.CreateAsyncScope();
        var sqlConnection = scope.ServiceProvider.GetRequiredService<SqlConnection>();
        await CustomerDeletionCommand.ExecuteCustomerWriteIfNotDeletedAsync(
            sqlConnection,
            normalizedRecord.CustomerId,
            token => SaveToCosmosAsync(normalizedRecord, token),
            cancellationToken);
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default) =>
        _ = await GetContainerAsync(cancellationToken);

    public async Task DeleteCustomerAsync(string customerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        customerId = customerId.Trim().ToLowerInvariant();
        var container = await GetContainerAsync(cancellationToken);
        using var iterator = container.GetItemQueryIterator<FeedbackId>(
            new QueryDefinition("SELECT c.id FROM c WHERE c.customerId = @customerId")
                .WithParameter("@customerId", customerId));
        var ids = new List<string>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            ids.AddRange(page.Select(item => item.Id
                ?? throw new InvalidOperationException(
                    "Cosmos returned a feedback document without the required 'id' field.")));
        }

        foreach (var id in ids)
        {
            await container.DeleteItemAsync<FeedbackRecord>(
                id,
                new PartitionKey(id),
                cancellationToken: cancellationToken);
        }
    }

    private async Task<Container> GetContainerAsync(CancellationToken cancellationToken)
    {
        var database = await cosmosClient.CreateDatabaseIfNotExistsAsync(
            DatabaseId,
            cancellationToken: cancellationToken);
        var containerResponse = await database.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(ContainerId, "/id"),
            cancellationToken: cancellationToken);

        return containerResponse.Container;
    }

    private async Task SaveToCosmosAsync(FeedbackRecord record, CancellationToken cancellationToken)
    {
        var container = await GetContainerAsync(cancellationToken);
        await container.CreateItemAsync(
            record,
            new PartitionKey(record.Id),
            cancellationToken: cancellationToken);
    }

    private sealed record FeedbackId(
        [property: JsonPropertyName("id")] string? Id);
}
