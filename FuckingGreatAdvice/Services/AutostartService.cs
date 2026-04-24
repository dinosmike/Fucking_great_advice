using Microsoft.Win32;

namespace FuckingGreatAdvice.Services;

public static class AutostartService
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "FuckingGreatAdvice";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            var v = key?.GetValue(ValueName) as string;
            return !string.IsNullOrEmpty(v);
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)
                           ?? Registry.CurrentUser.CreateSubKey(RunKey);

            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe))
                    return;
                key?.SetValue(ValueName, $"\"{exe}\"", RegistryValueKind.String);
            }
            else
            {
                try
                {
                    key?.DeleteValue(ValueName, false);
                }
                catch
                {
                    // ignored
                }
            }
        }
        catch
        {
            // игнорируем отказ реестра
        }
    }
}
