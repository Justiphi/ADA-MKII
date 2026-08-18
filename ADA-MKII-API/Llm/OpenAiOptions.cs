namespace ADA_MKII_API.Llm;

/// <summary>
/// Bound from "Ada:OpenAI". The API key must come from user-secrets in
/// development or an environment variable in production - never appsettings.json,
/// and never the database.
/// </summary>
public sealed class OpenAiOptions
{
    public const string SectionName = "Ada:OpenAI";

    public string? ApiKey { get; set; }

    /// <summary>
    /// The endpoint this server talks to by default. Blank means api.openai.com.
    /// Set it to run against any OpenAI-compatible backend - Ollama, Groq,
    /// OpenRouter - without touching code.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Whether a signed-in user may point ADA at a different endpoint from the
    /// settings page.
    ///
    /// Left on, because on a single-user install the account holder is also the
    /// operator. Turn it off when accounts belong to other people: the server is
    /// what makes the outbound call, so a user-supplied address is a request the
    /// server will make on their behalf to wherever they name.
    /// </summary>
    public bool AllowUserBaseUrl { get; set; } = true;
}
