using Microsoft.Azure.Cosmos;

namespace NICEDA.ApiService.Feedback;

public sealed class CosmosFeedbackStore(CosmosClient cosmosClient) : IFeedbackStore
{
    public const string DatabaseId = "niceda";
    public const string ContainerId = "feedback";

    public async Task SaveAsync(FeedbackRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        var container = await GetContainerAsync(cancellationToken);
        await container.CreateItemAsync(
            record,
            new PartitionKey(record.Id),
            cancellationToken: cancellationToken);
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default) =>
        _ = await GetContainerAsync(cancellationToken);

    public async Task DeleteCustomerAsync(string customerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        var container = await GetContainerAsync(cancellationToken);
        using var iterator = container.GetItemQueryIterator<FeedbackId>(
            new QueryDefinition("SELECT c.id FROM c WHERE c.customerId = @customerId")
                .WithParameter("@customerId", customerId));
        var ids = new List<string>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            ids.AddRange(page.Select(item => item.Id));
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

    private sealed record FeedbackId(string Id);
}
