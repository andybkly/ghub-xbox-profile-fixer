using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace GHubXBOXProfileFixer
{
    internal sealed class XboxGame
    {
        public string Name { get; set; }
        public string ExecutablePath { get; set; }
        public string SourceFolder { get; set; }
        public bool AlreadyRegistered { get; set; }
        public bool ManifestDetected { get; set; }
    }

    internal static class XboxScanner
    {
        private static readonly string[] ExcludedNameParts =
        {
            "unins", "crash", "report", "helper", "launcher", "setup", "install", "eac", "easyanticheat",
            "beservice", "battleye", "vc_redist", "dxsetup", "unitycrashhandler"
        };

        public static List<string> FindXboxRoots()
        {
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady || (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable)) continue;
                    string compact = Path.Combine(drive.RootDirectory.FullName, "XboxGames");
                    string spaced = Path.Combine(drive.RootDirectory.FullName, "Xbox Games");
                    if (Directory.Exists(compact)) roots.Add(compact);
                    if (Directory.Exists(spaced)) roots.Add(spaced);
                    string markerRoot = ReadGamingRoot(Path.Combine(drive.RootDirectory.FullName, ".GamingRoot"), drive.RootDirectory.FullName);
                    if (markerRoot != null) roots.Add(markerRoot);
                }
                catch { }
            }
            return roots.OrderBy(x => x).ToList();
        }

        private static string ReadGamingRoot(string markerPath, string driveRoot)
        {
            try
            {
                if (!File.Exists(markerPath)) return null;
                byte[] bytes = File.ReadAllBytes(markerPath);
                if (bytes.Length <= 8 || bytes[0] != (byte)'R' || bytes[1] != (byte)'G' || bytes[2] != (byte)'B' || bytes[3] != (byte)'X') return null;
                string relative = Encoding.Unicode.GetString(bytes, 8, bytes.Length - 8).Trim('\0', ' ', '\r', '\n', '\t');
                if (String.IsNullOrWhiteSpace(relative) || relative.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return null;
                string resolved = Path.IsPathRooted(relative) ? relative : Path.Combine(driveRoot, relative);
                return Directory.Exists(resolved) ? Path.GetFullPath(resolved) : null;
            }
            catch { return null; }
        }

        public static List<XboxGame> Scan(IEnumerable<string> roots)
        {
            var games = new List<XboxGame>();
            foreach (string suppliedRoot in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (string gameFolder in ResolveGameFolders(suppliedRoot))
                {
                    try { games.AddRange(ScanGameFolder(gameFolder)); }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                    catch (System.Xml.XmlException) { }
                }
            }
            return games
                .Where(g => File.Exists(g.ExecutablePath))
                .GroupBy(g => Path.GetFullPath(g.ExecutablePath), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static IEnumerable<string> ResolveGameFolders(string root)
        {
            if (File.Exists(Path.Combine(root, "MicrosoftGame.config")) ||
                File.Exists(Path.Combine(root, "Content", "MicrosoftGame.config")))
            {
                yield return root;
                yield break;
            }
            string name = new DirectoryInfo(root).Name;
            if (name.Equals("Content", StringComparison.OrdinalIgnoreCase))
            {
                yield return root;
                yield break;
            }
            string[] children;
            try { children = Directory.GetDirectories(root); }
            catch { yield break; }
            foreach (string child in children) yield return child;
        }

        private static IEnumerable<XboxGame> ScanGameFolder(string gameFolder)
        {
            string content = Directory.Exists(Path.Combine(gameFolder, "Content")) ? Path.Combine(gameFolder, "Content") : gameFolder;
            string manifest = SafeFiles(content, "MicrosoftGame.config", 3).FirstOrDefault();
            if (manifest != null)
            {
                var fromManifest = ParseManifest(gameFolder, content, manifest).ToList();
                if (fromManifest.Count > 0) return fromManifest;
            }
            string fallback = ChooseFallbackExecutable(content, gameFolder);
            if (fallback == null) return Enumerable.Empty<XboxGame>();
            return new[]
            {
                new XboxGame
                {
                    Name = FriendlyFolderName(gameFolder), ExecutablePath = fallback,
                    SourceFolder = gameFolder, ManifestDetected = false
                }
            };
        }

        private static IEnumerable<XboxGame> ParseManifest(string gameFolder, string content, string manifestPath)
        {
            XDocument doc = XDocument.Load(manifestPath, LoadOptions.None);
            string manifestFolder = Path.GetDirectoryName(manifestPath);
            string displayName = doc.Descendants().Where(e => e.Name.LocalName == "ShellVisuals")
                .Select(e => Attribute(e, "DefaultDisplayName") ?? Attribute(e, "DisplayName")).FirstOrDefault(x => !String.IsNullOrWhiteSpace(x));
            if (String.IsNullOrWhiteSpace(displayName) || displayName.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
                displayName = FriendlyFolderName(gameFolder);

            foreach (XElement element in doc.Descendants().Where(e => e.Name.LocalName == "Executable"))
            {
                string target = Attribute(element, "TargetDeviceFamily");
                if (!String.IsNullOrWhiteSpace(target) && target.IndexOf("PC", StringComparison.OrdinalIgnoreCase) < 0) continue;
                string relative = Attribute(element, "Name") ?? Attribute(element, "Executable");
                if (String.IsNullOrWhiteSpace(relative)) continue;
                string path = Path.IsPathRooted(relative) ? relative : Path.Combine(manifestFolder, relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                {
                    string byName = SafeFiles(content, Path.GetFileName(relative), 4).FirstOrDefault();
                    if (byName != null) path = byName;
                }
                if (!File.Exists(path)) continue;
                yield return new XboxGame
                {
                    Name = displayName,
                    ExecutablePath = Path.GetFullPath(path),
                    SourceFolder = gameFolder,
                    ManifestDetected = true
                };
            }
        }

        private static string Attribute(XElement element, string name)
            => element.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;

        private static string ChooseFallbackExecutable(string content, string gameFolder)
        {
            var executables = SafeFiles(content, "*.exe", 3)
                .Where(path => !ExcludedNameParts.Any(x => Path.GetFileNameWithoutExtension(path).IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();
            if (executables.Count == 0) return null;
            string expected = FriendlyFolderName(gameFolder).Replace(" ", "");
            return executables
                .OrderByDescending(path => Path.GetFileNameWithoutExtension(path).Replace(" ", "").IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0)
                .ThenBy(path => path.Count(c => c == Path.DirectorySeparatorChar))
                .ThenByDescending(path => new FileInfo(path).Length)
                .First();
        }

        private static IEnumerable<string> SafeFiles(string root, string pattern, int maxDepth)
        {
            var pending = new Queue<Tuple<string, int>>();
            pending.Enqueue(Tuple.Create(root, 0));
            while (pending.Count > 0)
            {
                var item = pending.Dequeue();
                string[] files = new string[0], directories = new string[0];
                try { files = Directory.GetFiles(item.Item1, pattern, SearchOption.TopDirectoryOnly); } catch { }
                foreach (string file in files) yield return file;
                if (item.Item2 >= maxDepth) continue;
                try { directories = Directory.GetDirectories(item.Item1); } catch { }
                foreach (string directory in directories) pending.Enqueue(Tuple.Create(directory, item.Item2 + 1));
            }
        }

        private static string FriendlyFolderName(string folder)
        {
            var info = new DirectoryInfo(folder);
            if (info.Name.Equals("Content", StringComparison.OrdinalIgnoreCase) && info.Parent != null) info = info.Parent;
            return info.Name;
        }
    }
}
