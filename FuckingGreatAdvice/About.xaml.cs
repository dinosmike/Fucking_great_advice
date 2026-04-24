using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using WpfButton = System.Windows.Controls.Button;
using System.Windows.Navigation;
using FuckingGreatAdvice.Services;

namespace FuckingGreatAdvice;

public partial class About
{
    public About()
    {
        InitializeComponent();
    }

    private void About_OnLoaded(object sender, RoutedEventArgs e)
    {
        RunDonatePrefix.Text = LocalizationService.T("About.DonatePrefix");
        RunDonateLink.Text = LocalizationService.T("About.DonateLink");

        AppTitleBarIcon.ApplyPackIco(TitleBarIcon);
    }

    private void SocialLink_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: string url })
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void DonateLink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    public static void Show(Window? owner)
    {
        try
        {
            var w = new About();
            if (owner is { IsLoaded: true, IsVisible: true })
                w.Owner = owner;
            else
                w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            w.ShowDialog();
        }
        catch
        {
            // prevent crash if the dialog fails to open or close
        }
    }

    private void Ok_OnClick(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void TitleBarClose_OnClick(object sender, RoutedEventArgs e) => Close();
}
