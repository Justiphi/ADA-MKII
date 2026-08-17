namespace ADA_MKII_Core;

/// <summary>
/// Every route string, consumed by BOTH ADA-MKII-Server (endpoint mapping) and
/// the HTTP client in this assembly (URL building). Having one definition is
/// what stops the two from drifting apart.
/// </summary>
public static class ApiRoutes
{
    public const string Health = "/health";

    public static class Conversations
    {
        public const string Base = "/api/conversations";

        public static string ById(Guid id) => $"{Base}/{id}";

        public static string Messages(Guid id) => $"{Base}/{id}/messages";
    }

    public static class Settings
    {
        public const string Base = "/api/settings";

        public static string ByKey(string key) => $"{Base}/{Uri.EscapeDataString(key)}";
    }
}
