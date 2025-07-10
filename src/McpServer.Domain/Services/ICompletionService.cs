using McpServer.Domain.Protocol.Messages;

namespace McpServer.Domain.Services;

/// <summary>
/// Service for providing completion suggestions.
/// </summary>
public interface ICompletionService
{
    /// <summary>
    /// Gets completion suggestions for a reference.
    /// </summary>
    /// <param name="reference">The reference to complete.</param>
    /// <param name="argument">The argument being completed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The completion response.</returns>
    Task<CompletionCompleteResponse> GetCompletionAsync(
        CompletionReference reference,
        CompletionArgument argument,
        CancellationToken cancellationToken = default);
}