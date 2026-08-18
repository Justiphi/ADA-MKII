namespace ADA_MKII_Core;

/// <summary>
/// Every route string, consumed by BOTH ADA-MKII-Server (endpoint mapping) and
/// the HTTP client in this assembly (URL building). Having one definition is
/// what stops the two from drifting apart.
/// </summary>
public static class ApiRoutes
{
    public const string Health = "/health";

    /// <summary>Streams a conversational turn as server-sent events.</summary>
    public const string Chat = "/api/chat";

    public static class Auth
    {
        /// <summary>Exchanges a username and password for a bearer token. Anonymous.</summary>
        public const string Login = "/api/auth/login";

        /// <summary>Revokes the token used to make the call.</summary>
        public const string Logout = "/api/auth/logout";

        /// <summary>Returns the account the current token belongs to.</summary>
        public const string Me = "/api/auth/me";
    }

    public static class Conversations
    {
        public const string Base = "/api/conversations";

        public static string ById(Guid id) => $"{Base}/{id}";

        public static string Messages(Guid id) => $"{Base}/{id}/messages";
    }

    public static class Notes
    {
        public const string Base = "/api/notes";

        public static string ById(Guid id) => $"{Base}/{id}";

        public static string Search(string query) => $"{Base}/search?q={Uri.EscapeDataString(query)}";
    }

    public static class Memories
    {
        public const string Base = "/api/memories";

        public static string ById(Guid id) => $"{Base}/{id}";

        public static string Search(string query) => $"{Base}/search?q={Uri.EscapeDataString(query)}";
    }

    public static class Calendar
    {
        public const string Base = "/api/calendar";

        public static string ById(Guid id) => $"{Base}/{id}";

        /// <summary>Expanded occurrences in a window - what a calendar view asks for.</summary>
        public static string Occurrences(DateTimeOffset fromUtc, DateTimeOffset toUtc) =>
            $"{Base}/occurrences?from={Uri.EscapeDataString(fromUtc.ToString("o"))}&to={Uri.EscapeDataString(toUtc.ToString("o"))}";

        public static string CancelOccurrence(Guid id) => $"{Base}/{id}/cancel";
    }

    public static class Weather
    {
        public const string Base = "/api/weather";

        /// <summary>A named place, or the account's own setting when omitted.</summary>
        public static string For(string? location) =>
            string.IsNullOrWhiteSpace(location)
                ? Base
                : $"{Base}?location={Uri.EscapeDataString(location)}";
    }

    public static class Settings
    {
        public const string Base = "/api/settings";

        public static string ByKey(string key) => $"{Base}/{Uri.EscapeDataString(key)}";
    }
}
