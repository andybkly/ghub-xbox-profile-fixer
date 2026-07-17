using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GHubProfileUtility
{
    internal sealed class MainForm : Form
    {
        private readonly TextBox databasePath = new TextBox();
        private readonly Label globalStatus = new Label();
        private readonly ListView gameList = new ListView();
        private readonly Label rootsLabel = new Label();
        private readonly Button scanButton = new Button();
        private readonly Button createXboxButton = new Button();
        private readonly List<string> customRoots = new List<string>();

        public MainForm()
        {
            Text = "G Hub Profile Utility (Unofficial)";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(820, 600);
            Size = new Size(920, 680);
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.White;
            BuildInterface();
            string defaultDb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LGHUB", "settings.db");
            if (File.Exists(defaultDb)) databasePath.Text = defaultDb;
            RefreshRootsLabel();
            UpdateGHubStatus();
        }

        private void BuildInterface()
        {
            var page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(18) };
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(page);

            var title = new Label
            {
                Text = "G Hub Profile Utility",
                Font = new Font("Segoe UI Semibold", 18F),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            page.Controls.Add(title);

            var sourcePanel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 6, 0, 14) };
            sourcePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            sourcePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            sourcePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            sourcePanel.Controls.Add(new Label { Text = "LGHUB settings.db", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 10, 0) }, 0, 0);
            databasePath.Dock = DockStyle.Fill;
            sourcePanel.Controls.Add(databasePath, 1, 0);
            var browse = new Button { Text = "Browse…", AutoSize = true, Margin = new Padding(8, 0, 0, 0) };
            browse.Click += BrowseDatabase;
            sourcePanel.Controls.Add(browse, 2, 0);
            page.Controls.Add(sourcePanel);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildTransferTab());
            tabs.TabPages.Add(BuildXboxTab());
            page.Controls.Add(tabs);

            globalStatus.AutoSize = true;
            globalStatus.ForeColor = Color.DarkRed;
            globalStatus.Margin = new Padding(0, 10, 0, 0);
            page.Controls.Add(globalStatus);
        }

        private TabPage BuildTransferTab()
        {
            var tab = new TabPage("Transfer G502 X Profiles") { Padding = new Padding(18), BackColor = Color.White };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tab.Controls.Add(layout);
            layout.Controls.Add(new Label
            {
                AutoSize = true,
                MaximumSize = new Size(780, 0),
                Text = "Copies button, scroll-wheel and G-Shift assignments from wired G502 X profiles to their matching G502 X LIGHTSPEED profiles. DPI, polling and onboard settings remain separate."
            });
            var notes = new Label
            {
                AutoSize = true,
                ForeColor = Color.DimGray,
                Margin = new Padding(0, 18, 0, 0),
                Text = "The original database is never overwritten. You choose where to save the converted copy."
            };
            layout.Controls.Add(notes);
            var convert = new Button { Text = "Analyse and create converted copy…", AutoSize = true, Padding = new Padding(8, 5, 8, 5), Anchor = AnchorStyles.Left };
            convert.Click += ConvertG502;
            layout.Controls.Add(convert);
            return tab;
        }

        private TabPage BuildXboxTab()
        {
            var tab = new TabPage("Fix Xbox Game Pass Profiles") { Padding = new Padding(14), BackColor = Color.White };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tab.Controls.Add(layout);
            layout.Controls.Add(new Label
            {
                AutoSize = true,
                MaximumSize = new Size(800, 0),
                Text = "Finds Xbox games that G Hub has missed and creates proper application/profile records for the selected titles. Game files are not changed."
            });
            rootsLabel.AutoSize = true;
            rootsLabel.ForeColor = Color.DimGray;
            rootsLabel.Margin = new Padding(0, 10, 0, 8);
            layout.Controls.Add(rootsLabel);

            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 8) };
            scanButton.Text = "Scan Xbox games";
            scanButton.AutoSize = true;
            scanButton.Padding = new Padding(7, 3, 7, 3);
            scanButton.Click += async (s, e) => await ScanGames();
            buttons.Controls.Add(scanButton);
            var addRoot = new Button { Text = "Add install folder…", AutoSize = true, Padding = new Padding(5, 3, 5, 3) };
            addRoot.Click += AddInstallFolder;
            buttons.Controls.Add(addRoot);
            var addExe = new Button { Text = "Add executable manually…", AutoSize = true, Padding = new Padding(5, 3, 5, 3) };
            addExe.Click += AddExecutable;
            buttons.Controls.Add(addExe);
            layout.Controls.Add(buttons);

            gameList.Dock = DockStyle.Fill;
            gameList.View = View.Details;
            gameList.CheckBoxes = true;
            gameList.FullRowSelect = true;
            gameList.GridLines = true;
            gameList.HideSelection = false;
            gameList.Columns.Add("Game", 220);
            gameList.Columns.Add("Executable", 430);
            gameList.Columns.Add("Status", 120);
            layout.Controls.Add(gameList);

            createXboxButton.Text = "Create fixed database…";
            createXboxButton.AutoSize = true;
            createXboxButton.Padding = new Padding(8, 5, 8, 5);
            createXboxButton.Margin = new Padding(0, 10, 0, 0);
            createXboxButton.Enabled = false;
            createXboxButton.Click += CreateXboxDatabase;
            layout.Controls.Add(createXboxButton);
            return tab;
        }

        private void BrowseDatabase(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog
            {
                Title = "Select LGHUB settings.db (close G Hub first)",
                Filter = "LGHUB settings.db|settings.db|Database files (*.db)|*.db|All files (*.*)|*.*"
            })
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LGHUB");
                if (Directory.Exists(folder)) dialog.InitialDirectory = folder;
                if (dialog.ShowDialog(this) == DialogResult.OK) databasePath.Text = dialog.FileName;
            }
        }

        private bool ValidateSource()
        {
            if (!File.Exists(databasePath.Text))
            {
                ShowError("Select a valid LGHUB settings.db first.");
                return false;
            }
            if (IsGHubRunning())
            {
                ShowError("Logitech G Hub is still running. Quit it from the system tray and close lghub.exe and lghub_agent.exe before continuing.");
                UpdateGHubStatus();
                return false;
            }
            return true;
        }

        private void ConvertG502(object sender, EventArgs e)
        {
            if (!ValidateSource()) return;
            try
            {
                var db = GHubDatabase.Load(databasePath.Text);
                TransferResult result = db.TransferG502Assignments();
                if (result.AssignmentCount == 0)
                {
                    MessageBox.Show(this, "No differing G502 X assignments were found. The LIGHTSPEED profiles already match.", "Nothing to convert", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                string message = String.Format("Found {0} assignments to transfer across {1} profiles.\r\n\r\nCreate a converted copy now?", result.AssignmentCount, result.ProfileCount);
                if (MessageBox.Show(this, message, "Ready to convert", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                string output = ChooseOutput("settings-G502X-Lightspeed-converted.db");
                if (output == null) return;
                db.SaveCopy(databasePath.Text, output);
                MessageBox.Show(this, String.Format("Conversion complete.\r\n\r\nUpdated {0} assignments across {1} profiles.\r\n\r\nCreated:\r\n{2}\r\n\r\nYour original database was not changed.", result.AssignmentCount, result.ProfileCount, output), "Conversion complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { ShowError("Conversion failed. Your original database was not changed.\r\n\r\n" + ex.Message); }
        }

        private async Task ScanGames()
        {
            if (!File.Exists(databasePath.Text)) { ShowError("Select a valid LGHUB settings.db first."); return; }
            scanButton.Enabled = false;
            createXboxButton.Enabled = false;
            gameList.Items.Clear();
            globalStatus.Text = "Scanning Xbox game folders…";
            try
            {
                var roots = XboxScanner.FindXboxRoots().Concat(customRoots).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var games = await Task.Run(() => XboxScanner.Scan(roots));
                var db = GHubDatabase.Load(databasePath.Text);
                HashSet<string> registered = db.RegisteredPaths();
                foreach (XboxGame game in games)
                {
                    game.AlreadyRegistered = registered.Contains(Path.GetFullPath(game.ExecutablePath).TrimEnd(Path.DirectorySeparatorChar));
                    AddGameRow(game);
                }
                globalStatus.Text = games.Count == 0 ? "No Xbox games were found. Add the XboxGames folder manually if it uses a custom location." : String.Format("Found {0} game executable{1}.", games.Count, games.Count == 1 ? "" : "s");
                globalStatus.ForeColor = games.Count == 0 ? Color.DarkRed : Color.DarkGreen;
                createXboxButton.Enabled = games.Any(g => !g.AlreadyRegistered);
                RefreshRootsLabel();
            }
            catch (Exception ex) { ShowError("The Xbox game scan failed.\r\n\r\n" + ex.Message); }
            finally { scanButton.Enabled = true; }
        }

        private void AddGameRow(XboxGame game)
        {
            var item = new ListViewItem(game.Name) { Tag = game, Checked = !game.AlreadyRegistered };
            item.SubItems.Add(game.ExecutablePath);
            item.SubItems.Add(game.AlreadyRegistered ? "Already in G Hub" : game.ManifestDetected ? "Missing" : "Check executable");
            if (game.AlreadyRegistered) item.ForeColor = Color.DimGray;
            gameList.Items.Add(item);
        }

        private void AddInstallFolder(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog { Description = "Select an XboxGames folder, a game folder, or its Content folder" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (!customRoots.Contains(dialog.SelectedPath, StringComparer.OrdinalIgnoreCase)) customRoots.Add(dialog.SelectedPath);
                RefreshRootsLabel();
            }
        }

        private void AddExecutable(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { Title = "Select the game's main executable", Filter = "Applications (*.exe)|*.exe" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var game = new XboxGame
                {
                    Name = Path.GetFileNameWithoutExtension(dialog.FileName),
                    ExecutablePath = dialog.FileName,
                    SourceFolder = Path.GetDirectoryName(dialog.FileName),
                    ManifestDetected = false
                };
                AddGameRow(game);
                createXboxButton.Enabled = true;
            }
        }

        private void CreateXboxDatabase(object sender, EventArgs e)
        {
            if (!ValidateSource()) return;
            var selected = gameList.CheckedItems.Cast<ListViewItem>().Select(i => i.Tag as XboxGame).Where(g => g != null && !g.AlreadyRegistered).ToList();
            if (selected.Count == 0) { MessageBox.Show(this, "Select at least one missing game first.", "Nothing selected", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            try
            {
                var db = GHubDatabase.Load(databasePath.Text);
                int added = db.AddXboxGames(selected);
                if (added == 0) { MessageBox.Show(this, "The selected games are already registered in this database.", "Nothing to add", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                string output = ChooseOutput("settings-Xbox-profiles-fixed.db");
                if (output == null) return;
                db.SaveCopy(databasePath.Text, output);
                MessageBox.Show(this, String.Format("Created {0} new G Hub profile{1}.\r\n\r\nCreated:\r\n{2}\r\n\r\nYour original database was not changed.", added, added == 1 ? "" : "s", output), "Fixed database created", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { ShowError("The fixed database could not be created. Your original database was not changed.\r\n\r\n" + ex.Message); }
        }

        private string ChooseOutput(string filename)
        {
            using (var dialog = new SaveFileDialog
            {
                Title = "Save the converted database",
                Filter = "Database files (*.db)|*.db",
                InitialDirectory = Path.GetDirectoryName(databasePath.Text),
                FileName = filename
            })
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
        }

        private void RefreshRootsLabel()
        {
            var roots = XboxScanner.FindXboxRoots().Concat(customRoots).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            rootsLabel.Text = roots.Count == 0 ? "No XboxGames folders detected yet." : "Install folders: " + String.Join("  •  ", roots);
        }

        private static bool IsGHubRunning()
        {
            string[] names = { "lghub", "lghub_agent", "lghub_system_tray" };
            return names.Any(name => Process.GetProcessesByName(name).Length > 0);
        }

        private void UpdateGHubStatus()
        {
            bool running = IsGHubRunning();
            globalStatus.Text = running ? "G Hub is running. Quit it completely before creating a database." : "G Hub does not appear to be running.";
            globalStatus.ForeColor = running ? Color.DarkRed : Color.DarkGreen;
        }

        private void ShowError(string message) => MessageBox.Show(this, message, "G Hub Profile Utility", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
