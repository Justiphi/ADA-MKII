using System.Runtime.CompilerServices;
using ADA_MKII_Core.Abstractions;
using ADA_MKII_Core.Contracts;
using ADA_MKII_Core.Pipeline;
using Microsoft.Extensions.Options;

namespace ADA_MKII_API.Llm;

/// <summary>
/// Picks a backend per turn.
///
/// This exists so the choice is not frozen at startup. A user who sets an
/// endpoint on the settings page must get a real model on the very next turn,
/// even on a server that was deployed with no key at all - which is exactly the
/// case when someone points a fresh install at their own Ollama.
///
/// With nothing configured and nothing chosen, the echo provider answers. That
/// keeps a new deployment testable end to end before any credential exists.
/// </summary>
public sealed class RoutingLlmProvider(
    OpenAiLlmProvider openAi,
    EchoLlmProvider echo,
    IOptions<OpenAiOptions> options) : ILlmProvider
{
    private readonly OpenAiOptions _options = options.Value;

    public string Name => "routing";

    public IAsyncEnumerable<LlmDelta> StreamAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var haveBackend = request.Endpoint is not null
            || !string.IsNullOrWhiteSpace(_options.BaseUrl)
            || !string.IsNullOrWhiteSpace(_options.ApiKey);

        return haveBackend
            ? openAi.StreamAsync(request, cancellationToken)
            : echo.StreamAsync(request, cancellationToken);
    }
}
