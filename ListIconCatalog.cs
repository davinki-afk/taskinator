using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Taskinator
{
    public class ListIconOption
    {
        private readonly string caption;
        private readonly TextId? captionKey;

        public ListIconOption(string key, string caption, Image image)
        {
            Key = key;
            this.caption = caption;
            Image = image;
        }

        internal ListIconOption(string key, TextId captionKey, Image image)
            : this(key, String.Empty, image)
        {
            this.captionKey = captionKey;
        }

        public string Key { get; private set; }
        public string Caption
        {
            get { return captionKey.HasValue ? L10n.Get(captionKey.Value) : caption; }
        }
        public Image Image { get; private set; }
    }

    public static class ListIconCatalog
    {
        public const string DefaultIconKey = "001.png";
        private static IList<ListIconOption> options;
        private static readonly IDictionary<string, Image> images = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private static readonly Lazy<Bitmap> fallback = new Lazy<Bitmap>(SystemIcons.Application.ToBitmap);

        internal static bool IsValidKey(string key)
        {
            return String.IsNullOrEmpty(key) ||
                (key.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
                 key.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
        }

        public static IList<ListIconOption> GetOptions()
        {
            if (options != null)
            {
                return options;
            }

            List<ListIconOption> loadedOptions = new List<ListIconOption>();
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons");
            if (Directory.Exists(folder))
            {
                string[] files = Directory.GetFiles(folder, "*.png");
                System.Array.Sort(files, System.StringComparer.OrdinalIgnoreCase);
                foreach (string file in files)
                {
                    string key = Path.GetFileName(file);
                    loadedOptions.Add(new ListIconOption(key, String.Empty, LoadImage(key, file)));
                }
            }

            if (loadedOptions.Count == 0)
            {
                loadedOptions.Add(new ListIconOption(DefaultIconKey, TextId.Default, fallback.Value));
            }

            options = loadedOptions;
            return options;
        }

        public static Image GetImage(string key)
        {
            if (String.IsNullOrEmpty(key))
            {
                key = DefaultIconKey;
            }

            Image image;
            if (images.TryGetValue(key, out image))
            {
                return image;
            }

            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", key);
            if (File.Exists(file))
            {
                return LoadImage(key, file);
            }

            // Keep the fallback independent from GetOptions so the first dialog
            // does not have to load the complete icon catalog.
            return fallback.Value;
        }

        private static Image LoadImage(string key, string file)
        {
            Image image;
            if (!images.TryGetValue(key, out image))
            {
                image = new Bitmap(file);
                images[key] = image;
            }
            return image;
        }
    }
}
