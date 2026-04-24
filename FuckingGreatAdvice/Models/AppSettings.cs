using System.Text.Json.Serialization;
using FuckingGreatAdvice.Services;

namespace FuckingGreatAdvice.Models;

/// <summary>FuckingGreatAdvice_settings.json в %LocalAppData%\FuckingGreatAdvice.</summary>
public sealed class AppSettings
{
    [JsonPropertyOrder(0)]
    [JsonPropertyName("startupAndWindow")]
    public StartupWindowSettings StartupAndWindow { get; set; } = new();

    [JsonPropertyOrder(1)]
    [JsonPropertyName("ui")]
    public UiAppearanceSettings Ui { get; set; } = new();

    [JsonPropertyOrder(2)]
    [JsonPropertyName("advice")]
    public AdviceSectionSettings Advice { get; set; } = new();

    [JsonIgnore]
    public bool AutostartEnabled
    {
        get => StartupAndWindow.AutostartEnabled;
        set => StartupAndWindow.AutostartEnabled = value;
    }

    [JsonIgnore]
    public bool AskOnExit
    {
        get => StartupAndWindow.AskOnExit;
        set => StartupAndWindow.AskOnExit = value;
    }

    [JsonIgnore]
    public string? UiLanguage
    {
        get => Ui.Language;
        set => Ui.Language = value;
    }

    [JsonIgnore]
    public bool AdviceEnabled
    {
        get => Advice.Enabled;
        set => Advice.Enabled = value;
    }

    [JsonIgnore]
    public bool ShowAdviceOnStartup
    {
        get => Advice.ShowOnStartup;
        set => Advice.ShowOnStartup = value;
    }

    public static AppSettings CreateDefault() => new()
    {
        StartupAndWindow = new StartupWindowSettings
        {
            AutostartEnabled = false,
            AskOnExit = true
        },
        Ui = new UiAppearanceSettings
        {
            Language = AppLanguageCatalog.ResolveDefaultLanguage()
        },
        Advice = new AdviceSectionSettings
        {
            Enabled = true,
            ShowOnStartup = true,
            TimerEnabled = false,
            TimerIntervalMinutes = 30
        }
    };
}
