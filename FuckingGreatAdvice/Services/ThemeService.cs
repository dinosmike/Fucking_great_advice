using System.Windows;

namespace FuckingGreatAdvice.Services;

public static class ThemeService
{
    public static void ApplyDarkOnly()
    {
        var rd = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Themes/Dark.xaml", UriKind.Absolute)
        };
        var merged = System.Windows.Application.Current.Resources.MergedDictionaries;
        if (merged.Count > 0)
            merged[0] = rd;
        else
            merged.Add(rd);
    }
}
