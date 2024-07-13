using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace Flow.Launcher.Plugin.Color
{
    public sealed class ColorsPlugin : IPlugin, IPluginI18n
    {
        private const int IMG_SIZE = 32;

        private DirectoryInfo ColorsDirectory { get; set; }

        private PluginInitContext context;

        private readonly string colorCodeCleanRegex = @"(\s+|\(|\))";

        public void Init(PluginInitContext context)
        {
            this.context = context;

            var imageCacheDirectoryPath = Path.Combine(context.CurrentPluginMetadata.PluginDirectory, "CachedImages");

            if (!Directory.Exists(imageCacheDirectoryPath))
            {
                ColorsDirectory = Directory.CreateDirectory(imageCacheDirectoryPath);
            }
            else
            {
                ColorsDirectory = new DirectoryInfo(imageCacheDirectoryPath);
            }
        }

        public List<Result> Query(Query query)
        {
            var search = query.Search;

            if (string.IsNullOrEmpty(search))
                return new List<Result> {
                    new Result
                    {
                        Title = context.API.GetTranslation("flowlauncher_plugin_color_plugin_multi_code_format"),
                        SubTitle = context.API.GetTranslation("flowlauncher_plugin_color_plugin_multi_code_format_suggestion"),
                        IcoPath = context.CurrentPluginMetadata.IcoPath,
                        Action = _ =>
                        {
                            Clipboard.SetDataObject("99,197,34;(39,0,152)");
                            return true;
                        }
                    }
                };

            var rawList = search.Split(';');

            var results = new List<Result>();

            foreach (var raw in rawList)
            {

                // Standardise format since rgb code typed in could be 99,197,34 or (39,0,152)
                var colorCode = Regex.Replace(raw, colorCodeCleanRegex, "");

                var rgbRegex = new Regex(@"^(\d{1,3}),\s?(\d{1,3}),\s?(\d{1,3})$");
                var hexRegex = new Regex(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$");
                var isRgb = rgbRegex.IsMatch(colorCode);
                var isHex = hexRegex.IsMatch(colorCode);

                if (!isRgb && !isHex)
                    return new List<Result>();

                if (isRgb)
                {
                    var rgbValues = colorCode.Split(',').Select(int.Parse).ToList();
                    if (rgbValues.Any(val => val > 255))
                        return new List<Result>();
                }

                try
                {
                    var createdColorImagePath = string.Empty;

                    var cached = FindFileImage(colorCode);
                    if (cached == null)
                    {
                        createdColorImagePath = CreateCacheImage(colorCode);

                        results.Add(
                            new Result
                            {
                                Title = isRgb ? string.Format("({0})", colorCode) : colorCode,
                                SubTitle = isRgb ? "RGB" : "HEX",
                                IcoPath = createdColorImagePath,
                                Action = _ =>
                                {
                                    Clipboard.SetDataObject(colorCode);
                                    return true;
                                }
                            });

                    }
                    else
                    {
                        createdColorImagePath = cached.FullName;

                        results.Add(
                            new Result
                            {
                                Title = isRgb ? string.Format("({0})", colorCode) : colorCode,
                                SubTitle = isRgb ? "RGB" : "HEX",
                                IcoPath = createdColorImagePath,
                                Action = _ =>
                                {
                                    Clipboard.SetDataObject(colorCode);
                                    return true;
                                }
                            });
                    }

                    // Reverse conversion
                    var conversion = isRgb ? RgbToHex(colorCode, rgbRegex) : HexToRgb(colorCode);

                    results.Add(
                        new Result
                        {
                            Title = conversion,
                            SubTitle = isRgb ? "HEX" : "RGB",
                            IcoPath = createdColorImagePath,
                            Action = _ =>
                            {
                                Clipboard.SetDataObject(conversion);
                                return true;
                            }
                        });
                }
                catch (Exception e)
                {
                    context.API.ShowMsgError(context.API.GetTranslation("flowlauncher_plugin_color_plugin_name"),
                        context.API.GetTranslation("flowlauncher_plugin_color_plugin_conversion_error"));

                    context.API.LogException("Query", "Colors plugin failed to convert user's input", e);

                    return new List<Result>();
                }
            }

            return results;
        }

        public FileInfo FindFileImage(string name)
        {
            var file = string.Format("{0}.png", name);

            // shouldnt have multiple files of the same name
            return ColorsDirectory.GetFiles(file, SearchOption.TopDirectoryOnly).FirstOrDefault();
        }

        private string CreateCacheImage(string name)
        {
            using (var bitmap = new Bitmap(IMG_SIZE, IMG_SIZE))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                var colorCode = name;
                var rgbRegex = new Regex(@"^(\d{1,3}),\s?(\d{1,3}),\s?(\d{1,3})$");
                var isRgb = rgbRegex.IsMatch(colorCode);
                if (isRgb)
                {
                    colorCode = RgbToHex(colorCode, rgbRegex); // Convert RGB to hex
                }
                var color = ColorTranslator.FromHtml(colorCode);
                graphics.Clear(color);


                var path = Path.Combine(ColorsDirectory.FullName, name + ".png");
                bitmap.Save(path, ImageFormat.Png);
                return path;
            }
        }

        private string HexToRgb(string hex)
        {
            var color = ColorTranslator.FromHtml(hex);
            return $"({color.R},{color.G},{color.B})";
        }

        private string RgbToHex(string rgb, Regex rgbRegex)
        {
            var color = new List<string>();
            // match multiple RGB input?
            foreach (Match match in Regex.Matches(rgb, rgbRegex.ToString(), RegexOptions.IgnoreCase))
            {
                var r = int.Parse(match.Groups[1].Value);
                var g = int.Parse(match.Groups[2].Value);
                var b = int.Parse(match.Groups[3].Value);
                var c = System.Drawing.Color.FromArgb(255, r, g, b);
                color.Add(ColorTranslator.ToHtml(c));
            }

            return color.First();
        }

        public string GetTranslatedPluginTitle()
        {
            return context.API.GetTranslation("flowlauncher_plugin_color_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return context.API.GetTranslation("flowlauncher_plugin_color_plugin_description");
        }
    }
}