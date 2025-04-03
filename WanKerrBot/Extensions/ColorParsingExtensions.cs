using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.PixelFormats;

namespace WamBot.Extensions;

// loosely based on csscolorparser.rs

internal static class ColorParsingExtensions
{
    private const double DEG2RAD = 180 / Math.PI;

    private static Lazy<Dictionary<string, Color>> NamedColors
        = new(() =>
        {
            var colours = typeof(Color).GetFields(BindingFlags.Public | BindingFlags.Static)
                .ToDictionary(m => m.Name.ToLowerInvariant(), m => (Color)m.GetValue(null));

            colours.TryAdd("kitty", Color.Parse("#ff00ff"));

            return colours;
        });

    public static bool TryParseColor(this string s, out Color color)
    {
        s = s.Trim().ToLowerInvariant();

        if (s == "transparent")
        {
            color = Color.Transparent;
            return true;
        }

        if (NamedColors.Value.TryGetValue(s, out color))
            return true;

        if (s.StartsWith('#'))
        {
            return TryParseHex(s, out color);
        }

        var idx = s.IndexOf('(');
        var idx2 = s.IndexOf(')');

        if (idx != -1 && idx2 != -1 && idx < idx2)
        {
            var prefix = s[0..idx];
            var els = s[(idx + 1)..idx2].Split(',', '/');

            switch (prefix)
            {
                case var _ when (prefix == "rgb" || prefix == "rgba") && (els.Length == 3 || els.Length == 4):
                    return TryParseRgb(els, out color);
                case var _ when (prefix == "hsl" || prefix == "hsla") && (els.Length == 3 || els.Length == 4):
                    return TryParseHsl(els, out color);
                case var _ when (prefix == "hsv" || prefix == "hsva") && (els.Length == 3 || els.Length == 4):
                    return TryParseHsv(els, out color);
                case var _ when (prefix == "cmyk") && (els.Length == 4):
                    return TryParseCmyk(els, out color);
            }
        }

        return TryParseHex(s, out color);
    }

    private static bool TryParseCmyk(string[] els, out Color color)
    {
        double c, m, y, k;
        if (TryParsePercentOrFloat(els[0], out c) &&
            TryParsePercentOrFloat(els[1], out m) &&
            TryParsePercentOrFloat(els[2], out y) &&
            TryParsePercentOrFloat(els[3], out k))
        {
            var hsl = new Cmyk((float)c, (float)m, (float)y, (float)k);
            var rgb = ColorSpaceConverter.ToRgb(hsl);

            color = new Color((Rgba32)rgb);
            return true;
        }

        color = Color.Transparent;
        return false;
    }

    private static bool TryParseHsv(string[] els, out Color color)
    {
        double h, s, v, a = 1.0;
        if (TryParseAngle(els[0], out h) &&
            TryParsePercentOrFloat(els[1], out s) &&
            TryParsePercentOrFloat(els[2], out v) &&
            (els.Length == 3 || TryParsePercentOrFloat(els[3], out a)))
        {
            var hsl = new Hsv((float)h, (float)s, (float)v);
            var rgb = ColorSpaceConverter.ToRgb(hsl);

            color = new Color((Rgba32)rgb).WithAlpha((float)a);
            return true;
        }

        color = Color.Transparent;
        return false;
    }

    private static bool TryParseHsl(string[] els, out Color color)
    {
        double h, s, l, a = 1.0;
        if (TryParseAngle(els[0], out h) &&
            TryParsePercentOrFloat(els[1], out s) &&
            TryParsePercentOrFloat(els[2], out l) &&
            (els.Length == 3 || TryParsePercentOrFloat(els[3], out a)))
        {
            var hsl = new Hsl((float)h, (float)s, (float)l);
            var rgb = ColorSpaceConverter.ToRgb(hsl);

            color = new Color((Rgba32)rgb).WithAlpha((float)a);
            return true;
        }

        color = Color.Transparent;
        return false;
    }

    private static bool TryParseRgb(string[] els, out Color color)
    {
        double r, g, b, a = 1.0;
        if (TryParsePercentOrByte(els[0], out r) &&
            TryParsePercentOrByte(els[1], out g) &&
            TryParsePercentOrByte(els[2], out b) &&
            (els.Length == 3 || TryParsePercentOrFloat(els[3], out a)))
        {
            var rgb = new Rgb((float)r, (float)g, (float)b);
            color = new Color((Rgba32)rgb).WithAlpha((float)a);
            return true;
        }

        color = Color.Transparent;
        return false;
    }

    private static bool TryParseHex(this string s, out Color color)
    {
        color = Color.Transparent;
        s = s.TrimStart('#');

        var len = s.Length;
        int r, g, b, a = 255;
        if (len == 3 || len == 4)
        {
            if (!TryParseHex(s[0..1], out r) ||
                !TryParseHex(s[1..2], out g) ||
                !TryParseHex(s[2..3], out b) ||
                (len == 4 && !TryParseHex(s[3..4], out a)))
            {
                return false;
            }

            r = r << 4 | r;
            g = g << 4 | g;
            b = b << 4 | b;
            if (len == 4)
                a = a << 4 | a;
        }
        else if (len == 6 || len == 8)
        {
            if (!TryParseHex(s[0..2], out r) ||
                !TryParseHex(s[2..4], out g) ||
                !TryParseHex(s[4..6], out b) ||
                (len == 8 && !TryParseHex(s[6..8], out a)))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        color = new Color(new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, a / 255.0f));

        return true;
    }

    private static bool TryParseAngle(string s, out double deg)
    {
        deg = 0;

        switch (s)
        {
            case var degrees when s.EndsWith("deg"):
                return TryParse(s[..^3], out deg);
            case var radians when s.EndsWith("rad"):
                return TryParse(s[..^3], out var rad) && (deg = (rad * DEG2RAD)) != double.NaN;
            case var radians when s.EndsWith("grad"):
                return TryParse(s[..^4], out var grad) && (deg = (grad * (360.0 / 400.0))) != double.NaN;
            case var radians when s.EndsWith("turn"):
                return TryParse(s[..^4], out var turn) && (deg = (turn * (360.0))) != double.NaN;
            default: return false;
        }
    }

    private static bool TryParsePercentOrFloat(string s, out double percent)
    {
        percent = 0;

        if (s.EndsWith('%'))
            return TryParse(s[..^1], out double tmp) && (percent = tmp / 100) != double.NaN;
        else
            return TryParse(s, out percent);
    }

    private static bool TryParsePercentOrByte(string s, out double percent)
    {
        percent = 0;

        if (s.EndsWith('%'))
            return TryParse(s[..^1], out double tmp) && (percent = tmp / 100) != double.NaN;
        else
            return TryParse(s, out double tmp) && (percent = tmp / 255) != double.NaN;
    }

    private static bool TryParseHex(string s, out int x)
    {
        return int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out x);
    }

    private static bool TryParse(string s, out double x)
    {
        return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out x);
    }
}
