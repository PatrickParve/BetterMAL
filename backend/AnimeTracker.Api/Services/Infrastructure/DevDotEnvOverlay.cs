using Npgsql;

namespace AnimeTracker.Api.Services.Infrastructure;

/// <summary>A natively run backend (Development) reads the repo-root <c>.env</c>
/// that Docker Compose reads, taking each value the way Compose reads it, so
/// one file configures both run paths. Does nothing outside Development.</summary>
public static class DevDotEnvOverlay
{
    public static void Apply(IConfigurationBuilder configuration, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
            return;

        var path = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", ".env"));
        if (!File.Exists(path))
            return;

        configuration.AddInMemoryCollection(ToConfiguration(Parse(File.ReadAllLines(path))));
    }

    internal static Dictionary<string, string> Parse(IEnumerable<string> lines)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0)
                continue;

            var key = line[..separatorIndex].Trim();
            if (key.Length == 0)
                continue;

            result[key] = ParseValue(line[(separatorIndex + 1)..].Trim());
        }

        return result;
    }

    private static string ParseValue(string trimmedValue)
    {
        if (trimmedValue.Length >= 2 && (trimmedValue[0] == '"' || trimmedValue[0] == '\''))
        {
            var quote = trimmedValue[0];
            var closingIndex = trimmedValue.IndexOf(quote, 1);
            if (closingIndex > 0)
                return trimmedValue[1..closingIndex];
        }

        for (var i = 1; i < trimmedValue.Length; i++)
        {
            if (trimmedValue[i] == '#' && (trimmedValue[i - 1] == ' ' || trimmedValue[i - 1] == '\t'))
                return trimmedValue[..i].Trim();
        }

        return trimmedValue;
    }

    internal static Dictionary<string, string?> ToConfiguration(IReadOnlyDictionary<string, string> env)
    {
        var result = new Dictionary<string, string?>();

        SetIfNonEmpty(result, "Mal:ClientId", env.GetValueOrDefault("MAL_CLIENT_ID"));
        SetIfNonEmpty(result, "Mal:ClientSecret", env.GetValueOrDefault("MAL_CLIENT_SECRET"));

        var backendPort = env.GetValueOrDefault("BACKEND_PORT");
        if (!string.IsNullOrEmpty(backendPort))
        {
            var port = ParsePort(backendPort, "BACKEND_PORT");
            result["Mal:CallbackPort"] = port.ToString();
            result["urls"] = $"http://localhost:{port}";
        }

        var database = env.GetValueOrDefault("POSTGRES_DB");
        var username = env.GetValueOrDefault("POSTGRES_USER");
        var password = env.GetValueOrDefault("POSTGRES_PASSWORD");
        if (!string.IsNullOrEmpty(database) && !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            var postgresPort = env.GetValueOrDefault("POSTGRES_PORT");
            var port = string.IsNullOrEmpty(postgresPort) ? 5432 : ParsePort(postgresPort, "POSTGRES_PORT");

            result["ConnectionStrings:Default"] = new NpgsqlConnectionStringBuilder
            {
                Host = "localhost",
                Port = port,
                Database = database,
                Username = username,
                Password = password,
            }.ConnectionString;
        }

        return result;
    }

    private static void SetIfNonEmpty(Dictionary<string, string?> result, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            result[key] = value;
    }

    private static int ParsePort(string value, string key)
    {
        if (!int.TryParse(value, out var port))
            throw new InvalidOperationException($"{key} in .env must be a port number");

        return port;
    }
}
