using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyButtonMaker;

public static class SettingsStore
{
    /// <summary>実行ファイルと同じフォルダに保存する</summary>
    public static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "settings.json");
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettings Load()
    {
        AppSettings? settings = null;
        try
        {
            if (File.Exists(FilePath))
                settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options);
        }
        catch (Exception) { /* 壊れた設定は初期化する */ }

        settings ??= new AppSettings();
        if (!settings.Profiles.Any(p => p.IsGlobal))
            settings.Profiles.Insert(0, new Profile());
        return settings;
    }

    public static void Save(AppSettings settings)
    {
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
    }
}
