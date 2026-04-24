using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace FuckingGreatAdvice.Services;

/// <summary>Иконка приложения из встроенного <c>app.ico</c> (тот же файл, что <see cref="ApplicationIcon"/> в csproj).</summary>
public static class AppIconFactory
{
    private const string PackUri = "pack://application:,,,/app.ico";

    public static Icon CreateApplicationIcon() => LoadEmbeddedIconClone();

    public static Icon CreateTrayIcon() => LoadEmbeddedIconClone();

    /// <summary>Иконка «нет связи» для трея из <c>Assets/offline.png</c> (EmbeddedResource или <c>offline.png</c> рядом с exe).</summary>
    public static Icon CreateTrayDisconnectedIconClone() => LoadOfflinePngAsTrayIcon();

    private static Icon LoadOfflinePngAsTrayIcon()
    {
        var bytes = ReadOfflinePngBytes();
        using var ms = new MemoryStream(bytes);
        using var src = new Bitmap(ms);
        var d = Math.Clamp(SystemInformation.SmallIconSize.Width, 16, 32);
        using var tray = new Bitmap(d, d, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(tray))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, new Rectangle(0, 0, d, d));
        }

        var hIcon = tray.GetHicon();
        using var tmp = Icon.FromHandle(hIcon);
        return (Icon)tmp.Clone();
    }

    private static byte[] ReadOfflinePngBytes()
    {
        var asm = Assembly.GetExecutingAssembly();
        var resName = Array.Find(
            asm.GetManifestResourceNames(),
            n => n.EndsWith("offline.png", StringComparison.OrdinalIgnoreCase));
        if (resName is not null)
        {
            using var s = asm.GetManifestResourceStream(resName)!;
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }

        var path = Path.Combine(AppContext.BaseDirectory, "offline.png");
        if (File.Exists(path))
            return File.ReadAllBytes(path);

        throw new InvalidOperationException("Не найден offline.png (ни в сборке, ни рядом с exe).");
    }

    private static Icon LoadEmbeddedIconClone()
    {
        using var ms = CopyPackResourceToMemoryStream(PackUri, "app.ico");
        using var icon = new Icon(ms);
        return (Icon)icon.Clone();
    }

    private static MemoryStream CopyPackResourceToMemoryStream(string packUri, string labelForErrors)
    {
        var streamInfo = System.Windows.Application.GetResourceStream(new Uri(packUri, UriKind.Absolute));
        if (streamInfo?.Stream == null)
            throw new InvalidOperationException($"Embedded {labelForErrors} not found.");

        var ms = new MemoryStream();
        using (streamInfo.Stream)
            streamInfo.Stream.CopyTo(ms);
        ms.Position = 0;
        return ms;
    }

    /// <summary>Экспорт встроенного <c>app.ico</c> в файл (например для инструментов).</summary>
    public static void SaveApplicationIconFile(string path)
    {
        var streamInfo = System.Windows.Application.GetResourceStream(new Uri(PackUri, UriKind.Absolute));
        if (streamInfo?.Stream == null)
            throw new InvalidOperationException("Embedded app.ico not found.");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var fs = File.Create(path);
        using (streamInfo.Stream)
        {
            streamInfo.Stream.CopyTo(fs);
        }
    }
}
