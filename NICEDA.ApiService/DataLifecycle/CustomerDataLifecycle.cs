using Microsoft.Azure.Cosmos;
using Microsoft.Data.SqlClient;
using NICEDA.ApiService.Crm;
using NICEDA.ApiService.Legal;
using NICEDA.ApiService.Rag;
using NICEDA.ApiService.Feedback;

namespace NICEDA.ApiService.DataLifecycle;

public sealed class CustomerDataLifecycle(
    SqlConnection connection, CosmosClient cosmos, CrmRepository crm,
    LegalRepository legal, KnowledgeSearchService knowledge, FeedbackService feedback,
    ILogger<CustomerDataLifecycle> logger)
{
    // NICEDA-PRACTICE-6: invalidate derivative data first, then delete stores.
    // A durable SQL marker prevents restart reseeding. Cross-store failures
    // propagate: the caller must retry, never report partial deletion as done.
    public async Task DeleteAsync(string customerId, CancellationToken cancellationToken)
        => await DeleteAsync(customerId, null, cancellationToken);

    internal async Task DeleteAsync(
        string customerId,
        Func<CancellationToken, Task>? afterSqlCommitAsync,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        if (customerId.Length > 50)
            throw new ArgumentException("CustomerId cannot exceed 50 characters.", nameof(customerId));
        customerId = customerId.Trim().ToLowerInvariant();
        await knowledge.InvalidateCustomerAsync(customerId, cancellationToken);
        crm.RemoveCustomer(customerId);
        legal.RemoveCustomer(customerId);
        await CustomerDeletionCommand.DeleteCustomerDataAsync(
            connection, cosmos, customerId, afterSqlCommitAsync, cancellationToken);
        await feedback.DeleteCustomerAsync(customerId, cancellationToken);
        logger.LogInformation("Deleted customer {CustomerId} from source stores and derived data", customerId);
    }

    public Task ReconcilePendingDeletionsAsync(CancellationToken cancellationToken = default) =>
        ReconcileMarkedCustomersAsync(
            token => CustomerDeletionCommand.GetDeletedCustomerIdsAsync(connection, token),
            DeleteAsync,
            cancellationToken);

    internal Task ReconcilePendingDeletionsAsync(
        Func<CancellationToken, Task>? afterSqlCommitAsync,
        CancellationToken cancellationToken = default) =>
        ReconcileMarkedCustomersAsync(
            token => CustomerDeletionCommand.GetDeletedCustomerIdsAsync(connection, token),
            (customerId, token) => DeleteAsync(customerId, afterSqlCommitAsync, token),
            cancellationToken);

    internal static async Task ReconcileMarkedCustomersAsync(
        Func<CancellationToken, Task<IReadOnlyList<string>>> readMarkedCustomerIdsAsync,
        Func<string, CancellationToken, Task> deleteCustomerAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(readMarkedCustomerIdsAsync);
        ArgumentNullException.ThrowIfNull(deleteCustomerAsync);

        var customerIds = await readMarkedCustomerIdsAsync(cancellationToken);
        foreach (var customerId in customerIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await deleteCustomerAsync(customerId, cancellationToken);
        }
    }
}
