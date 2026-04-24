using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FuckingGreatAdvice.Models;
using FuckingGreatAdvice.Services;

namespace FuckingGreatAdvice;

public partial class AdviceOverlayWindow
{
    /// <summary>Закрыть все открытые оверлеи советов перед показом нового.</summary>
    public static void CloseAllOpen()
    {
        var app = System.Windows.Application.Current;
        if (app == null)
            return;
        var toClose = new List<AdviceOverlayWindow>();
        foreach (Window w in app.Windows)
        {
            if (w is AdviceOverlayWindow o)
                toClose.Add(o);
        }

        foreach (var o in toClose)
            o.CloseInstant();
    }

    /// <summary>Сдвиг оверлея вверх относительно расчётной позиции (логические px WPF).</summary>
    private const double VerticalOffsetUpPx = 15;

    /// <summary>Сдвиг оверлея влево относительно расчётной позиции (логические px WPF).</summary>
    private const double HorizontalOffsetLeftPx = 20;

    private readonly AppSettings _settings;
    private readonly bool _openedFromTray;
    private DateTime? _hoverFadeNotBeforeUtc;
    private bool _fadeOutStarted;

    public AdviceOverlayWindow(string adviceText, AppSettings settings, bool openedFromTray = false)
    {
        InitializeComponent();
        _settings = settings;
        _openedFromTray = openedFromTray;
        var h = AdviceSettings.Default;

        TipText.Text = FormatAdviceText(adviceText, h);
        TipText.FontFamily = AdviceFontResolver.Resolve(h.FontFamily);
        TipText.FontWeight = FontWeights.Bold;
        TipText.FontSize = h.FontSizePx;
        TipText.Foreground = ParseBrush(h.ForegroundHex);
        TipText.TextAlignment = ParseTextAlignment(h.TextHorizontalAlignment);

        Opacity = 0;
        MouseEnter += OnMouseEnter;
        RootBorder.MouseEnter += OnMouseEnter;
        TipText.MouseEnter += OnMouseEnter;
        PreviewMouseLeftButtonDown += (_, _) => CloseInstant();
        MouseRightButtonDown += OnMouseRightButtonDown;

        Loaded += (_, _) =>
        {
            PositionOnPrimaryScreen(h);
            BeginFadeIn(h);
            if (_openedFromTray)
            {
                var cooldown = Math.Max(0, AdviceSettings.Default.TrayOpenHoverFadeCooldownMs);
                _hoverFadeNotBeforeUtc = DateTime.UtcNow.AddMilliseconds(cooldown);
            }
        };
    }

    private static string FormatAdviceText(string adviceText, AdviceSettings h)
    {
        if (h.DisplayUppercase == false)
            return adviceText;
        return adviceText.ToUpperInvariant();
    }

    private static System.Windows.Media.Brush ParseBrush(string hex)
    {
        try
        {
            var o = System.Windows.Media.ColorConverter.ConvertFromString(hex.Trim());
            if (o is System.Windows.Media.Color c)
                return new SolidColorBrush(c);
        }
        catch
        {
            // ignored
        }

        return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD3, 0xD3, 0xD3));
    }

    private static TextAlignment ParseTextAlignment(string? s)
    {
        return s?.Trim().ToLowerInvariant() switch
        {
            "left" => TextAlignment.Left,
            "center" => TextAlignment.Center,
            _ => TextAlignment.Right
        };
    }

    /// <summary>
    /// Границы основного монитора в логических единицах WPF (как <see cref="Window.Left"/> / <see cref="Window.Top"/>).
    /// Берём полный прямоугольник экрана, включая область панели задач — отступы считаются от физического края.
    /// </summary>
    private bool TryGetPrimaryScreenLogicalRect(out Rect screen)
    {
        screen = default;
        if (Screen.PrimaryScreen == null)
            return false;

        var b = Screen.PrimaryScreen.Bounds;
        var source = PresentationSource.FromVisual(this);
        var m = source?.CompositionTarget?.TransformFromDevice;
        if (m != null)
        {
            var tl = m.Value.Transform(new System.Windows.Point(b.Left, b.Top));
            var br = m.Value.Transform(new System.Windows.Point(b.Right, b.Bottom));
            screen = new Rect(tl, br);
            return true;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        screen = new Rect(
            b.Left / dpi.DpiScaleX,
            b.Top / dpi.DpiScaleY,
            b.Width / dpi.DpiScaleX,
            b.Height / dpi.DpiScaleY);
        return true;
    }

    private void PositionOnPrimaryScreen(AdviceSettings h)
    {
        UpdateLayout();
        var aw = ActualWidth;
        var ah = ActualHeight;
        if (aw <= 0 || ah <= 0)
            return;

        if (!TryGetPrimaryScreenLogicalRect(out var screen))
            screen = SystemParameters.WorkArea;

        var corner = (h.ScreenCorner ?? "BottomRight").Trim();
        switch (corner.ToLowerInvariant())
        {
            case "bottomleft":
                Left = screen.Left + h.MarginLeft;
                Top = screen.Bottom - h.MarginBottom - ah;
                break;
            case "topright":
                Left = screen.Right - h.MarginRight - aw;
                Top = screen.Top + h.MarginTop;
                break;
            case "topleft":
                Left = screen.Left + h.MarginLeft;
                Top = screen.Top + h.MarginTop;
                break;
            case "bottomcenter":
                Left = screen.Left + (screen.Width - aw) / 2;
                Top = screen.Bottom - h.MarginBottom - ah;
                break;
            case "topcenter":
                Left = screen.Left + (screen.Width - aw) / 2;
                Top = screen.Top + h.MarginTop;
                break;
            default:
                Left = screen.Right - h.MarginRight - aw;
                Top = screen.Bottom - h.MarginBottom - ah;
                break;
        }

        Top -= VerticalOffsetUpPx;
        Left -= HorizontalOffsetLeftPx;
    }

    private void BeginFadeIn(AdviceSettings h)
    {
        var ms = Math.Max(0, h.FadeInDurationMs);
        if (ms == 0)
        {
            Opacity = 1;
            return;
        }

        var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, anim);
    }

    private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_fadeOutStarted)
            return;
        if (_hoverFadeNotBeforeUtc is { } notBefore && DateTime.UtcNow < notBefore)
            return;

        _fadeOutStarted = true;
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;

        var h = AdviceSettings.Default;
        var ms = Math.Max(100, h.HoverFadeOutDurationMs);
        var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        anim.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, anim);
    }

    private void CloseInstant()
    {
        BeginAnimation(OpacityProperty, null);
        Close();
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        SettingsWindow.ShowSingletonOrActivate(dlg =>
        {
            dlg.LoadSettings(SettingsStorage.Load());
            dlg.Owner = null;
            dlg.CenterOnWorkAreaAfterLoad = true;
            dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        });
        // Не закрываем оверлей и не используем ShowDialog — настройки не должны блокировать совет.
    }
}
