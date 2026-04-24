using System.Threading;
using System.Windows;
using System.Windows.Threading;
using FuckingGreatAdvice.Models;
using FuckingGreatAdvice.Services;

namespace FuckingGreatAdvice;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private EventWaitHandle? _activateEvent;
    private RegisteredWaitHandle? _activateWaitRegistration;
    private TrayService? _tray;

    /// <summary>Истинно при «Выход» из трея — чтобы не отменять закрытие в OnClosing.</summary>
    internal static bool ShutdownRequested { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        var activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "FuckingGreatAdvice_Activate");

        _mutex = new Mutex(true, "FuckingGreatAdvice_SingleInstance_Mutex", out var createdNew);
        if (!createdNew)
        {
            activateEvent.Set();
            activateEvent.Dispose();
            Shutdown();
            return;
        }

        _activateEvent = activateEvent;
        _activateWaitRegistration = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent,
            (_, timedOut) =>
            {
                if (timedOut)
                    return;
                Dispatcher.BeginInvoke(OpenSettingsFromSecondInstance, DispatcherPriority.Normal);
            },
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);

        var startupSettings = SettingsStorage.Load();
        AutostartService.SetEnabled(startupSettings.AutostartEnabled);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);

        ThemeService.ApplyDarkOnly();
        LocalizationService.Apply(AppLanguageCatalog.Normalize(startupSettings.UiLanguage));

        MainWindow = null;

        _tray = new TrayService();
        LocalizationService.LanguageChanged += OnLocalizationLanguageChanged;

        Dispatcher.BeginInvoke(static () =>
        {
            AdviceService.TryScheduleStartupTip();
            AdvicePeriodicTimer.ApplyFromSettings();
        }, DispatcherPriority.ApplicationIdle);
    }

    private static void OpenSettingsFromSecondInstance()
    {
        SettingsWindow.ShowSingletonOrActivate(dlg =>
        {
            dlg.LoadSettings(SettingsStorage.Load());
            dlg.Owner = null;
            dlg.CenterOnWorkAreaAfterLoad = true;
            dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        });
    }

    private void OnLocalizationLanguageChanged()
    {
        _tray?.RefreshTexts();
        AdvicePeriodicTimer.ApplyFromSettings();
    }

    internal void RefreshTrayMenu() => _tray?.RefreshTexts();

    internal static void NotifyTrayAdviceFetchFailed()
    {
        if (Current is App app)
            app._tray?.ShowAdviceFetchFailedBriefly();
    }

    internal static void EnsureTrayIconNormal()
    {
        if (Current is App app)
            app._tray?.EnsureNormalTrayIcon();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_activateWaitRegistration != null && _activateEvent != null)
        {
            _activateWaitRegistration.Unregister(_activateEvent);
            _activateWaitRegistration = null;
        }

        _activateEvent?.Dispose();
        _activateEvent = null;

        LocalizationService.LanguageChanged -= OnLocalizationLanguageChanged;
        AdvicePeriodicTimer.Stop();
        _tray?.Dispose();
        try
        {
            _mutex?.ReleaseMutex();
        }
        catch
        {
            // ignored
        }

        _mutex?.Dispose();
        base.OnExit(e);
    }

    /// <summary>Если askOnExit == true — показать вопрос про автозапуск. Вызывать ДО Shutdown().</summary>
    internal static void TryShowAutostartPromptOnFirstExit()
    {
        try
        {
            var s = SettingsStorage.Load();
            if (!s.AskOnExit)
                return;

            var dlg = new AutostartPromptWindow();
            dlg.ShowDialog();
        }
        catch
        {
            // не мешаем завершению процесса
        }
    }
}
