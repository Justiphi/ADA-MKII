using ADA_MKII_Core.Contracts;

namespace ADA_MKII_Core.Abstractions;

/// <summary>
/// The one entry point for a conversational turn: history, prompting, the model
/// call, persistence and cost control all happen behind this. Composed only on
/// the server - clients reach it over HTTP.
/// </summary>
public interface IAssistantPipeline
{
    IAsyncEnumerable<ChatStreamEvent> RunAsync(ChatRequest request, CancellationToken cancellationToken);
}
