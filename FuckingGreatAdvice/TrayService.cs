using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using FuckingGreatAdvice.Services;
using Application = System.Windows.Application;

namespace FuckingGreatAdvice;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _normalTrayIcon;
    private DispatcherTimer? _restoreTrayIconTimer;
    private bool _disposed;

    /// <summary>После двойного клика иногда приходят один-два лишних MouseClick(Clicks=1); пропускаем их, не блокируя реальные клики по таймеру.</summary>
    private int _ignoreLeftTrayClicksRemaining;

    public TrayService()
    {
        _normalTrayIcon = AppIconFactory.CreateTrayIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = (Icon)_normalTrayIcon.Clone(),
            Text = LocalizationService.T("Common.AppTitle"),
            Visible = true
        };
        _notifyIcon.MouseClick += OnNotifyIconMouseClick;
        _notifyIcon.MouseDoubleClick += OnNotifyIconMouseDoubleClick;

        _notifyIcon.ContextMenuStrip = BuildContextMenu();
    }

    public void RefreshTexts()
    {
        _notifyIcon.Text = LocalizationService.T("Common.AppTitle");
        var oldMenu = _notifyIcon.ContextMenuStrip;
        _notifyIcon.ContextMenuStrip = BuildContextMenu();
        oldMenu?.Dispose();
    }

    /// <summary>Красная иконка «нет связи» на ~1 с, затем обычная (ошибка API / сеть / пустой ответ).</summary>
    internal void ShowAdviceFetchFailedBriefly()
    {
        if (_disposed)
            return;

        void Run()
        {
            if (_disposed)
                return;

            _restoreTrayIconTimer?.Stop();
            _restoreTrayIconTimer = null;

            try
            {
                // NotifyIcon сам Dispose предыдущей Icon; не клонируем — один владелец.
                _notifyIcon.Icon = AppIconFactory.CreateTrayDisconnectedIconClone();
                // Оболочка иногда не перерисовывает иконку без «подталкивания».
                var vis = _notifyIcon.Visible;
                _notifyIcon.Visible = false;
                _notifyIcon.Visible = vis;
            }
            catch
            {
                return;
            }

            _restoreTrayIconTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _restoreTrayIconTimer.Tick += (_, _) =>
            {
                if (_disposed)
                    return;
                _restoreTrayIconTimer?.Stop();
                _restoreTrayIconTimer = null;
                try
                {
                    _notifyIcon.Icon = (Icon)_normalTrayIcon.Clone();
                }
                catch
                {
                    // ignored
                }
            };
            _restoreTrayIconTimer.Start();
        }

        var disp = Application.Current?.Dispatcher;
        if (disp == null || disp.HasShutdownStarted)
            return;
        if (disp.CheckAccess())
            Run();
        else
            disp.BeginInvoke(DispatcherPriority.Normal, Run);
    }

    /// <summary>Сброс «ошибочной» иконки трея (успешный совет, отмена, настройки).</summary>
    internal void EnsureNormalTrayIcon()
    {
        if (_disposed)
            return;

        void Run()
        {
            if (_disposed)
                return;
            _restoreTrayIconTimer?.Stop();
            _restoreTrayIconTimer = null;
            try
            {
                _notifyIcon.Icon = (Icon)_normalTrayIcon.Clone();
            }
            catch
            {
                // ignored
            }
        }

        var disp = Application.Current?.Dispatcher;
        if (disp == null || disp.HasShutdownStarted)
            return;
        if (disp.CheckAccess())
            Run();
        else
            disp.BeginInvoke(DispatcherPriority.Normal, Run);
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            ShowCheckMargin = false
        };
        menu.Items.Add(CreateTrayMenuHeader(menu));
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(LocalizationService.T("Tray.MenuSettings"), null, (_, _) => ShowSettings());
        menu.Items.Add(LocalizationService.T("Tray.MenuRequestAdvice"), null, (_, _) => RequestAdvice());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(LocalizationService.T("Tray.MenuExit"), null, (_, _) => ExitApp());
        return menu;
    }

    private static ToolStripItem CreateTrayMenuHeader(ContextMenuStrip menu)
    {
        var text = LocalizationService.T("Tray.MenuHeader");
        var lbl = new TrayMenuHeaderLabel
        {
            Text = text,
            AutoSize = true,
            ForeColor = System.Drawing.Color.Black,
            BackColor = System.Drawing.Color.Transparent,
            TabStop = false,
            Cursor = Cursors.Default,
            Padding = new Padding(0, 4, 8, 2),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new System.Drawing.Font(
                System.Drawing.SystemFonts.MenuFont?.FontFamily ?? new System.Drawing.FontFamily("Segoe UI"),
                6f,
                System.Drawing.FontStyle.Bold,
                System.Drawing.GraphicsUnit.Point)
        };

        lbl.Click += (_, _) =>
        {
            menu.Close();
            InvokeOnUi(static () => About.Show(null));
        };

        return new ToolStripControlHost(lbl)
        {
            AutoSize = true,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
    }

    /// <summary>Заголовок меню: не в Tab-цепочке; клик — «О программе».</summary>
    private sealed class TrayMenuHeaderLabel : Label
    {
        public TrayMenuHeaderLabel()
        {
            SetStyle(ControlStyles.Selectable, false);
        }
    }

    private void OnNotifyIconMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        if (_ignoreLeftTrayClicksRemaining > 0)
        {
            _ignoreLeftTrayClicksRemaining--;
            return;
        }

        if (e.Clicks >= 2)
        {
            ArmIgnoreGhostLeftClicksAfterDoubleGesture();
            InvokeOnUi(OnTrayDoubleClickOpenSettings);
            return;
        }

        InvokeOnUi(RequestAdvice);
    }

    private void OnNotifyIconMouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;
        ArmIgnoreGhostLeftClicksAfterDoubleGesture();
        InvokeOnUi(OnTrayDoubleClickOpenSettings);
    }

    private void ArmIgnoreGhostLeftClicksAfterDoubleGesture()
    {
        _ignoreLeftTrayClicksRemaining = 2;
    }

    private static void OnTrayDoubleClickOpenSettings()
    {
        AdviceService.CancelInFlightTrayAdviceFetch();
        AdviceOverlayWindow.CloseAllOpen();
        ShowSettings();
    }

    private static void InvokeOnUi(Action action)
    {
        var disp = Application.Current?.Dispatcher;
        if (disp == null || disp.HasShutdownStarted)
            return;
        if (disp.CheckAccess())
            action();
        else
            disp.BeginInvoke(DispatcherPriority.Input, action);
    }

    private static void RequestAdvice() => AdviceService.RequestAdviceFromTray();

    private static void ShowSettings()
    {
        SettingsWindow.ShowSingletonOrActivate(dlg =>
        {
            dlg.LoadSettings(SettingsStorage.Load());
            dlg.Owner = null;
            dlg.CenterOnWorkAreaAfterLoad = true;
            dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        });
    }

    private static void ExitApp()
    {
        App.TryShowAutostartPromptOnFirstExit();
        App.ShutdownRequested = true;
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _restoreTrayIconTimer?.Stop();
        _restoreTrayIconTimer = null;
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
        _normalTrayIcon.Dispose();
    }
}
