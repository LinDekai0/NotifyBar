using System.Text.Json;
using System.Text.Json.Serialization;
using NotificationBarrage.Domain;

namespace NotificationBarrage.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _directory;
    private readonly string _path;
    public string? LoadWarning { get; private set; }

    public SettingsStore(string? rootDirectory = null)
    {
        _directory = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NotificationBarrage");
        _path = Path.Combine(_directory, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_path))
            return new AppSettings().Normalize();

        try
        {
            var json = File.ReadAllText(_path);
            using var document = JsonDocument.Parse(json);
            var settings = (JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings()).Normalize();
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && !document.RootElement.TryGetProperty(nameof(AppSettings.KnownSources), out _)
                && !document.RootElement.TryGetProperty(nameof(AppSettings.EnabledSourceIds), out _))
            {
                var pending = new List<LegacyNotificationSource>();
                if (ReadLegacyBoolean(document.RootElement, "EnableQQ", true)) pending.Add(LegacyNotificationSource.QQ);
                if (ReadLegacyBoolean(document.RootElement, "EnableWeChat", true)) pending.Add(LegacyNotificationSource.WeChat);
                settings = settings with { PendingLegacySources = pending.ToArray() };
            }
            return settings.Normalize();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            LoadWarning = "设置文件损坏或无法读取，已恢复默认设置，并尝试保留原文件备份。";
            BackupBrokenFile();
            return new AppSettings().Normalize();
        }
    }

    private static bool ReadLegacyBoolean(JsonElement root, string propertyName, bool defaultValue)
    {
        if (!root.TryGetProperty(propertyName, out var value)) return defaultValue;
        return value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : defaultValue;
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(_directory);
        var temporaryPath = _path + ".tmp";
        var json = JsonSerializer.Serialize(settings.Normalize(), JsonOptions);
        using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(json);
            writer.Flush();
            stream.Flush(true);
        }

        File.Move(temporaryPath, _path, true);
    }

    private void BackupBrokenFile()
    {
        try
        {
            var backupPath = _path + $".broken-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
            File.Move(_path, backupPath);
        }
        catch (IOException)
        {
            // A failed backup must not prevent returning safe defaults.
        }
        catch (UnauthorizedAccessException)
        {
            // A failed backup must not prevent returning safe defaults.
        }
    }
}
