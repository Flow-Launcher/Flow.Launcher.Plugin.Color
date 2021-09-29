using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;

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
            var raw = query.Search;

            var rgbRegex = new Regex(@"(\d{1,3}),\s?(\d{1,3}),\s?(\d{1,3})");
            var hexRegex = new Regex(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$");
            var isRgb = rgbRegex.IsMatch(raw);
            var isHex = hexRegex.IsMatch(raw);

            if (!isRgb && !isHex)
                return new List<Result>();

            try
            {
                var results = new List<Result>();
                var createdColorImagePath = string.Empty;

                var cached = FindFileImage(raw);
                if (cached == null)
                {
                    createdColorImagePath = CreateCacheImage(raw);

                    results.Add(
                        new Result
                        {
                            Title = raw,
                            SubTitle = isRgb ? "RGB" : "HEX",
                            IcoPath = createdColorImagePath,
                            Action = _ =>
                            {
                                Clipboard.SetDataObject(raw);
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
                            Title = raw,
                            SubTitle = isRgb ? "RGB" : "HEX",
                            IcoPath = createdColorImagePath,
                            Action = _ =>
                            {
                                Clipboard.SetDataObject(raw);
                                return true;
                            }
                        });
                }

                // Reverse conversion
                var conversion = isRgb ? RgbToHex(raw, rgbRegex) : HexToRgb(raw);

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

                return results;

            }
            catch (Exception e)
            {
                context.API.ShowMsgError(context.API.GetTranslation("flowlauncher_plugin_color_plugin_name"), 
                    context.API.GetTranslation("flowlauncher_plugin_color_plugin_conversion_error"));

                context.API.LogException("Query", "Colors plugin failed to convert user's input", e);

                return new List<Result>();
            }
        }

        public FileInfo FindFileImage(string raw)
        {
            var name = Regex.Replace(raw, colorCodeCleanRegex, "");

            var file = string.Format("{0}.png", name);

            // shouldnt have multiple files of the same name
            return ColorsDirectory.GetFiles(file, SearchOption.TopDirectoryOnly).FirstOrDefault();
        }

        private string CreateCacheImage(string raw)
        {
            var name = Regex.Replace(raw, colorCodeCleanRegex, "");

            using (var bitmap = new Bitmap(IMG_SIZE, IMG_SIZE))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                var color = ColorTranslator.FromHtml(name);
                graphics.Clear(color);


                var path = Path.Combine(ColorsDirectory.FullName, name+".png");
                bitmap.Save(path, ImageFormat.Png);
                return path;
            }
        }

        private string HexToRgb(string hex)
        {
            var color = ColorTranslator.FromHtml(hex);
            return $"({color.R}, {color.G}, {color.B})";
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