using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfImage = System.Windows.Controls.Image;

namespace FuckingGreatAdvice.Services;

/// <summary>Иконка заголовка из <c>app.ico</c> (как в окне «О программе»).</summary>
public static class AppTitleBarIcon
{
    public static void ApplyPackIco(WpfImage target)
    {
        ArgumentNullException.ThrowIfNull(target);
        try
        {
            var uri = new Uri("pack://application:,,,/app.ico");
            var decoder = BitmapDecoder.Create(
                uri,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            BitmapFrame? best = null;
            foreach (BitmapFrame frame in decoder.Frames)
            {
                if (best == null || frame.PixelWidth > best.PixelWidth)
                    best = frame;
            }

            if (best == null)
                return;

            if (best.CanFreeze)
                best.Freeze();

            target.Source = best;
            RenderOptions.SetBitmapScalingMode(target, BitmapScalingMode.Fant);
        }
        catch
        {
            // оставляем пустую иконку при ошибке декодирования
        }
    }
}
