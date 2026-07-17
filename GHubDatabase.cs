using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace GHubXBOXProfileFixer
{
    internal sealed class GHubDatabase
    {
        private readonly JavaScriptSerializer serializer;
        private readonly long rowId;
        private readonly Dictionary<string, object> root;

        private GHubDatabase(Dictionary<string, object> root, long rowId, JavaScriptSerializer serializer)
        {
            this.root = root;
            this.rowId = rowId;
            this.serializer = serializer;
            ValidateShape();
        }

        public static GHubDatabase Load(string path)
        {
            long id;
            byte[] bytes = WinSqlite.ReadData(path, out id);
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 500 };
            var root = serializer.DeserializeObject(Encoding.UTF8.GetString(bytes)) as Dictionary<string, object>;
            if (root == null) throw new InvalidDataException("The LGHUB data is not a JSON object.");
            return new GHubDatabase(root, id, serializer);
        }

        private void ValidateShape()
        {
            GetNestedList("profiles", "profiles");
            GetNestedList("applications", "applications");
            GetNestedList("cards", "cards");
        }

        private List<object> GetNestedList(string parentKey, string childKey)
        {
            Dictionary<string, object> parent;
            object rawParent, rawList;
            if (!root.TryGetValue(parentKey, out rawParent) || (parent = rawParent as Dictionary<string, object>) == null || !parent.TryGetValue(childKey, out rawList))
                throw new InvalidDataException("This database does not contain the expected LGHUB " + parentKey + " structure.");
            var list = rawList as object[];
            if (list != null)
            {
                var converted = list.ToList();
                parent[childKey] = converted;
                return converted;
            }
            var existing = rawList as List<object>;
            if (existing == null) throw new InvalidDataException("The LGHUB " + parentKey + " list has an unexpected format.");
            return existing;
        }

        private static Dictionary<string, object> Dict(object value) => value as Dictionary<string, object>;
        private static string Text(Dictionary<string, object> value, string key)
        {
            object raw;
            return value != null && value.TryGetValue(key, out raw) && raw != null ? Convert.ToString(raw) : null;
        }

        private static bool Bool(Dictionary<string, object> value, string key)
        {
            object raw;
            return value != null && value.TryGetValue(key, out raw) && raw != null && Convert.ToBoolean(raw);
        }

        private List<object> Profiles => GetNestedList("profiles", "profiles");
        private List<object> Applications => GetNestedList("applications", "applications");
        private List<object> Cards => GetNestedList("cards", "cards");

        public HashSet<string> RegisteredPaths()
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in Applications)
            {
                var app = Dict(raw);
                AddPath(paths, Text(app, "applicationPath"));
                object userPaths;
                if (app != null && app.TryGetValue("userPaths", out userPaths))
                {
                    var items = userPaths as object[];
                    if (items != null) foreach (object item in items) AddPath(paths, Convert.ToString(item));
                    var list = userPaths as List<object>;
                    if (list != null) foreach (object item in list) AddPath(paths, Convert.ToString(item));
                }
            }
            return paths;
        }

        private static void AddPath(HashSet<string> paths, string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return;
            try { paths.Add(Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar)); }
            catch { paths.Add(path.Trim()); }
        }

        public int AddXboxGames(IEnumerable<XboxGame> selectedGames)
        {
            var games = selectedGames.GroupBy(g => Normalize(g.ExecutablePath), StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
            var existing = RegisteredPaths();
            var desktopApp = Applications.Select(Dict).FirstOrDefault(a => Text(a, "name") == "APPLICATION_NAME_DESKTOP");
            if (desktopApp == null) throw new InvalidDataException("The LGHUB Desktop application record was not found.");
            string desktopId = Text(desktopApp, "applicationId");
            var desktopProfile = Profiles.Select(Dict).FirstOrDefault(p => Text(p, "applicationId") == desktopId && Bool(p, "activeForApplication"));
            if (desktopProfile == null) throw new InvalidDataException("The active LGHUB Desktop profile was not found.");

            var cardById = Cards.Select(Dict).Where(c => c != null && Text(c, "id") != null).ToDictionary(c => Text(c, "id"), c => c, StringComparer.Ordinal);
            var referenceCounts = CountProfileCardReferences();
            int added = 0;
            foreach (XboxGame game in games)
            {
                string normalized = Normalize(game.ExecutablePath);
                if (existing.Contains(normalized)) continue;
                string appId = Guid.NewGuid().ToString();
                Applications.Add(new Dictionary<string, object>
                {
                    ["applicationId"] = appId,
                    ["applicationPath"] = game.ExecutablePath,
                    ["isCustom"] = true,
                    ["name"] = game.Name
                });

                var profile = DeepClone(desktopProfile);
                profile["applicationId"] = appId;
                profile["id"] = appId;
                profile["name"] = "PROFILE_NAME_DEFAULT";
                profile.Remove("publishing");
                ClonePrivateCards(profile, cardById, referenceCounts);
                Profiles.Add(profile);
                existing.Add(normalized);
                added++;
            }
            return added;
        }

        private Dictionary<string, int> CountProfileCardReferences()
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var profile in Profiles.Select(Dict).Where(p => p != null))
            {
                var refs = ProfileCardReferences(profile);
                foreach (string id in refs) counts[id] = counts.ContainsKey(id) ? counts[id] + 1 : 1;
            }
            return counts;
        }

        private static HashSet<string> ProfileCardReferences(Dictionary<string, object> profile)
        {
            var refs = new HashSet<string>(StringComparer.Ordinal);
            object rawAssignments;
            if (profile.TryGetValue("assignments", out rawAssignments))
            {
                foreach (var assignment in ToMutableList(rawAssignments).Select(Dict).Where(a => a != null))
                {
                    string id = Text(assignment, "cardId");
                    if (id != null) refs.Add(id);
                }
            }
            string lighting = Text(profile, "lightingCard");
            if (lighting != null) refs.Add(lighting);
            return refs;
        }

        private void ClonePrivateCards(Dictionary<string, object> profile, Dictionary<string, Dictionary<string, object>> cardById, Dictionary<string, int> counts)
        {
            var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string oldId in ProfileCardReferences(profile))
            {
                Dictionary<string, object> card;
                int count;
                if (!cardById.TryGetValue(oldId, out card) || !counts.TryGetValue(oldId, out count) || count != 1) continue;
                string newId = Guid.NewGuid().ToString();
                replacements[oldId] = newId;
                var clonedCard = DeepClone(card);
                clonedCard["id"] = newId;
                Cards.Add(clonedCard);
            }
            object rawAssignments;
            if (profile.TryGetValue("assignments", out rawAssignments))
            {
                var assignments = ToMutableList(rawAssignments);
                profile["assignments"] = assignments;
                foreach (var assignment in assignments.Select(Dict).Where(a => a != null))
                {
                    string oldId = Text(assignment, "cardId"), newId;
                    if (oldId != null && replacements.TryGetValue(oldId, out newId)) assignment["cardId"] = newId;
                }
            }
            string oldLighting = Text(profile, "lightingCard"), newLighting;
            profile["lightingCard"] = oldLighting != null && replacements.TryGetValue(oldLighting, out newLighting) ? newLighting : oldLighting ?? Guid.NewGuid().ToString();
            profile["syncLightingCard"] = Guid.NewGuid().ToString();
        }

        private Dictionary<string, object> DeepClone(Dictionary<string, object> source)
            => serializer.DeserializeObject(serializer.Serialize(source)) as Dictionary<string, object>;

        private static List<object> ToMutableList(object value)
        {
            var list = value as List<object>;
            if (list != null) return list;
            var array = value as object[];
            return array != null ? array.ToList() : new List<object>();
        }

        private static string Normalize(string path)
        {
            try { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar); }
            catch { return path.Trim(); }
        }

        public void SaveCopy(string sourcePath, string destinationPath)
        {
            if (String.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Choose a different filename. The original settings.db will not be overwritten.");
            File.Copy(sourcePath, destinationPath, true);
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(serializer.Serialize(root));
                WinSqlite.WriteData(destinationPath, rowId, bytes);
                string integrity = WinSqlite.IntegrityCheck(destinationPath);
                if (!String.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The converted database failed SQLite validation: " + integrity);
                ValidateRelationships();
            }
            catch
            {
                try { File.Delete(destinationPath); } catch { }
                throw;
            }
        }

        private void ValidateRelationships()
        {
            var appIds = new HashSet<string>(Applications.Select(Dict).Where(a => a != null).Select(a => Text(a, "applicationId")).Where(x => x != null), StringComparer.Ordinal);
            foreach (var profile in Profiles.Select(Dict).Where(p => p != null))
            {
                string id = Text(profile, "applicationId");
                if (id == null || !appIds.Contains(id)) throw new InvalidDataException("A profile has no matching application record.");
            }
        }
    }

}
