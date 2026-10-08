namespace NICEDA.ApiService.Rag;

public sealed record KnowledgeDocument(
    string Id,
    string CustomerId,
    string? Region,
    string? Classification,
    string? Concept,
    string Content,
    string? Owner = null,
    int? RetentionDays = null,
    string? SourceId = null);

public sealed record KnowledgeSearchResult(
    string Id,
    string CustomerId,
    string? Concept,
    string Content,
    double Score);
