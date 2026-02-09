using Support.Application.Models;

namespace Support.Application.Interfaces;

public interface IPolicySearchService
{
    Task<List<PolicyCitation>> SearchAsync(string query, int topK = 5, CancellationToken cancellationToken = default);
    Task ReindexPolicyAsync(Guid policyDocumentId, CancellationToken cancellationToken = default);
}
