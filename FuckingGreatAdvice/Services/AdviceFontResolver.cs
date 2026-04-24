using System.Linq;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfFonts = System.Windows.Media.Fonts;

namespace FuckingGreatAdvice.Services;

/// <summary>
/// Резолвер шрифта окна совета: встроенный Helvetica LT Pro UltraCompressed (OTF, имя в файле — UltCompressed),
/// при необходимости — Roboto Condensed (старые настройки), иначе системные шрифты.
/// </summary>
public static class AdviceFontResolver
{
    /// <summary>Имя семейства внутри <c>Helvetica LT Pro UltraCompressed.otf</c> (как в pack URI <c>#…</c>).</summary>
    private const string EmbeddedHelveticaLtProUri = "./Fonts/#Helvetica LT Pro UltCompressed";

    /// <summary>Значение по умолчанию в настройках (человекочитаемое имя продукта).</summary>
    private const string EmbeddedHelveticaLtProDefaultLabel = "Helvetica LT Pro UltraCompressed";

    private const string EmbeddedRobotoUri = "./Fonts/#Roboto Condensed";
    private const string EmbeddedRobotoFamilyName = "Roboto Condensed";

    private static readonly Uri PackRoot = new("pack://application:,,,/");

    public static WpfFontFamily Resolve(string? preferredFromSettings)
    {
        var preferred = string.IsNullOrWhiteSpace(preferredFromSettings)
            ? EmbeddedHelveticaLtProDefaultLabel
            : preferredFromSettings.Trim();

        if (IsEmbeddedHelveticaLtPro(preferred))
            return new WpfFontFamily(PackRoot, EmbeddedHelveticaLtProUri);

        if (IsRobotoCondensed(preferred))
            return new WpfFontFamily(PackRoot, EmbeddedRobotoUri);

        foreach (var family in WpfFonts.SystemFontFamilies)
        {
            if (string.Equals(family.Source, preferred, StringComparison.OrdinalIgnoreCase))
                return family;
            if (family.FamilyNames.Values.Any(v => string.Equals(v, preferred, StringComparison.OrdinalIgnoreCase)))
                return family;
        }

        try
        {
            return new WpfFontFamily(preferred);
        }
        catch
        {
            return new WpfFontFamily(PackRoot, EmbeddedHelveticaLtProUri);
        }
    }

    private static bool IsEmbeddedHelveticaLtPro(string name)
    {
        var n = name.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        // старые сохранённые настройки
        if (n.Contains("helveticainserat"))
            return true;
        if (n.Contains("helveticaltpro") && (n.Contains("ultracompressed") || n.Contains("ultcompressed")))
            return true;
        if (string.Equals(name, "Helvetica LT Pro UltCompressed", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static bool IsRobotoCondensed(string name)
    {
        var n = name.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return n.Contains("robotocondensed");
    }
}
