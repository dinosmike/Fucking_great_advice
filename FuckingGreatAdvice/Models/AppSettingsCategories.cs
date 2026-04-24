using System.Text.Json.Serialization;

namespace FuckingGreatAdvice.Models;

public sealed class StartupWindowSettings
{
    public bool AutostartEnabled { get; set; }

    [JsonPropertyName("askOnExit")]
    public bool AskOnExit { get; set; } = true;
}

public sealed class UiAppearanceSettings
{
    public string? Language { get; set; }
}

public sealed class AdviceSectionSettings
{
    /// <summary>Разрешены советы (трей, таймер, оверлей). Отключается из оверлея.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Совет при запуске приложения (независимо от таймера).</summary>
    public bool ShowOnStartup { get; set; } = true;

    /// <summary>Периодически показывать новый совет из трея.</summary>
    public bool TimerEnabled { get; set; }

    /// <summary>Интервал в минутах (после сохранения и при тике поджимается к допустимому диапазону).</summary>
    public int TimerIntervalMinutes { get; set; } = 30;
}
