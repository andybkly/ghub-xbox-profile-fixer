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

namespace GHubXBOXProfileFixer
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<GameViewModel> games = new ObservableCollection<GameViewModel>();
        private readonly List<string> customRoots = new List<string>();
        private int currentStep = 1;
        private bool scanning;
        private string lastBackupPath;
        private readonly List<XboxGame> manualGames = new List<XboxGame>();

        public MainWindow()
        {
            InitializeComponent();
            GamesItemsControl.ItemsSource = games;
            string defaultDb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LGHUB", "settings.db");
            if (File.Exists(defaultDb))
            {
                DatabasePathBox.Text = defaultDb;
                SetDatabaseReady(false);
            }
            ShowStep(1);
            UpdateSelectedCount();
        }

        private void ShowStep(int step)
        {
            currentStep = step;
            DatabasePage.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
            GamesPage.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
            CreatePage.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
            CompletionPage.Visibility = step == 4 ? Visibility.Visible : Visibility.Collapsed;

            SetNavState(Step1Nav, Step1NavText, step == 1, step > 1, "1", "Settings file");
            SetNavState(Step2Nav, Step2NavText, step == 2, step > 2, "2", "XBOX games");
            SetNavState(Step3Nav, Step3NavText, step == 3 || step == 4, step == 4, "3", "Add profiles");
        }

        private void SetNavState(System.Windows.Controls.Button border, System.Windows.Controls.TextBlock text, bool active, bool complete, string number, string label)
        {
            border.BorderThickness = new Thickness(active ? 1 : 0);
            border.BorderBrush = active ? new SolidColorBrush(Color.FromRgb(57, 101, 57)) : Brushes.Transparent;
            border.Background = active ? (Brush)FindResource("PaleGreenBrush") : Brushes.Transparent;
            text.Foreground = active || complete ? (Brush)FindResource("GreenBrush") : (Brush)FindResource("InkBrush");
            text.Text = (complete ? "✓" : number) + "     " + label;
            border.Cursor = Cursors.Hand;
        }

        private void Step1Nav_Click(object sender, RoutedEventArgs e) => ShowStep(1);
        private void Step2Nav_Click(object sender, RoutedEventArgs e)
        {
            DatabaseContinue_Click(sender, e);
        }
        private void Step3Nav_Click(object sender, RoutedEventArgs e)
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
                SetDatabaseReady(false);
            }
        }

        private void SetDatabaseReady(bool ready)
        {
            DatabaseStatusText.Text = ready ? "Validated G HUB database" : "Close G HUB, then continue to validate this file.";
            DatabaseStatusText.Foreground = ready ? (Brush)FindResource("GreenBrush") : (Brush)FindResource("MutedBrush");
        }

        private async void DatabaseContinue_Click(object sender, RoutedEventArgs e)
        {
            if (scanning) return;
            if (!ValidateSource()) return;
            if (!File.Exists(DatabasePathBox.Text))
            {
                ShowError("Select a valid LGHUB settings.db first.");
                return;
            }
            try { RefreshRegistration(); }
            catch (Exception ex) { ShowError("That file is not a valid G Hub database.\n\n" + ex.Message); return; }
            SetDatabaseReady(true);
            DatabasePathSummary.Text = DatabasePathBox.Text;
            ShowStep(2);
            if (games.Count == 0) await ScanGames();
        }

        private void ChangeDatabase_Click(object sender, RoutedEventArgs e) => ShowStep(1);
        private async void ScanAgain_Click(object sender, RoutedEventArgs e) => await ScanGames();

        private async Task ScanGames()
        {
            if (scanning || !ValidateSource()) return;
            scanning = true;
            Workspace.IsEnabled = false;
            ScanSummaryText.Text = "Scanning game folders…";
            try
            {
                string databasePath = DatabasePathBox.Text;
                var extraRoots = customRoots.ToList();
                var previous = games.ToDictionary(g => Normalize(g.ExecutablePath), g => g.Selected, StringComparer.OrdinalIgnoreCase);
                var found = await Task.Run(() => XboxScanner.Scan(XboxScanner.FindXboxRoots().Concat(extraRoots)));
                var database = GHubDatabase.Load(databasePath);
                HashSet<string> registered = database.RegisteredPaths();
                var merged = found.Concat(manualGames).GroupBy(g => Normalize(g.ExecutablePath), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First()).OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                games.Clear();
                foreach (XboxGame game in merged)
                {
                    game.AlreadyRegistered = registered.Contains(Normalize(game.ExecutablePath));
                    var model = new GameViewModel(game, GameIconLoader.Load(game));
                    bool selected;
                    if (previous.TryGetValue(Normalize(game.ExecutablePath), out selected)) model.Selected = selected && model.CanSelect;
                    games.Add(model);
                }
                ScanSummaryText.Text = games.Count + (games.Count == 1 ? " game found" : " games found");
            }
            catch (Exception ex)
            {
                ScanSummaryText.Text = "Scan failed — try again";
                ShowError("The game scan failed.\n\n" + ex.Message);
            }
            finally
            {
                scanning = false;
                Workspace.IsEnabled = true;
                UpdateSelectedCount();
            }
        }

        private void RefreshRegistration()
        {
            var registered = GHubDatabase.Load(DatabasePathBox.Text).RegisteredPaths();
            foreach (var game in games) game.SetRegistered(registered.Contains(Normalize(game.ExecutablePath)));
            UpdateSelectedCount();
        }

        private void DatabasePathChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!IsInitialized) return;
            SetDatabaseReady(false);
            games.Clear();
            manualGames.Clear();
            UpdateSelectedCount();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool select = games.Any(g => g.CanSelect && !g.Selected);
            foreach (var game in games.Where(g => g.CanSelect)) game.Selected = select;
            UpdateSelectedCount();
        }

        private async void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new Forms.FolderBrowserDialog { Description = "Select an XBOX game library, game folder, or Content folder" })
            {
                if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
                if (!customRoots.Contains(dialog.SelectedPath, StringComparer.OrdinalIgnoreCase)) customRoots.Add(dialog.SelectedPath);
            }
            await ScanGames();
        }

        private void AddExecutable_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Title = "Select the game's main executable", Filter = "Applications (*.exe)|*.exe" };
            if (dialog.ShowDialog(this) != true) return;
            if (games.Any(g => String.Equals(Normalize(g.ExecutablePath), Normalize(dialog.FileName), StringComparison.OrdinalIgnoreCase))) return;
            bool alreadyRegistered;
            try { alreadyRegistered = GHubDatabase.Load(DatabasePathBox.Text).RegisteredPaths().Contains(Normalize(dialog.FileName)); }
            catch (Exception ex) { ShowError("Could not read the database.\n\n" + ex.Message); return; }
            var game = new XboxGame
            {
                Name = Path.GetFileNameWithoutExtension(dialog.FileName),
                ExecutablePath = dialog.FileName,
                SourceFolder = Path.GetDirectoryName(dialog.FileName),
                ManifestDetected = false,
                AlreadyRegistered = alreadyRegistered
            };
            manualGames.Add(game);
            games.Add(new GameViewModel(game, GameIconLoader.Load(game)));
            ScanSummaryText.Text = games.Count + (games.Count == 1 ? " game found" : " games found");
            UpdateSelectedCount();
        }

        private void GameCheckBox_Click(object sender, RoutedEventArgs e) => UpdateSelectedCount();

        private void UpdateSelectedCount()
        {
            int count = games.Count(g => g.Selected && g.CanSelect);
            SelectedCountText.Text = count + (count == 1 ? " game selected" : " games selected");
            GamesContinueButton.IsEnabled = count > 0 && !scanning;
            EmptyState.Visibility = games.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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

        private void ApplyDatabase_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateSource()) return;
            var selected = games.Where(g => g.Selected && g.CanSelect).Select(g => g.Game).ToList();
            if (selected.Count == 0) { ShowError("Select at least one missing game first."); return; }
            string backup;
            int added;
            try
            {
                var database = GHubDatabase.Load(DatabasePathBox.Text);
                added = database.AddXboxGames(selected);
                if (added == 0) { RefreshRegistration(); ShowStep(2); return; }
                backup = database.ApplyWithBackup(DatabasePathBox.Text, () =>
                {
                    if (IsGHubRunning()) throw new InvalidOperationException("G HUB is running. Quit it from the system tray before applying profiles.");
                });
            }
            catch (Exception ex) { ShowError("The update could not be completed.\n\n" + ex.Message); return; }
            lastBackupPath = backup;
            AppliedCountText.Text = added + (added == 1 ? " game profile added. No manual installation is needed." : " game profiles added. No manual installation is needed.");
            BackupSavedText.Text = "Updated database:\n" + DatabasePathBox.Text + "\n\nOriginal settings saved as:\n" + backup;
            try { RefreshRegistration(); } catch { SetDatabaseReady(false); }
            ShowStep(4);
        }

        private void ShowBackup_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start("explorer.exe", "/select,\"" + lastBackupPath + "\""); }
            catch (Exception ex) { ShowError("Could not open the backup folder.\n\n" + lastBackupPath + "\n\n" + ex.Message); }
        }

        private void OpenGHub_Click(object sender, RoutedEventArgs e)
        {
            string executable = new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 }
                .Select(folder => Path.Combine(Environment.GetFolderPath(folder), "LGHUB", "lghub.exe")).FirstOrDefault(File.Exists);
            if (executable == null)
            {
                MessageBox.Show(this, "Your profiles are installed. Open G HUB from the Windows Start menu to continue.", "Open G HUB", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try { Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true }); }
            catch (Exception ex) { ShowError("Your profiles are installed, but G HUB could not be opened. Open it from the Start menu.\n\n" + ex.Message); }
        }

        private void CreateDatabase_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateSource()) return;
            var selected = games.Where(g => g.Selected && g.CanSelect).Select(g => g.Game).ToList();
            if (selected.Count == 0) { ShowError("Select at least one missing game first."); return; }
            var save = new SaveFileDialog
            {
                Title = "Save a separate database for manual installation",
                Filter = "Database files (*.db)|*.db",
                InitialDirectory = Path.GetDirectoryName(DatabasePathBox.Text),
                FileName = "settings-XBOX-profiles-fixed.db"
            };
            if (save.ShowDialog(this) != true) return;
            if (!ValidateSource()) return;
            try
            {
                var database = GHubDatabase.Load(DatabasePathBox.Text);
                int added = database.AddXboxGames(selected);
                if (added == 0) { MessageBox.Show(this, "The selected games are already registered.", "Nothing to add", MessageBoxButton.OK, MessageBoxImage.Information); return; }
                database.SaveCopy(DatabasePathBox.Text, save.FileName);
                MessageBox.Show(this,
                    "Created " + added + (added == 1 ? " new G Hub profile.\n\n" : " new G Hub profiles.\n\n") +
                    "Saved copy:\n" + save.FileName + "\n\nThis copy has not been installed. Your current settings are unchanged." +
                    "\n\nTo install it manually: keep G HUB closed, back up the original settings.db, then copy this file into that folder and name it settings.db." +
                    "\n\nFor automatic installation, return here and choose Back up & update G HUB.",
                    "Separate copy saved — not installed", MessageBoxButton.OK, MessageBoxImage.Information);
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
            foreach (string name in names)
            {
                var processes = Process.GetProcessesByName(name);
                try { if (processes.Length > 0) return true; }
                finally { foreach (var process in processes) process.Dispose(); }
            }
            return false;
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
        public string Detection => Game.ManifestDetected ? "From manifest" : "Check executable";
        public string Status => Game.AlreadyRegistered ? "Already added" : "Missing from G Hub";
        public Brush StatusBrush => Game.AlreadyRegistered ? new SolidColorBrush(Color.FromRgb(157, 170, 185)) : new SolidColorBrush(Color.FromRgb(160, 215, 160));
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

        public void SetRegistered(bool registered)
        {
            Game.AlreadyRegistered = registered;
            if (registered) Selected = false;
            OnPropertyChanged(nameof(CanSelect));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(StatusBrush));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
