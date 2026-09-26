using System.Text.Json;

namespace ImageAligner.Core.Settings;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _path;

    public JsonSettingsStore(string? path = null)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ImageAligner");
        Directory.CreateDirectory(dir);
        _path = path ?? Path.Combine(dir, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_path)) return new AppSettings();
        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, json);
    }
}