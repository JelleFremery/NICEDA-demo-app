using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;

namespace NICEDA.ApiService.Rag;

public sealed class CosmosKnowledgeDocumentSource(CosmosClient cosmosClient) : IKnowledgeDocumentSource
{
    // NICEDA-PRACTICE-5: policy metadata travels with the chunk for filtering
    // but is never part of the embedded text.
    private static readonly HashSet<string> ExcludedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "id",
        "customerId",
        "region",
        "classification",
        "severity",
        "concept",
        "sourceSystem",
        "ingestedAtUtc",
        "owner",
        "retention",
        "retentionDays",
        "sourceId",
        "permissions",
        "authorizedUsers",
        "securityMetadata",
    };

    public async Task<IReadOnlyList<KnowledgeDocument>> LoadAsync(
        string mode, CancellationToken cancellationToken = default)
    {
        var containerId = mode.ToLowerInvariant() switch
        {
            "before" => "raw-support",
            "after" => "optimized-support",
            _ => throw new ArgumentException("Mode must be 'before' or 'after'.", nameof(mode)),
        };

        var container = cosmosClient.GetContainer("niceda", containerId);
        using var iterator = container.GetItemQueryIterator<JsonObject>("SELECT * FROM c");
        var documents = new List<KnowledgeDocument>();

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            foreach (var item in page)
            {
                var document = ToKnowledgeDocument(item, containerId);
                if (document is not null)
                {
                    documents.Add(document);
                }
            }
        }

        return documents;
    }

    internal static KnowledgeDocument? ToKnowledgeDocument(JsonObject item, string containerId)
    {
        var id = item["id"]?.GetValue<string>();
        var customerId = item["customerId"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(customerId))
        {
            throw new InvalidOperationException(
                $"A document in '{containerId}' is missing its id or customerId.");
        }

        var content = ExtractContent(item);
        return string.IsNullOrWhiteSpace(content)
            ? null
            : new KnowledgeDocument(
                id,
                customerId,
                item["region"]?.GetValue<string>(),
                item["classification"]?.GetValue<string>(),
                item["concept"]?.GetValue<string>(),
                content,
                item["owner"]?.GetValue<string>(),
                item["retentionDays"]?.GetValue<int?>(),
                item["sourceId"]?.GetValue<string>());
    }

    internal static string ExtractContent(JsonObject document)
    {
        var text = new StringBuilder();
        AppendContent(document, text);
        return text.ToString().Trim();
    }

    private static void AppendContent(JsonNode? node, StringBuilder text, string? fieldName = null)
    {
        if (fieldName is not null
            && (fieldName.StartsWith('_')
                || ExcludedFields.Contains(fieldName)
                || fieldName.Contains("metadata", StringComparison.OrdinalIgnoreCase)
                || fieldName.Contains("classification", StringComparison.OrdinalIgnoreCase)
                || fieldName.Contains("permission", StringComparison.OrdinalIgnoreCase)
                || fieldName.Contains("authoriz", StringComparison.OrdinalIgnoreCase)
                || fieldName.StartsWith("security", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (node is JsonObject obj)
        {
            foreach (var pair in obj)
            {
                AppendContent(pair.Value, text, pair.Key);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                AppendContent(item, text);
            }
        }
        else if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var stringValue) && !string.IsNullOrWhiteSpace(stringValue))
            {
                text.Append(stringValue).Append(' ');
            }
            else if (value.TryGetValue<bool>(out var booleanValue))
            {
                text.Append(booleanValue.ToString(CultureInfo.InvariantCulture)).Append(' ');
            }
        }
    }
}
