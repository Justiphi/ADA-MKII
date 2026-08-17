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
}
