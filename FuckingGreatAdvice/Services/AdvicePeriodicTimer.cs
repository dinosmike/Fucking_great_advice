using System;
using System.Windows.Threading;

namespace FuckingGreatAdvice.Services;

/// <summary>Показ совета по интервалу — только <c>advice.timerEnabled</c> (независимо от остальных галочек и <c>enabled</c>).</summary>
public static class AdvicePeriodicTimer
{
    private static DispatcherTimer? _timer;

    public static void ApplyFromSettings()
    {
        Stop();

        var s = SettingsStorage.Load();
        if (!s.Advice.TimerEnabled)
            return;

        var minutes = Math.Clamp(s.Advice.TimerIntervalMinutes, 1, 999);
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher == null)
            return;

        _timer = new DispatcherTimer(DispatcherPriority.Normal, app.Dispatcher)
        {
            Interval = TimeSpan.FromMinutes(minutes)
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public static void Stop()
    {
        if (_timer == null)
            return;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
    }

    private static void OnTick(object? sender, EventArgs e)
    {
        var s = SettingsStorage.Load();
        if (!s.Advice.TimerEnabled)
        {
            Stop();
            return;
        }

        var minutes = Math.Clamp(s.Advice.TimerIntervalMinutes, 1, 999);
        if (_timer != null)
            _timer.Interval = TimeSpan.FromMinutes(minutes);

        AdviceService.RequestAdviceFromTimer();
    }
}
