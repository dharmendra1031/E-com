using System.Globalization;
using System.Text.RegularExpressions;

namespace EcommerceWebApplication.Helper;

public sealed record StoreTheme(string Primary, string Foreground, string Hover, string HoverForeground, string Link, string Tint, string Rgb)
{
    public const string DefaultColor = "#075A96";

    public static bool IsValid(string? value) =>
        value != null && Regex.IsMatch(value, @"\A#[0-9a-fA-F]{6}\z");

    public static StoreTheme From(string? value)
    {
        var primary = IsValid(value) ? value!.ToUpperInvariant() : DefaultColor;
        var r = int.Parse(primary.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var g = int.Parse(primary.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b = int.Parse(primary.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        static string Hex(int r, int g, int b) => $"#{r:X2}{g:X2}{b:X2}";
        static double Linear(int c)
        {
            double n = c / 255d;
            return n <= 0.04045 ? n / 12.92 : Math.Pow((n + 0.055) / 1.055, 2.4);
        }
        static double Luminance(int r, int g, int b) => 0.2126 * Linear(r) + 0.7152 * Linear(g) + 0.0722 * Linear(b);
        static string Text(int r, int g, int b)
        {
            double l = Luminance(r, g, b);
            return (l + 0.05) / 0.05 >= 1.05 / (l + 0.05) ? "#000000" : "#FFFFFF";
        }

        int hr = (int)(r * 0.82), hg = (int)(g * 0.82), hb = (int)(b * 0.82);
        int lr = r, lg = g, lb = b;
        int Tint(int c) => (int)(255 * 0.9 + c * 0.1);
        double surfaceLuminance = Math.Min(Luminance(245, 245, 245), Luminance(Tint(r), Tint(g), Tint(b)));
        // Link and selected navigation text must remain readable on neutral and tinted surfaces.
        while ((surfaceLuminance + 0.05) / (Luminance(lr, lg, lb) + 0.05) < 4.5)
        {
            lr = (int)(lr * 0.9); lg = (int)(lg * 0.9); lb = (int)(lb * 0.9);
        }
        return new StoreTheme(primary, Text(r, g, b), Hex(hr, hg, hb), Text(hr, hg, hb),
            Hex(lr, lg, lb), Hex(Tint(r), Tint(g), Tint(b)), $"{r},{g},{b}");
    }
}
