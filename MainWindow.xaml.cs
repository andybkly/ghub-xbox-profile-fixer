using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;
using DrawingIcon = System.Drawing.Icon;

namespace GHubXBOXProfileFixer
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<GameViewModel> games = new ObservableCollection<GameViewModel>();
        private readonly List<string> customRoots = new List<string>();
        private int currentStep = 1;

        public MainWindow()
        {
            InitializeComponent();
            GamesItemsControl.ItemsSource = games;
            string defaultDb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LGHUB", "settings.db");
            if (File.Exists(defaultDb))
            {
                DatabasePathBox.Text = defaultDb;
                SetDatabaseReady(true);
            }
            ShowStep(1);
        }

        private void ShowStep(int step)
        {
            currentStep = step;
            DatabasePage.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
            GamesPage.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
            CreatePage.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;

            SetNavState(Step1Nav, Step1NavText, step == 1, step > 1, "1", "G Hub database");
            SetNavState(Step2Nav, Step2NavText, step == 2, step > 2, "2", "XBOX games");
            SetNavState(Step3Nav, Step3NavText, step == 3, false, "3", "Create copy");
        }

        private void SetNavState(System.Windows.Controls.Border border, System.Windows.Controls.TextBlock text, bool active, bool complete, string number, string label)
        {
            border.Background = active ? (Brush)FindResource("PaleGreenBrush") : Brushes.Transparent;
            text.Foreground = active || complete ? (Brush)FindResource("GreenBrush") : (Brush)FindResource("InkBrush");
            text.Text = (complete ? "✓" : number) + "     " + label;
            border.Cursor = Cursors.Hand;
        }

        private void Step1Nav_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => ShowStep(1);
        private void Step2Nav_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (File.Exists(DatabasePathBox.Text)) ShowStep(2);
        }
        private void Step3Nav_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (games.Any(g => g.Selected && g.CanSelect)) PrepareCreatePage();
        }

        private void BrowseDatabase_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select LGHUB settings.db (close G Hub first)",
                Filter = "LGHUB settings.db|settings.db|Database files (*.db)|*.db|All files (*.*)|*.*"
            };
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LGHUB");
            if (Directory.Exists(folder)) dialog.InitialDirectory = folder;
            if (dialog.ShowDialog(this) == true)
            {
                DatabasePathBox.Text = dialog.FileName;
                SetDatabaseReady(true);
            }
        }

        private void SetDatabaseReady(bool ready)
        {
            DatabaseStatusText.Text = ready ? "✓  Ready" : "Select your settings.db";
            DatabaseStatusText.Foreground = ready ? (Brush)FindResource("GreenBrush") : (Brush)FindResource("MutedBrush");
        }

        private async void DatabaseContinue_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(DatabasePathBox.Text))
            {
                ShowError("Select a valid LGHUB settings.db first.");
                return;
            }
            try { GHubDatabase.Load(DatabasePathBox.Text); }
            catch (Exception ex) { ShowError("That file is not a valid G Hub database.\n\n" + ex.Message); return; }
            DatabasePathSummary.Text = DatabasePathBox.Text;
            ShowStep(2);
            if (games.Count == 0) await ScanGames();
        }

        private void ChangeDatabase_Click(object sender, RoutedEventArgs e) => ShowStep(1);
        private async void ScanAgain_Click(object sender, RoutedEventArgs e) => await ScanGames();

        private async Task ScanGames()
        {
            ScanSummaryText.Text = "Scanning XBOX game folders…";
            try
            {
                var roots = XboxScanner.FindXboxRoots().Concat(customRoots).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var found = await Task.Run(() => XboxScanner.Scan(roots));
                var database = GHubDatabase.Load(DatabasePathBox.Text);
                HashSet<string> registered = database.RegisteredPaths();
                games.Clear();
                foreach (XboxGame game in found)
                {
                    game.AlreadyRegistered = registered.Contains(Normalize(game.ExecutablePath));
                    games.Add(new GameViewModel(game, LoadIcon(game.ExecutablePath)));
                }
                ScanSummaryText.Text = found.Count == 0 ? "No XBOX games found" : found.Count + (found.Count == 1 ? " game found" : " games found");
                UpdateSelectedCount();
            }
            catch (Exception ex) { ShowError("The XBOX game scan failed.\n\n" + ex.Message); }
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new Forms.FolderBrowserDialog { Description = "Select an XboxGames folder, game folder, or Content folder" })
            {
                if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
                if (!customRoots.Contains(dialog.SelectedPath, StringComparer.OrdinalIgnoreCase)) customRoots.Add(dialog.SelectedPath);
            }
            ScanSummaryText.Text = "Folder added — scan again";
        }

        private void AddExecutable_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Title = "Select the game's main executable", Filter = "Applications (*.exe)|*.exe" };
            if (dialog.ShowDialog(this) != true) return;
            bool alreadyRegistered = false;
            try { alreadyRegistered = GHubDatabase.Load(DatabasePathBox.Text).RegisteredPaths().Contains(Normalize(dialog.FileName)); } catch { }
            var game = new XboxGame
            {
                Name = Path.GetFileNameWithoutExtension(dialog.FileName),
                ExecutablePath = dialog.FileName,
                SourceFolder = Path.GetDirectoryName(dialog.FileName),
                ManifestDetected = false,
                AlreadyRegistered = alreadyRegistered
            };
            games.Add(new GameViewModel(game, LoadIcon(game.ExecutablePath)));
            ScanSummaryText.Text = games.Count + (games.Count == 1 ? " game found" : " games found");
            UpdateSelectedCount();
        }

        private static ImageSource LoadIcon(string executablePath)
        {
            try
            {
                using (DrawingIcon icon = DrawingIcon.ExtractAssociatedIcon(executablePath))
                {
                    if (icon == null) return null;
                    BitmapSource image = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    image.Freeze();
                    return image;
                }
            }
            catch { return null; }
        }

        private void GameCheckBox_Click(object sender, RoutedEventArgs e) => UpdateSelectedCount();

        private void UpdateSelectedCount()
        {
            int count = games.Count(g => g.Selected && g.CanSelect);
            SelectedCountText.Text = count + (count == 1 ? " game selected" : " games selected");
        }

        private void GamesContinue_Click(object sender, RoutedEventArgs e)
        {
            if (!games.Any(g => g.Selected && g.CanSelect))
            {
                ShowError("Select at least one missing game first.");
                return;
            }
            PrepareCreatePage();
        }

        private void PrepareCreatePage()
        {
            var selected = games.Where(g => g.Selected && g.CanSelect).ToList();
            CreateCountText.Text = selected.Count + (selected.Count == 1 ? " game will be added" : " games will be added");
            CreateNamesText.Text = String.Join("  •  ", selected.Select(g => g.Name));
            CreateSourceText.Text = "Source: " + DatabasePathBox.Text;
            ShowStep(3);
        }

        private void BackToGames_Click(object sender, RoutedEventArgs e) => ShowStep(2);

        private void CreateDatabase_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateSource()) return;
            var selected = games.Where(g => g.Selected && g.CanSelect).Select(g => g.Game).ToList();
            if (selected.Count == 0) { ShowError("Select at least one missing game first."); return; }
            var save = new SaveFileDialog
            {
                Title = "Save the fixed database",
                Filter = "Database files (*.db)|*.db",
                InitialDirectory = Path.GetDirectoryName(DatabasePathBox.Text),
                FileName = "settings-XBOX-profiles-fixed.db"
            };
            if (save.ShowDialog(this) != true) return;
            try
            {
                var database = GHubDatabase.Load(DatabasePathBox.Text);
                int added = database.AddXboxGames(selected);
                if (added == 0) { MessageBox.Show(this, "The selected games are already registered.", "Nothing to add", MessageBoxButton.OK, MessageBoxImage.Information); return; }
                database.SaveCopy(DatabasePathBox.Text, save.FileName);
                MessageBox.Show(this,
                    "Created " + added + (added == 1 ? " new G Hub profile.\n\n" : " new G Hub profiles.\n\n") +
                    "Created:\n" + save.FileName + "\n\nYour original database was not changed.",
                    "Fixed database created", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { ShowError("The fixed database could not be created. Your original database was not changed.\n\n" + ex.Message); }
        }

        private bool ValidateSource()
        {
            if (!File.Exists(DatabasePathBox.Text)) { ShowError("Select a valid LGHUB settings.db first."); return false; }
            if (IsGHubRunning())
            {
                ShowError("Logitech G Hub is still running. Quit it from the system tray and close lghub.exe and lghub_agent.exe before continuing.");
                return false;
            }
            return true;
        }

        private static bool IsGHubRunning()
        {
            string[] names = { "lghub", "lghub_agent", "lghub_system_tray" };
            return names.Any(name => Process.GetProcessesByName(name).Length > 0);
        }

        private static string Normalize(string path)
        {
            try { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar); }
            catch { return path.Trim(); }
        }

        private void ShowError(string message)
            => MessageBox.Show(this, message, "G Hub XBOX Profile Fixer", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    internal sealed class GameViewModel : INotifyPropertyChanged
    {
        private bool selected;
        public XboxGame Game { get; }
        public string Name => Game.Name;
        public string ExecutablePath => Game.ExecutablePath;
        public ImageSource Icon { get; }
        public bool CanSelect => !Game.AlreadyRegistered;
        public string Status => Game.AlreadyRegistered ? "Already added" : "Missing from G Hub";
        public Brush StatusBrush => Game.AlreadyRegistered ? new SolidColorBrush(Color.FromRgb(102, 106, 102)) : new SolidColorBrush(Color.FromRgb(47, 143, 91));
        public bool Selected
        {
            get => selected;
            set { if (selected == value) return; selected = value; OnPropertyChanged(); }
        }

        public GameViewModel(XboxGame game, ImageSource icon)
        {
            Game = game;
            Icon = icon;
            selected = !game.AlreadyRegistered;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
