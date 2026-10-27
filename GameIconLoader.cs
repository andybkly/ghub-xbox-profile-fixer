using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GHubXBOXProfileFixer
{
    internal static class GameIconLoader
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "ExtractIconExW")]
        private static extern uint ExtractIconEx(string file, int index, out IntPtr large, out IntPtr small, uint count);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr icon);

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
            IntPtr large = IntPtr.Zero, small = IntPtr.Zero;
            try
            {
                // Unlike ExtractAssociatedIcon, this does not substitute Windows' generic EXE icon.
                if (ExtractIconEx(game.ExecutablePath, 0, out large, out small, 1) == 0) return null;
                IntPtr handle = large != IntPtr.Zero ? large : small;
                if (handle == IntPtr.Zero) return null;
                var image = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                image.Freeze();
                return image;
            }
            catch { return null; }
            finally
            {
                if (large != IntPtr.Zero) DestroyIcon(large);
                if (small != IntPtr.Zero && small != large) DestroyIcon(small);
            }
        }
    }
}
