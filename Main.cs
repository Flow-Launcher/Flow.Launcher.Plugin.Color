using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.ColorsPlugin;

public partial class ColorsPlugin : IPlugin, IPluginI18n
{
    const int ImageSize = 32;
    const char Separator = ';';

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    DirectoryInfo _cacheDir;
    PluginInitContext _context;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    [GeneratedRegex(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$")]
    private static partial Regex HexRegex();


    [GeneratedRegex(@"^(?:(?:rgb)?(?:\s+|\s*\())?(\d{1,3}),\s?(\d{1,3}),\s?(\d{1,3})\)?$", RegexOptions.IgnoreCase)]
    private static partial Regex RgbRegex();


    [GeneratedRegex(@"^(?:(?:vec3)?(?:\s+|\s*\())?(\d*\.?\d+)\s*,\s*(\d*\.?\d+)\s*,\s*(\d*\.?\d+)\)?$", RegexOptions.IgnoreCase)]
    private static partial Regex Vec3Regex();


    [GeneratedRegex(@"^(?:(?:hsl)?(?:\s+|\s*\())?(\d*\.?\d+)\s*,\s*(\d*\.?\d+)%\s*,\s*(\d*\.?\d+)%\)?$", RegexOptions.IgnoreCase)]
    private static partial Regex HslRegex();

    static string ToString(Color color)
    {
        return $"{color.R}, {color.G}, {color.B}";
    }

    static string ToVec3String(Color color)
    {
        return string.Format("{0}, {1}, {2}",
            (color.R / 255f).ToString("0.0###", CultureInfo.InvariantCulture),
            (color.G / 255f).ToString("0.0###", CultureInfo.InvariantCulture),
            (color.B / 255f).ToString("0.0###", CultureInfo.InvariantCulture));
    }

    static string ToHslString(Color color)
    {
        float h = color.GetHue();
        float s = color.GetSaturation() * 100;
        float l = color.GetBrightness() * 100;

        return string.Format("{0:F1}, {1:F1}%, {2:F1}%",
            h.ToString("0.#", CultureInfo.InvariantCulture),
            s.ToString("0.#", CultureInfo.InvariantCulture),
            l.ToString("0.#", CultureInfo.InvariantCulture));
    }

    // C# port of https://gist.github.com/mjackson/5311256
    static float HueToRgb(float p, float q, float t)
    {
        if (t < 0) t += 1f;
        if (t > 1) t -= 1f;
        if (t < 1f / 6f ) return p + (q - p) * 6f * t;
        if (t < 1f / 2f) return q;
        if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
        return p;
    }
    static Color ToColor(float h, float s, float l)
    {
        h /= 360f;
        s /= 100f;
        l /= 100f;
        float r, g, b;

        if (s == 0)
        {
            r = g = b = l; // achromatic
        }
        else
        {
            float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
            float p = 2 * l - q;
            r = HueToRgb(p, q, h + 1f / 3f);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1f / 3f);
        }

        return Color.FromArgb(255, (int)MathF.Round(r * 255f),
                                   (int)MathF.Round(g * 255f),
                                   (int)MathF.Round(b * 255f));
    }

    static string GetImageFileName(Color color)
    {
        return ColorTranslator.ToHtml(color) + ".png";
    }

    string? GetCachedImagePath(Color color)
    {
        return _cacheDir.GetFiles(GetImageFileName(color), SearchOption.TopDirectoryOnly).FirstOrDefault()?.FullName;
    }

    string CreateImageCache(Color color)
    {
        using Bitmap bitmap = new(ImageSize, ImageSize);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);

        string path = Path.Combine(_cacheDir.FullName, GetImageFileName(color));
        bitmap.Save(path, ImageFormat.Png);

        return path;
    }

    string GetColorImagePath(Color color)
    {
        if (GetCachedImagePath(color) is string cachePath)
        {
            return cachePath;
        }

        return CreateImageCache(color);
    }

    bool IsGlobalQuery()
    {
        return _context.CurrentPluginMetadata.ActionKeyword == "*";
    }

    public void Init(PluginInitContext context)
    {
        _context = context;

        string path = Path.Combine(_context.CurrentPluginMetadata.PluginDirectory, "cache");
        if (!Directory.Exists(path))
        {
            _cacheDir = Directory.CreateDirectory(path);
        }
        else
        {
            _cacheDir = new DirectoryInfo(path);
        }

        foreach (FileInfo file in _cacheDir.EnumerateFiles())
        {
            file.Delete();
        }
    }

    public List<Result>? Query(Query query)
    {
        if (string.IsNullOrEmpty(query.Search))
        {
            return IsGlobalQuery() ? null : new List<Result>(1)
            {
                new()
                {
                    Title = _context.API.GetTranslation("flowlauncher_plugin_color_plugin_multi_code_format"),
                    SubTitle = _context.API.GetTranslation("flowlauncher_plugin_color_plugin_multi_code_format_suggestion"),
                    IcoPath = _context.CurrentPluginMetadata.IcoPath,
                    Action = _ =>
                    {
                        _context.API.CopyToClipboard("99,197,34;(39,0,152)");
                        return true;
                    }
                }
            };
        }

        string[] inputs = query.Search.Split(Separator, StringSplitOptions.TrimEntries);
        List<Result> results = new(inputs.Length * 4);
        foreach (string input in inputs)
        {
            Color color = Color.Black;
            bool isSet = false;

            {
                if (HexRegex().IsMatch(input))
                {
                    color = ColorTranslator.FromHtml(input);
                    isSet = true;
                }
            }

            if (!isSet)
            {
                Match rgbMatch = RgbRegex().Match(input);
                if (rgbMatch.Success)
                {
                    byte r = byte.Parse(rgbMatch.Groups[1].Value);
                    byte g = byte.Parse(rgbMatch.Groups[2].Value);
                    byte b = byte.Parse(rgbMatch.Groups[3].Value);
                    color = Color.FromArgb(255, r, g, b);
                    isSet = true;
                }
            }

            if (!isSet)
            {
                Match vec3Match = Vec3Regex().Match(input);
                if (vec3Match.Success)
                {
                    float r = float.Parse(vec3Match.Groups[1].Value, CultureInfo.InvariantCulture);
                    float g = float.Parse(vec3Match.Groups[2].Value, CultureInfo.InvariantCulture);
                    float b = float.Parse(vec3Match.Groups[3].Value, CultureInfo.InvariantCulture);
                    if (r <= 1 && g <= 1 && b <= 1)
                    {
                        color = Color.FromArgb(255, (int)MathF.Round(r * 255f),
                                                    (int)MathF.Round(g * 255f),
                                                    (int)MathF.Round(b * 255f));

                        isSet = true;
                    }
                }
            }

            if (!isSet)
            {
                Match hslMatch = HslRegex().Match(input);
                if (hslMatch.Success)
                {
                    float h = float.Parse(hslMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                    float s = float.Parse(hslMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                    float l = float.Parse(hslMatch.Groups[3].Value, CultureInfo.InvariantCulture);
                    if (h <= 360 && s <= 100 && l <= 100)
                    {
                        color = ToColor(h, s, l);
                        isSet = true;
                    }
                }
            }

            if (!isSet)
            {
                return IsGlobalQuery() ? null : new List<Result>(1)
                {
                    new()
                    {
                        Title = _context.API.GetTranslation("flowlauncher_plugin_color_plugin_name"),
                        SubTitle = _context.API.GetTranslation("flowlauncher_plugin_color_plugin_conversion_error"),
                        IcoPath = _context.CurrentPluginMetadata.IcoPath,
                    }
                };
            }

            string imgPath = GetColorImagePath(color);
            string hexStr = ColorTranslator.ToHtml(color);
            results.Add(
                new()
                {
                    Title = hexStr,
                    SubTitle = "hex",
                    IcoPath = imgPath,
                    Action = _ =>
                    {
                        _context.API.CopyToClipboard(hexStr);
                        return true;
                    }
                }
            );

            string rgbStr = ToString(color);
            results.Add(
                new()
                {
                    Title = rgbStr,
                    SubTitle = "rgb",
                    IcoPath = imgPath,
                    Action = _ =>
                    {
                        _context.API.CopyToClipboard(rgbStr);
                        return true;
                    }
                }
            );

            string vec3Str = ToVec3String(color);
            results.Add(
                new()
                {
                    Title = vec3Str,
                    SubTitle = "vec3",
                    IcoPath = imgPath,
                    Action = _ =>
                    {
                        _context.API.CopyToClipboard(vec3Str);
                        return true;
                    }
                }
            );

            string hslStr = ToHslString(color);
            results.Add(
                new()
                {
                    Title = hslStr,
                    SubTitle = "hsl",
                    IcoPath = imgPath,
                    Action = _ =>
                    {
                        _context.API.CopyToClipboard(hslStr);
                        return true;
                    }
                }
            );
        }

        return results;
    }

    public string GetTranslatedPluginTitle()
    {
        return _context.API.GetTranslation("flowlauncher_plugin_color_plugin_name");
    }

    public string GetTranslatedPluginDescription()
    {
        return _context.API.GetTranslation("flowlauncher_plugin_color_plugin_description");
    }
}
