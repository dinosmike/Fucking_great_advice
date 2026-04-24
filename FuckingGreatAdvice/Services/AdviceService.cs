using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FuckingGreatAdvice;
using FuckingGreatAdvice.Models;

namespace FuckingGreatAdvice.Services;

/// <summary>Запрос совета по сети в фоне; UI не блокируется.</summary>
public static class AdviceService
{
    private static readonly object TrayAdviceCtsLock = new();
    private static CancellationTokenSource? _trayAdviceFetchCts;

    /// <summary>При старте программы — только если в настройках включены советы при запуске.</summary>
    public static void TryScheduleStartupTip()
    {
        var s = SettingsStorage.Load();
        if (!s.Advice.ShowOnStartup)
            return;

        RunAdviceFetchAndShow(openedFromTray: false, ignoreAdviceEnabled: false, CancellationToken.None);
    }

    /// <summary>По клику на иконке в трее — всегда, независимо от галочек и <c>advice.enabled</c>.</summary>
    public static void RequestAdviceFromTray()
    {
        CancellationToken trayToken;
        lock (TrayAdviceCtsLock)
        {
            _trayAdviceFetchCts?.Cancel();
            _trayAdviceFetchCts?.Dispose();
            _trayAdviceFetchCts = new CancellationTokenSource();
            trayToken = _trayAdviceFetchCts.Token;
        }

        RunAdviceFetchAndShow(openedFromTray: true, ignoreAdviceEnabled: true, trayToken);
    }

    /// <summary>По таймеру — не зависит от <c>advice.enabled</c> и от галочек автозапуска / совета при старте.</summary>
    public static void RequestAdviceFromTimer() =>
        RunAdviceFetchAndShow(openedFromTray: false, ignoreAdviceEnabled: true, CancellationToken.None);

    /// <summary>Отмена текущего запроса с трея (двойной клик по иконке). Вызывать с UI-потока перед закрытием оверлеев.</summary>
    public static void CancelInFlightTrayAdviceFetch()
    {
        lock (TrayAdviceCtsLock)
        {
            _trayAdviceFetchCts?.Cancel();
            _trayAdviceFetchCts?.Dispose();
            _trayAdviceFetchCts = null;
        }

        App.EnsureTrayIconNormal();
    }

    private static void RunAdviceFetchAndShow(bool openedFromTray, bool ignoreAdviceEnabled, CancellationToken trayOrNone)
    {
        if (!ignoreAdviceEnabled && !SettingsStorage.Load().Advice.Enabled)
            return;

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null)
            return;

        App.EnsureTrayIconNormal();

        var advice = AdviceSettings.Default;
        var totalWaitSec = Math.Clamp(advice.FetchTimeoutSeconds, 1, 600);
        // С трея: ограниченное окно ожидания, чтобы «нет сети» не ждал минуту+ из-за долгих HTTP.
        var effectiveWaitSec = openedFromTray ? Math.Clamp(totalWaitSec, 6, 18) : totalWaitSec;
        var censored = false;

        _ = Task.Run(async () =>
        {
            var deadline = DateTime.UtcNow.AddSeconds(effectiveWaitSec);
            using var totalCts = new CancellationTokenSource(TimeSpan.FromSeconds(effectiveWaitSec));

            // 0 — ждём; 1 — совет получен; 2 — уже показали «нет сети» по таймеру «не сразу»; 3 — финальная ошибка без дубля.
            var quickGate = new int[1];

            if (openedFromTray)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(900, trayOrNone).ConfigureAwait(false);
                    }
                    catch
                    {
                        return;
                    }

                    if (trayOrNone.IsCancellationRequested)
                        return;
                    if (Interlocked.CompareExchange(ref quickGate[0], 2, 0) == 0)
                        _ = dispatcher.BeginInvoke(new Action(() => App.NotifyTrayAdviceFetchFailed()));
                });
            }

            string? tipText = null;
            while (DateTime.UtcNow < deadline && !totalCts.IsCancellationRequested)
            {
                if (trayOrNone.IsCancellationRequested)
                    return;

                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    break;

                try
                {
                    using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(totalCts.Token, trayOrNone);
                    var perAttempt = TimeSpan.FromSeconds(Math.Min(10, Math.Max(3, remaining.TotalSeconds)));
                    attemptCts.CancelAfter(perAttempt);

                    var text = await GreatAdviceApiClient.FetchRandomAsync(censored, attemptCts.Token)
                        .ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        tipText = text.Trim();
                        Interlocked.Exchange(ref quickGate[0], 1);
                        break;
                    }
                }
                catch
                {
                    // нет сети / обрыв — пауза и следующая попытка, пока не кончилось окно ожидания
                }

                remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    break;

                var pause = TimeSpan.FromSeconds(1);
                if (remaining < pause)
                    pause = remaining;
                if (pause <= TimeSpan.Zero)
                    break;

                try
                {
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(totalCts.Token, trayOrNone);
                    await Task.Delay(pause, linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            if (trayOrNone.IsCancellationRequested)
                return;

            if (tipText == null)
            {
                if (!openedFromTray)
                    _ = dispatcher.BeginInvoke(new Action(() => App.NotifyTrayAdviceFetchFailed()));
                else if (Interlocked.CompareExchange(ref quickGate[0], 3, 0) == 0)
                    _ = dispatcher.BeginInvoke(new Action(() => App.NotifyTrayAdviceFetchFailed()));
                else
                    Interlocked.CompareExchange(ref quickGate[0], 3, 2);
                return;
            }

            _ = dispatcher.BeginInvoke(new Action(() =>
            {
                if (trayOrNone.IsCancellationRequested)
                    return;

                App.EnsureTrayIconNormal();
                AdviceOverlayWindow.CloseAllOpen();
                var w = new AdviceOverlayWindow(tipText, SettingsStorage.Load(), openedFromTray);
                w.Show();
            }));
        });
    }
}
