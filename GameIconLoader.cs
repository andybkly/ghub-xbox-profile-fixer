using System;
using System.IO;
using DrawingIcon = System.Drawing.Icon;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GHubXBOXProfileFixer
{
    internal static class GameIconLoader
    {
        public static ImageSource Load(XboxGame game)
        {
            try
            {
                foreach (string path in XboxScanner.ArtworkPaths(game))
                {
                    try
                    {
                        if (new FileInfo(path).Length > 32 * 1024 * 1024) continue;
                        // OnLoad releases the stream immediately, avoiding locks on game assets.
                        using (var stream = File.OpenRead(path))
                        {
                            var image = new BitmapImage();
                            image.BeginInit();
                            image.CacheOption = BitmapCacheOption.OnLoad;
                            image.DecodePixelWidth = 96;
                            image.StreamSource = stream;
                            image.EndInit();
                            image.Freeze();
                            return image;
                        }
                    }
                    catch { /* A damaged or inaccessible asset must not hide the game. */ }
                }
            }
            catch { /* Fall back to the executable resource. */ }
            try
            {
                // Retain the managed fallback used by the earlier working release.
                using (DrawingIcon icon = DrawingIcon.ExtractAssociatedIcon(game.ExecutablePath))
                {
                    if (icon == null) return null;
                    var image = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    image.Freeze();
                    return image;
                }
            }
            catch { return null; }
        }
    }
}
