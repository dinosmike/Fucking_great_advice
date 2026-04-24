using System.IO;
using System.Windows.Media;
using System.Xml.Linq;

namespace FuckingGreatAdvice.Services;

/// <summary>Читает первый <c>&lt;path d="…"&gt;</c> из <c>exit.svg</c> в ресурсах сборки.</summary>
public static class SvgExitIconGeometry
{
    /// <summary>Must match the key in <c>g.resources</c> (MSBuild lowercases logical paths).</summary>
    private const string PackUri = "pack://application:,,,/assets/exit.svg";

    public static Geometry? TryLoadFromPack()
    {
        try
        {
            var info = System.Windows.Application.GetResourceStream(new Uri(PackUri, UriKind.Absolute));
            if (info?.Stream == null)
                return null;
            using var reader = new StreamReader(info.Stream);
            return ParseFirstPathD(reader.ReadToEnd());
        }
        catch
        {
            return null;
        }
    }

    private static Geometry? ParseFirstPathD(string svgMarkup)
    {
        try
        {
            var doc = XDocument.Parse(svgMarkup);
            XNamespace ns = "http://www.w3.org/2000/svg";
            var path = doc.Root?.Element(ns + "path") ?? doc.Root?.Element("path");
            var d = path?.Attribute("d")?.Value?.Trim();
            if (string.IsNullOrEmpty(d))
                return null;
            return Geometry.Parse(d);
        }
        catch
        {
            return null;
        }
    }
}
