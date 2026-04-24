using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FuckingGreatAdvice.Models;

namespace FuckingGreatAdvice.Services;

public static class SettingsStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private const string SettingsFileName = "FuckingGreatAdvice_settings.json";
    private const string SettingsFileHeaderLine = "Fucking Great Advice — settings file";

    /// <summary>%LocalAppData%\FuckingGreatAdvice — каталог настроек пользователя (Windows).</summary>
    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FuckingGreatAdvice");

    public static string SettingsPath => Path.Combine(SettingsDirectory, SettingsFileName);

    /// <summary>Загрузка настроек; при отсутствии или повреждении файла — значения по умолчанию и запись файла.</summary>
    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                var fresh = AppSettings.CreateDefault();
                Save(fresh);
                return fresh;
            }

            var raw = File.ReadAllText(SettingsPath);
            var json = ExtractJsonFromSettingsFile(raw);
            var sanitized = StripTrailingCommas(json);
            AppSettings loaded;
            try
            {
                loaded = JsonSerializer.Deserialize<AppSettings>(sanitized, JsonOptions) ?? AppSettings.CreateDefault();
            }
            catch
            {
                BackupCorruptedFile();
                loaded = AppSettings.CreateDefault();
            }

            Normalize(loaded);
            return loaded;
        }
        catch
        {
            BackupCorruptedFile();
            var fallback = AppSettings.CreateDefault();
            try
            {
                Save(fallback);
            }
            catch
            {
                // ignored
            }

            return fallback;
        }
    }

    private static void BackupCorruptedFile()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return;
            var backupPath = SettingsPath + ".bak";
            File.Copy(SettingsPath, backupPath, overwrite: true);
        }
        catch
        {
            // best effort
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        Normalize(settings);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, SettingsFileHeaderLine + Environment.NewLine + json);
    }

    private static void Normalize(AppSettings s)
    {
        s.StartupAndWindow ??= new StartupWindowSettings();
        s.Ui ??= new UiAppearanceSettings();
        s.Advice ??= new AdviceSectionSettings();

        s.UiLanguage = AppLanguageCatalog.Normalize(s.UiLanguage);
        s.Advice.TimerIntervalMinutes = Math.Clamp(s.Advice.TimerIntervalMinutes, 1, 999);
    }

    private static string StripTrailingCommas(string json) =>
        Regex.Replace(json, @",\s*([}\]])", "$1");

    /// <summary>Первая строка файла — подпись; далее JSON. Файл только JSON — тоже поддерживается.</summary>
    private static string ExtractJsonFromSettingsFile(string raw)
    {
        var trimmed = raw.TrimStart();
        if (trimmed.StartsWith('{'))
            return trimmed;

        var lines = raw.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        if (lines.Length < 2)
            return trimmed;

        return string.Join(Environment.NewLine, lines.Skip(1)).TrimStart();
    }
}
