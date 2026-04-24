using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FuckingGreatAdvice.Models;
using FuckingGreatAdvice.Services;

namespace FuckingGreatAdvice;

public partial class SettingsWindow
{
    internal const int TooltipInitialShowDelayMs = 400;

    private const int AdviceTimerMinMinutes = 1;
    private const int AdviceTimerMaxMinutes = 999;

    private AppSettings _settings = null!;
    private bool _languageComboLoading;
    private bool _adviceTimerRowLoading;
    private string _languageAtOpen = "en";

    /// <summary>Если true — после первой вёрстки центрировать по рабочей области экрана (трей, <see cref="SizeToContent"/>).</summary>
    internal bool CenterOnWorkAreaAfterLoad { get; set; }

    private static SettingsWindow? _singletonInstance;

    /// <summary>Одно немодальное окно настроек: повторный вызов активирует уже открытое (трей / оверлей совета не блокируются).</summary>
    internal static void ShowSingletonOrActivate(Action<SettingsWindow> configure)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher == null)
            return;

        void Run()
        {
            if (_singletonInstance != null)
            {
                _singletonInstance.Activate();
                if (_singletonInstance.WindowState == WindowState.Minimized)
                    _singletonInstance.WindowState = WindowState.Normal;
                _singletonInstance.Focus();
                return;
            }

            var dlg = new SettingsWindow();
            configure(dlg);
            _singletonInstance = dlg;
            dlg.Closed += (_, _) =>
            {
                if (ReferenceEquals(_singletonInstance, dlg))
                    _singletonInstance = null;
            };
            dlg.Show();
        }

        if (app.Dispatcher.CheckAccess())
            Run();
        else
            app.Dispatcher.Invoke(Run);
    }

    public SettingsWindow()
    {
        InitializeComponent();
        TooltipDelayHelper.Apply(this, TooltipInitialShowDelayMs);

        Loaded += OnLoaded;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape)
                return;
            e.Handled = true;
            RevertAndClose();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AppTitleBarIcon.ApplyPackIco(TitleBarIcon);
        // InlineUIContainer не даёт Path найти Hyperlink в визуальном дереве — Fill через Source.
        SettingsExitIconPath.SetBinding(Shape.FillProperty,
            new System.Windows.Data.Binding(nameof(Hyperlink.Foreground)) { Source = SettingsExitHyperlink });
        ApplyExitIconFromSvg();
        if (CenterOnWorkAreaAfterLoad)
            CenterOnWorkArea();
    }

    private void ApplyExitIconFromSvg()
    {
        var g = SvgExitIconGeometry.TryLoadFromPack();
        if (g == null)
            return;
        if (g.CanFreeze)
            g.Freeze();
        SettingsExitIconPath.Data = g;
    }

    private void CenterOnWorkArea()
    {
        UpdateLayout();
        var wa = SystemParameters.WorkArea;
        Left = wa.Left + (wa.Width - ActualWidth) / 2;
        Top = wa.Top + (wa.Height - ActualHeight) / 2;
    }

    public void LoadSettings(AppSettings settings)
    {
        _settings = settings;
        _languageAtOpen = AppLanguageCatalog.Normalize(settings.UiLanguage);

        _languageComboLoading = true;
        LanguageCombo.ItemsSource = AppLanguageCatalog.All;
        LanguageCombo.SelectedValue = _languageAtOpen;
        _languageComboLoading = false;

        AutostartCheck.IsChecked = settings.AutostartEnabled;
        AdviceOnStartupCheck.IsChecked = settings.Advice.ShowOnStartup;

        _adviceTimerRowLoading = true;
        AdviceTimerCheck.IsChecked = settings.Advice.TimerEnabled;
        var minutes = Math.Clamp(settings.Advice.TimerIntervalMinutes, AdviceTimerMinMinutes, AdviceTimerMaxMinutes);
        AdviceTimerMinutesText.Text = minutes.ToString(CultureInfo.InvariantCulture);
        _adviceTimerRowLoading = false;
        UpdateAdviceTimerRowVisuals();
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        _settings.AutostartEnabled = AutostartCheck.IsChecked == true;
        _settings.Advice.ShowOnStartup = AdviceOnStartupCheck.IsChecked == true;
        _settings.Advice.TimerEnabled = AdviceTimerCheck.IsChecked == true;
        _settings.Advice.TimerIntervalMinutes = ParseIntervalMinutesFromUi();
        _settings.UiLanguage = AppLanguageCatalog.Normalize(LanguageCombo.SelectedValue as string ?? _settings.UiLanguage);

        AutostartService.SetEnabled(_settings.AutostartEnabled);

        SettingsStorage.Save(_settings);
        AdvicePeriodicTimer.ApplyFromSettings();
        Close();
        // После Show() (немодально) нельзя выставлять DialogResult — он сам дергает Close и даёт реентрантность с Close().
        if (System.Windows.Application.Current is App app)
            app.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, app.RefreshTrayMenu);
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => RevertAndClose();

    private void RevertAndClose()
    {
        _settings.UiLanguage = _languageAtOpen;
        SettingsStorage.Save(_settings);
        var revertLanguage = _languageAtOpen;
        Close();
        // Apply после закрытия: обновление MergedDictionaries во время выгрузки окна давало зависание.
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            () => LocalizationService.Apply(revertLanguage));
    }

    private void LanguageCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_languageComboLoading)
            return;
        if (LanguageCombo.SelectedValue is not string code)
            return;

        LocalizationService.Apply(code);
        _settings.UiLanguage = code;
        SettingsStorage.Save(_settings);
    }

    private void AdviceTimerCheck_OnIsCheckedChanged(object sender, RoutedEventArgs e)
    {
        if (_adviceTimerRowLoading)
            return;
        UpdateAdviceTimerRowVisuals();
    }

    private void AdviceTimerCaption_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        AdviceTimerCheck.IsChecked = !(AdviceTimerCheck.IsChecked == true);

    private void UpdateAdviceTimerRowVisuals()
    {
        var on = AdviceTimerCheck.IsChecked == true;
        if (TryFindResource("Rs.Text.Muted") is not System.Windows.Media.Brush muted)
            return;
        if (TryFindResource("Rs.Text.Primary") is not System.Windows.Media.Brush primary)
            return;

        AdviceTimerPrefixText.Foreground = on ? primary : muted;
        AdviceTimerSuffixText.Foreground = on ? primary : muted;
        AdviceTimerMinutesText.Foreground = on ? primary : muted;
        AdviceTimerUp.Foreground = on ? primary : muted;
        AdviceTimerDown.Foreground = on ? primary : muted;

        AdviceTimerMinutesText.IsEnabled = on;
        AdviceTimerMinutesText.IsReadOnly = !on;
        // IsHitTestVisible вместо IsEnabled: у RepeatButton отключённое состояние даёт некрасивую заливку глифа.
        AdviceTimerUp.IsHitTestVisible = on;
        AdviceTimerDown.IsHitTestVisible = on;
        AdviceTimerUp.Focusable = on;
        AdviceTimerDown.Focusable = on;
    }

    private int ParseIntervalMinutesFromUi()
    {
        if (!int.TryParse(AdviceTimerMinutesText.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            v = _settings.Advice.TimerIntervalMinutes;
        return Math.Clamp(v, AdviceTimerMinMinutes, AdviceTimerMaxMinutes);
    }

    private void BumpTimerMinutes(int delta)
    {
        if (AdviceTimerMinutesText.IsReadOnly)
            return;
        _ = int.TryParse(AdviceTimerMinutesText.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v);
        v = Math.Clamp(v + delta, AdviceTimerMinMinutes, AdviceTimerMaxMinutes);
        AdviceTimerMinutesText.Text = v.ToString(CultureInfo.InvariantCulture);
    }

    private void AdviceTimerUp_OnClick(object sender, RoutedEventArgs e)
    {
        if (AdviceTimerCheck.IsChecked != true)
            return;
        BumpTimerMinutes(1);
    }

    private void AdviceTimerDown_OnClick(object sender, RoutedEventArgs e)
    {
        if (AdviceTimerCheck.IsChecked != true)
            return;
        BumpTimerMinutes(-1);
    }

    private void AdviceTimerMinutesText_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length == 0 || e.Text.All(char.IsDigit))
            return;
        e.Handled = true;
    }

    private void AdviceTimerMinutesText_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (AdviceTimerMinutesText.IsReadOnly)
            return;
        if (e.Key == Key.Up)
        {
            e.Handled = true;
            BumpTimerMinutes(1);
        }
        else if (e.Key == Key.Down)
        {
            e.Handled = true;
            BumpTimerMinutes(-1);
        }
    }

    private void AdviceTimerMinutesText_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (AdviceTimerMinutesText.IsReadOnly)
            return;
        var v = ParseIntervalMinutesFromUi();
        AdviceTimerMinutesText.Text = v.ToString(CultureInfo.InvariantCulture);
    }

    private void TitleBar_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void TitleBarClose_OnClick(object sender, RoutedEventArgs e) => Cancel_OnClick(sender, e);

    private void SettingsExitHyperlink_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        App.TryShowAutostartPromptOnFirstExit();
        App.ShutdownRequested = true;
        System.Windows.Application.Current.Shutdown();
    }

    private void TitleBarAbout_OnClick(object sender, RoutedEventArgs e) => About.Show(this);

    private void TitleBarAbout_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        About.Show(this);
    }
}
