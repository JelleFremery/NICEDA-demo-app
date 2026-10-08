namespace NICEDA.ApiService.Rag;

public interface IKnowledgeDocumentSource
{
    Task<IReadOnlyList<KnowledgeDocument>> LoadAsync(string mode, CancellationToken cancellationToken = default);
}
