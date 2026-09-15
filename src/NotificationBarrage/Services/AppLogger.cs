using System.Text.Json;

namespace NotificationBarrage.Services;

public sealed class AppLogger
{
    private const long MaxFileBytes = 2 * 1024 * 1024;
    private readonly string _directory;
    private readonly object _sync = new();

    public AppLogger(string? rootDirectory = null)
    {
        _directory = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NotificationBarrage", "logs");
    }

    public void Info(string eventName, object? metadata = null) => Write("info", eventName, metadata);

    public void Error(string eventName, Exception exception) => Write("error", eventName, new
    {
        ExceptionType = exception.GetType().Name
    });

    private void Write(string level, string eventName, object? metadata)
    {
        try { WriteCore(level, eventName, metadata); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException) { /* Logging must not terminate the tray process. */ }
    }

    private void WriteCore(string level, string eventName, object? metadata)
    {
        var entry = new Dictionary<string, object?>
        {
            ["timestamp"] = DateTimeOffset.UtcNow,
            ["level"] = level,
            ["event"] = eventName
        };
        if (metadata is not null)
            entry["metadata"] = Sanitize(metadata);

        var line = SerializeBounded(entry);
        lock (_sync)
        {
            Directory.CreateDirectory(_directory);
            var path = Path.Combine(_directory, $"{DateTimeOffset.Now:yyyyMMdd}.log");
            if (File.Exists(path) && new FileInfo(path).Length + System.Text.Encoding.UTF8.GetByteCount(line) > MaxFileBytes)
                File.Move(path, path + $".part-{DateTimeOffset.Now:HHmmssfff}-{Guid.NewGuid():N}", true);
            File.AppendAllText(path, line);
        }
    }

    private static string SerializeBounded(Dictionary<string, object?> entry)
    {
        var line = JsonSerializer.Serialize(entry) + Environment.NewLine;
        if (System.Text.Encoding.UTF8.GetByteCount(line) <= MaxFileBytes)
            return line;

        // A single notification or metadata value must never make a log file exceed its cap.
        entry.Remove("metadata");
        var eventText = entry["event"]?.ToString() ?? "unknown";
        entry["event"] = eventText[..Math.Min(256, eventText.Length)];
        line = JsonSerializer.Serialize(entry) + Environment.NewLine;
        return System.Text.Encoding.UTF8.GetByteCount(line) <= MaxFileBytes
            ? line
            : JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow, level = "info", @event = "log-entry-too-large" }) + Environment.NewLine;
    }

    private static object? Sanitize(object value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return SanitizeElement(document.RootElement);
    }

    private static object? SanitizeElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var result = new Dictionary<string, object?>();
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Contains("body", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Contains("message", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Contains("content", StringComparison.OrdinalIgnoreCase))
                    continue;
                result[property.Name] = SanitizeElement(property.Value);
            }
            return result;
        }
        if (element.ValueKind == JsonValueKind.Array)
            return element.EnumerateArray().Select(SanitizeElement).ToArray();
        return element.ValueKind == JsonValueKind.Null ? null : element.Clone();
    }
}
