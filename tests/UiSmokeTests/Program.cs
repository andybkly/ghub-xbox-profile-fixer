using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GHubXBOXProfileFixer;

internal static class UiSmokeTests
{
    static MainWindow window;
    static T Field<T>(string name) => (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
    static object Call(string name, params object[] args) => typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, args);
    static void Check(bool ok, string description) { if (!ok) throw new Exception(description); Console.WriteLine("PASS " + description); }
    static void Drain()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
    static void Await(Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!task.IsCompleted)
        {
            if (DateTime.UtcNow > deadline) throw new Exception("Scan timed out.");
            Drain();
            System.Threading.Thread.Sleep(10);
        }
        task.GetAwaiter().GetResult();
    }
    static void Capture(string name)
    {
        window.UpdateLayout(); Drain();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine("previews", name + ".png"))) encoder.Save(stream);
    }
    [STAThread] static int Main()
    {
        try
        {
            var app = new App(); app.InitializeComponent();
            // The app is created manually so StartupUri is never used by a dispatcher run.
            window = new MainWindow(); window.Show(); Drain();
            Directory.CreateDirectory("previews");
            string fixture = File.ReadAllText("tests/fixture-path.txt");
            var pathBox = (TextBox)window.FindName("DatabasePathBox");
            pathBox.Text = Path.Combine(fixture, "output.db");
            Check(!((Button)window.FindName("GamesContinueButton")).IsEnabled, "review disabled for empty selection");
            Capture("01-database");
            Field<List<string>>("customRoots").Add(Path.Combine(fixture, "games"));
            Await((Task)Call("ScanGames"));
            var games = Field<ObservableCollection<GameViewModel>>("games");
            Check(games.Count == 1 && !games[0].CanSelect, "scan marks registered game unselectable");
            string manualPath = Path.Combine(fixture, "manual.exe"); File.WriteAllText(manualPath, "fixture");
            var manual = new XboxGame { Name = "Manually added game", ExecutablePath = manualPath };
            Field<List<XboxGame>>("manualGames").Add(manual);
            games.Add(new GameViewModel(manual, null));
            Await((Task)Call("ScanGames"));
            Check(games.Count == 2 && games.Single(g => g.ExecutablePath == manualPath).Selected, "manual game survives rescan");
            games.Single(g => g.ExecutablePath == manualPath).Selected = false;
            Await((Task)Call("ScanGames"));
            Check(!games.Single(g => g.ExecutablePath == manualPath).Selected, "deselection survives rescan");
            Call("SelectAll_Click", null, new RoutedEventArgs());
            Check(((Button)window.FindName("GamesContinueButton")).IsEnabled, "select all enables review");
            Call("ShowStep", 2); Capture("02-library");
            window.Width = window.MinWidth; window.Height = window.MinHeight; Capture("03-library-minimum-size");
            window.Width = 1180; window.Height = 800;
            Call("PrepareCreatePage"); Capture("04-export");
            pathBox.Text = Path.Combine(fixture, "no-light.db");
            Check(games.Count == 0 && Field<List<XboxGame>>("manualGames").Count == 0, "database change clears stale selection and manual entries");
            window.Close();
            Console.WriteLine("UI smoke checks passed; previews saved."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); window?.Close(); return 1; }
    }
}
