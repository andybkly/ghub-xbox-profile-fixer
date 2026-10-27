using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using GHubXBOXProfileFixer;

internal static class RegressionTests
{
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    static string folder;
    static int passed;
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_open(byte[] path, out IntPtr db);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_exec(IntPtr db, byte[] sql, IntPtr cb, IntPtr data, out IntPtr error);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern int sqlite3_close(IntPtr db);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] static extern void sqlite3_free(IntPtr value);
    static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text + "\0");
    static void Sql(IntPtr db, string sql)
    {
        IntPtr error;
        int result = sqlite3_exec(db, Utf8(sql), IntPtr.Zero, IntPtr.Zero, out error);
        if (result != 0)
        {
            string message = Marshal.PtrToStringAnsi(error);
            sqlite3_free(error);
            throw new Exception(message);
        }
    }
    static IntPtr Open(string path)
    {
        IntPtr db;
        if (sqlite3_open(Utf8(path), out db) != 0) throw new Exception("Fixture database could not open.");
        return db;
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
    static void Throws(Action action, string message)
    {
        bool failed = false;
        try { action(); } catch (IOException) { failed = true; } catch (InvalidOperationException) { failed = true; }
        Check(failed, message);
    }
    static Dictionary<string, object> D(params object[] values)
    {
        var result = new Dictionary<string, object>();
        for (int i = 0; i < values.Length; i += 2) result[(string)values[i]] = values[i + 1];
        return result;
    }
    static object[] Items(Dictionary<string, object> root, string key) => (object[])((Dictionary<string, object>)root[key])[key];
    static Dictionary<string, object> Read(string path)
    {
        long id;
        return (Dictionary<string, object>)Json.DeserializeObject(Encoding.UTF8.GetString(WinSqlite.ReadData(path, out id)));
    }
    static Dictionary<string, object> Fixture()
    {
        return D("applications", D("applications", new[] { D("applicationId", "desktop", "name", "APPLICATION_NAME_DESKTOP", "userPaths", new[] { Path.Combine(folder, "registered.exe") }) }),
            "profiles", D("profiles", new[] { D("id", "desktop-profile", "applicationId", "desktop", "activeForApplication", true,
                "assignments", new[] { D("cardId", "macro") }, "lightingCard", "light", "syncLightingCard", "sync", "publishing", "remove") }),
            "cards", D("cards", new[] { D("id", "macro", "payload", "keep"), D("id", "light"), D("id", "sync") }),
            "unknownFutureSetting", D("keep", true));
    }
    static string Database(string name, Dictionary<string, object> root)
    {
        string path = Path.Combine(folder, name + ".db");
        IntPtr db = Open(path);
        try { Sql(db, "CREATE TABLE DATA (_id INTEGER PRIMARY KEY, FILE BLOB); INSERT INTO DATA VALUES(1, X'" + BitConverter.ToString(Encoding.UTF8.GetBytes(Json.Serialize(root))).Replace("-", "") + "');"); }
        finally { sqlite3_close(db); }
        return path;
    }
    static string Exe(string relative)
    {
        string path = Path.Combine(folder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, "fixture");
        return path;
    }
    static int Main(string[] args)
    {
        folder = Path.Combine(Path.GetTempPath(), "ghub-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var source = Database("source", Fixture());
            byte[] before = File.ReadAllBytes(source);
            string gameExe = Exe("games\\Test\\Content\\game.exe");
            var game = new XboxGame { Name = "Test game", ExecutablePath = gameExe };
            var model = GHubDatabase.Load(source);
            Check(model.RegisteredPaths().Contains(Path.Combine(folder, "registered.exe")), "userPaths recognised");
            Check(model.AddXboxGames(new[] { game, game }) == 1, "duplicate selections added once");
            Check(model.AddXboxGames(new[] { game }) == 0, "repeat add is idempotent");
            string output = Path.Combine(folder, "output.db");
            model.SaveCopy(source, output);
            Check(before.SequenceEqual(File.ReadAllBytes(source)), "source stays byte-for-byte unchanged");
            Check(WinSqlite.IntegrityCheck(output) == "ok", "output SQLite integrity");
            var root = Read(output);
            Check(Items(root, "applications").Length == 2 && Items(root, "profiles").Length == 2, "application and profile created together");
            Check(root.ContainsKey("unknownFutureSetting"), "unknown settings preserved");
            var profile = (Dictionary<string, object>)Items(root, "profiles")[1];
            var cards = Items(root, "cards").Cast<Dictionary<string, object>>().ToDictionary(c => (string)c["id"]);
            Check(!profile.ContainsKey("publishing"), "publishing metadata removed from clone");
            Check(cards.ContainsKey((string)profile["lightingCard"]) && (string)profile["lightingCard"] != "light", "private lighting card cloned and linked");
            Check(cards.ContainsKey((string)profile["syncLightingCard"]) && (string)profile["syncLightingCard"] != "sync", "sync lighting card cloned and linked");
            var assignment = (Dictionary<string, object>)((object[])profile["assignments"])[0];
            Check((string)cards[(string)assignment["cardId"]]["payload"] == "keep", "assignment payload preserved in cloned card");
            Throws(() => model.SaveCopy(source, source), "source overwrite rejected");
            model.SaveCopy(source, output);
            Check(WinSqlite.IntegrityCheck(output) == "ok", "validated export can replace previous output");
            byte[] outputBefore = File.ReadAllBytes(output);
            WinSqlite.WriteData(source, 1, Encoding.UTF8.GetBytes(Json.Serialize(Fixture()).Replace("unknownFutureSetting", "changedSetting")));
            Throws(() => model.SaveCopy(source, output), "changed source rejected");
            Check(outputBefore.SequenceEqual(File.ReadAllBytes(output)), "failed export preserves existing output");
            Check(!Directory.GetFiles(folder, ".ghub-*").Any(), "temporary export cleaned up");
            Throws(() => WinSqlite.WriteData(output, 99, new byte[] { 1 }), "missing DATA row rejected");
            Throws(() => GHubDatabase.Load(output).AddXboxGames(new[] { new XboxGame { ExecutablePath = Path.Combine(folder, "missing.exe") } }), "missing selected executable rejected");
            var absentLighting = Fixture();
            var desktop = (Dictionary<string, object>)Items(absentLighting, "profiles")[0];
            desktop.Remove("lightingCard"); desktop.Remove("syncLightingCard");
            var noLight = Database("no-light", absentLighting);
            var noLightModel = GHubDatabase.Load(noLight); noLightModel.AddXboxGames(new[] { game });
            string noLightOut = Path.Combine(folder, "no-light-out.db"); noLightModel.SaveCopy(noLight, noLightOut);
            var noLightProfile = (Dictionary<string, object>)Items(Read(noLightOut), "profiles")[1];
            Check(!noLightProfile.ContainsKey("lightingCard") && !noLightProfile.ContainsKey("syncLightingCard"), "absent lighting references are not fabricated");
            var walSource = Database("wal", Fixture());
            IntPtr wal = Open(walSource);
            try
            {
                Sql(wal, "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; CREATE TABLE recent(value TEXT); INSERT INTO recent VALUES('committed');");
                Check(File.Exists(walSource + "-wal"), "fixture has live WAL data");
                var walModel = GHubDatabase.Load(walSource); walModel.AddXboxGames(new[] { game });
                string walOut = Path.Combine(folder, "wal-out.db"); walModel.SaveCopy(walSource, walOut);
                IntPtr copy = Open(walOut);
                try { Sql(copy, "CREATE TABLE verify AS SELECT * FROM recent;"); } finally { sqlite3_close(copy); }
                Check(true, "export includes table committed in WAL");
                Check(!File.Exists(walOut + "-wal"), "export is standalone");
            }
            finally { sqlite3_close(wal); }
            var applySource = Database("apply", Fixture());
            byte[] applyBefore = File.ReadAllBytes(applySource);
            var apply = GHubDatabase.Load(applySource); apply.AddXboxGames(new[] { game });
            int checks = 0;
            string backup = apply.ApplyWithBackup(applySource, () => checks++);
            Check(checks == 2, "G HUB checked before staging and replacing");
            Check(Path.GetFileName(backup).StartsWith("apply-backup-" + DateTime.Now.ToString("ddMMyyyy")), "backup filename includes local date");
            Check(applyBefore.SequenceEqual(File.ReadAllBytes(backup)), "backup exactly preserves original settings");
            Check(GHubDatabase.Load(applySource).RegisteredPaths().Contains(gameExe), "apply installs selected profile in source database");
            string backup2 = GHubDatabase.Load(applySource).ApplyWithBackup(applySource, () => { });
            Check(backup != backup2 && File.Exists(backup), "repeat apply preserves earlier backup");
            byte[] appliedBefore = File.ReadAllBytes(applySource);
            int backupsBefore = Directory.GetFiles(folder, "apply-backup-*.db").Length;
            int guards = 0;
            Throws(() => GHubDatabase.Load(applySource).ApplyWithBackup(applySource, () => { if (++guards == 2) throw new InvalidOperationException("G HUB restarted"); }), "G HUB restarting aborts apply");
            Check(appliedBefore.SequenceEqual(File.ReadAllBytes(applySource)), "aborted apply keeps source unchanged");
            Check(backupsBefore == Directory.GetFiles(folder, "apply-backup-*.db").Length, "aborted apply creates no misleading backup");
            Check(!Directory.GetFiles(folder, ".ghub-apply-*").Any(), "aborted apply removes staged files");
            // Simulate sidecars left by an exited process using a stable copy of an idle WAL DB.
            string seed = Database("leftover-seed", Fixture());
            string leftover = Path.Combine(folder, "leftover.db");
            IntPtr seedHandle = Open(seed);
            try
            {
                Sql(seedHandle, "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; CREATE TABLE pending(value TEXT); INSERT INTO pending VALUES('preserve me');");
                File.Copy(seed, leftover);
                File.Copy(seed + "-wal", leftover + "-wal");
            }
            finally { sqlite3_close(seedHandle); }
            Check(File.Exists(leftover + "-wal"), "leftover WAL fixture exists without an open connection");
            var leftoverModel = GHubDatabase.Load(leftover); leftoverModel.AddXboxGames(new[] { game });
            string leftoverBackup = leftoverModel.ApplyWithBackup(leftover, () => { });
            Check(GHubDatabase.Load(leftover).RegisteredPaths().Contains(gameExe), "apply succeeds with leftover WAL files");
            foreach (string path in new[] { leftover, leftoverBackup })
            {
                IntPtr verify = Open(path);
                try { Sql(verify, "CREATE TABLE verify_pending AS SELECT * FROM pending WHERE value='preserve me';"); }
                finally { sqlite3_close(verify); }
                Check(WinSqlite.IntegrityCheck(path) == "ok", "committed WAL table preserved in " + Path.GetFileName(path));
            }
            // A real writer must still block applying; no replacement or backup may appear.
            var lockedModel = GHubDatabase.Load(applySource);
            IntPtr writer = Open(applySource);
            try
            {
                Sql(writer, "BEGIN IMMEDIATE");
                Throws(() => lockedModel.ApplyWithBackup(applySource, () => { }), "active SQLite writer blocks applying");
                Check(appliedBefore.SequenceEqual(File.ReadAllBytes(applySource)), "active writer failure leaves settings unchanged");
                Check(backupsBefore == Directory.GetFiles(folder, "apply-backup-*.db").Length, "active writer failure creates no backup");
            }
            finally { Sql(writer, "ROLLBACK"); sqlite3_close(writer); }
            string lateSource = Database("late-journal", Fixture());
            var lateModel = GHubDatabase.Load(lateSource); lateModel.AddXboxGames(new[] { game });
            int lateChecks = 0;
            string lateBackup = lateModel.ApplyWithBackup(lateSource, () =>
            {
                if (++lateChecks == 2) File.WriteAllBytes(lateSource + "-journal", new byte[0]);
            });
            Check(File.Exists(lateBackup) && GHubDatabase.Load(lateSource).RegisteredPaths().Contains(gameExe), "harmless journal appearing during staging does not block apply");
            var lateLocked = GHubDatabase.Load(applySource);
            IntPtr lateWriter = IntPtr.Zero;
            int lateLockChecks = 0;
            try
            {
                Throws(() => lateLocked.ApplyWithBackup(applySource, () =>
                {
                    if (++lateLockChecks == 2) { lateWriter = Open(applySource); Sql(lateWriter, "BEGIN IMMEDIATE"); }
                }), "writer starting after staging blocks replacement");
                Check(appliedBefore.SequenceEqual(File.ReadAllBytes(applySource)), "late writer keeps source unchanged");
                Check(backupsBefore == Directory.GetFiles(folder, "apply-backup-*.db").Length, "late writer creates no backup");
            }
            finally { if (lateWriter != IntPtr.Zero) { Sql(lateWriter, "ROLLBACK"); sqlite3_close(lateWriter); } }
            var staleApply = GHubDatabase.Load(applySource);
            WinSqlite.WriteData(applySource, 1, Encoding.UTF8.GetBytes(Json.Serialize(Fixture())));
            byte[] changedBefore = File.ReadAllBytes(applySource);
            Throws(() => staleApply.ApplyWithBackup(applySource, () => { }), "stale model cannot overwrite changed settings");
            Check(changedBefore.SequenceEqual(File.ReadAllBytes(applySource)), "stale apply preserves latest settings");
            string manifest = Path.Combine(Path.GetDirectoryName(gameExe), "MicrosoftGame.config");
            File.WriteAllText(manifest, "<Game><ShellVisuals DefaultDisplayName='Example'/><ExecutableList><Executable Name='game.exe' TargetDeviceFamily='PC'/></ExecutableList></Game>");
            var found = XboxScanner.Scan(new[] { Path.Combine(folder, "games") });
            Check(found.Count == 1 && found[0].ManifestDetected && found[0].Name == "Example", "manifest executable detected");
            string assets = Path.Combine(Path.GetDirectoryName(gameExe), "Assets"); Directory.CreateDirectory(assets);
            string logo = Path.Combine(assets, "Logo.scale-200.png"); File.WriteAllText(logo, "fixture");
            File.WriteAllText(manifest, "<Game><ShellVisuals Square150x150Logo='Assets/Logo.png'/></Game>");
            Check(XboxScanner.ArtworkPaths(game).Contains(logo), "manual EXE resolves qualified Xbox artwork from parent manifest");
            File.WriteAllText(manifest, "<Game><ShellVisuals Square150x150Logo='../../outside.png'/></Game>");
            Check(!XboxScanner.ArtworkPaths(game).Any(), "artwork paths cannot escape manifest folder");
            File.WriteAllText(manifest, "<broken");
            found = XboxScanner.Scan(new[] { Path.Combine(folder, "games", "Test") });
            Check(found.Count == 1 && !found[0].ManifestDetected, "malformed manifest falls back to executable");
            File.Delete(manifest);
            found = XboxScanner.Scan(new[] { Path.Combine(folder, "games", "Test") });
            Check(found.Count == 1 && found[0].Name == "Test", "direct game folder without manifest scanned once");
            Console.WriteLine(passed + " regression checks passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            if (args.Contains("--keep")) File.WriteAllText("tests/fixture-path.txt", folder);
            else try { Directory.Delete(folder, true); } catch { }
        }
    }
}
