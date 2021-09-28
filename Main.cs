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

            var isRgb = new Regex(@"(\d{1,3}),\s?(\d{1,3}),\s?(\d{1,3})").IsMatch(raw);
            var isHex = raw.StartsWith("#") && raw.Length >= 4;

            if (!isRgb && !isHex)
                return new List<Result>();

            try
            {
                var cached = Find(raw);
                if (cached.Length == 0)
                {
                    var path = CreateImage(raw);
                    return new List<Result>
                    {
                        new Result
                        {
                            Title = raw,
                            IcoPath = path,
                            Action = _ =>
                            {
                                Clipboard.SetDataObject(raw);
                                return true;
                            }
                        }
                    };
                }

                return cached.Select(x => new Result
                {
                    Title = raw,
                    IcoPath = x.FullName,
                    Action = _ =>
                    {
                        Clipboard.SetDataObject(raw);
                        return true;
                    }
                }).ToList();
            }
            catch (Exception e)
            {
                context.API.ShowMsgError(context.API.GetTranslation("flowlauncher_plugin_color_plugin_name"), 
                    context.API.GetTranslation("flowlauncher_plugin_color_plugin_conversion_error"));

                context.API.LogException("Query", "Colors plugin failed to convert user's input", e);

                return new List<Result>();
            }
        }

        public FileInfo[] Find(string name)
        {
            var file = string.Format("{0}.png", name);
            return ColorsDirectory.GetFiles(file, SearchOption.TopDirectoryOnly);
        }

        private string CreateImage(string name)
        {
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