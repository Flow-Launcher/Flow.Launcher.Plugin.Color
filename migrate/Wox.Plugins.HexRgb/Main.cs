using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using Wox.Plugin;

namespace Wox.Plugins.HexRgb
{
    public class Main : IPlugin
    {
        private static readonly string ResultIconPath = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData), "Wox", "Plugins", "plugin", "images");

        private readonly Regex _rgbRegex = new Regex(@"(\d{1,3}),\s?(\d{1,3}),\s?(\d{1,3})");
        private string _resultIcon;

        public List<Result> Query(Query query)
        {
            var raw = query.Search;
            var isRgb = _rgbRegex.IsMatch(raw);
            if ((!raw.StartsWith("#") || (raw.Length < 4)) && !isRgb)
                return new List<Result>();
            return !isRgb ? HexToRgb(raw) : RgbToHex(raw);
        }

        public void Init(PluginInitContext context)
        {
            Clear();
        }

        private List<Result> HexToRgb(string hex)
        {
            var color = ColorTranslator.FromHtml(hex);
            var colorString = $"rgb({color.R}, {color.G}, {color.B})";
            CreateIcon(color);
            return new List<Result>
            {
                new Result
                {
                    Title = colorString,
                    IcoPath = Path.Combine("images", _resultIcon),
                    Action = ctx =>
                    {
                        Clipboard.SetText(colorString);
                        return true;
                    }
                }
            };
        }

        private List<Result> RgbToHex(string rgb)
        {
            foreach (Match match in Regex.Matches(rgb, _rgbRegex.ToString(), RegexOptions.IgnoreCase))
            {
                var r = int.Parse(match.Groups[1].Value);
                var g = int.Parse(match.Groups[2].Value);
                var b = int.Parse(match.Groups[3].Value);
                var c = Color.FromArgb(255, r, g, b);
                var color = ColorTranslator.ToHtml(c);
                CreateIcon(c);

                return new List<Result>
                {
                    new Result
                    {
                        Title = color,
                        IcoPath = Path.Combine("images", _resultIcon),
                        Action = ctx =>
                        {
                            Clipboard.SetText(color);
                            return true;
                        }
                    }
                };
            }
            return new List<Result>();
        }

        private void CreateIcon(Color c)
        {
            _resultIcon = $"resultIcon-{Guid.NewGuid()}.png";
            var path = Path.Combine(ResultIconPath, _resultIcon);
            using (var b = new Bitmap(48, 48))
            {
                using (var g = Graphics.FromImage(b))
                {
                    g.Clear(c);
                }
                b.Save(path, ImageFormat.Png);
            }
        }

        private void RemoveIcons()
        {
            var files = Directory.GetFiles(ResultIconPath, "*.png");
            foreach (var t in files)
                File.Delete(t);
        }

        private void Clear()
        {
            if (!Directory.Exists(ResultIconPath))
                Directory.CreateDirectory(ResultIconPath);
            else RemoveIcons();
        }
    }
}