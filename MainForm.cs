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
        private static readonly Color Canvas = Color.FromArgb(247, 247, 245);
        private static readonly Color Surface = Color.White;
        private static readonly Color Ink = Color.FromArgb(23, 24, 23);
        private static readonly Color Muted = Color.FromArgb(102, 106, 102);
        private static readonly Color Border = Color.FromArgb(227, 229, 226);
        private static readonly Color Green = Color.FromArgb(47, 143, 91);
        private static readonly Color PaleGreen = Color.FromArgb(237, 246, 240);

        private readonly TextBox databasePath = new TextBox();
        private readonly Panel mainHost = new Panel();
        private readonly Label[] stepLabels = new Label[3];
        private readonly ListView gameList = new ListView();
        private readonly ImageList gameIcons = new ImageList();
        private readonly Label selectedCount = new Label();
        private readonly Label scanSummary = new Label();
        private readonly Label databaseReady = new Label();
        private readonly List<string> customRoots = new List<string>();
        private readonly List<XboxGame> discoveredGames = new List<XboxGame>();
        private int currentStep = 1;

        public MainForm()
        {
            Text = "G Hub XBOX Game Pass Profile Fixer (Unofficial)";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(920, 650);
            Size = new Size(1120, 760);
            Font = new Font("Segoe UI", 9.5F);
            BackColor = Canvas;
            ForeColor = Ink;
            AutoScaleMode = AutoScaleMode.Dpi;

            string defaultDb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LGHUB", "settings.db");
            if (File.Exists(defaultDb)) databasePath.Text = defaultDb;

            BuildWindow();
            ShowStep(1);
        }

        private void BuildWindow()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Surface, Padding = new Padding(24, 0, 24, 0) };
            header.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, header.Height - 1, header.Width, header.Height - 1);
            Controls.Add(header);

            var mark = new Label
            {
                Text = "↗",
                Font = new Font("Segoe UI Semibold", 17F),
                ForeColor = Green,
                AutoSize = true,
                Location = new Point(24, 16)
            };
            header.Controls.Add(mark);
            var appName = new Label
            {
                Text = "G Hub XBOX Game Pass Profile Fixer",
                Font = new Font("Segoe UI Semibold", 13F),
                AutoSize = true,
                Location = new Point(60, 19)
            };
            header.Controls.Add(appName);
            var unofficial = new Label
            {
                Text = "Unofficial",
                ForeColor = Muted,
                BackColor = Canvas,
                AutoSize = true,
                Padding = new Padding(8, 4, 8, 4),
                Location = new Point(354, 16)
            };
            header.Controls.Add(unofficial);

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Canvas };
            Controls.Add(body);
            header.BringToFront();

            var sidebar = new Panel { Dock = DockStyle.Left, Width = 230, BackColor = Surface, Padding = new Padding(18, 42, 18, 18) };
            sidebar.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
            body.Controls.Add(sidebar);
            for (int i = 0; i < 3; i++)
            {
                int step = i + 1;
                var label = new Label
                {
                    Text = step + "     " + (step == 1 ? "G Hub database" : step == 2 ? "XBOX games" : "Create copy"),
                    Font = new Font("Segoe UI Semibold", 10F),
                    AutoSize = false,
                    Height = 58,
                    Dock = DockStyle.Top,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(14, 0, 0, 0),
                    Cursor = Cursors.Hand,
                    Tag = step
                };
                label.Click += (s, e) => NavigateFromSidebar((int)((Label)s).Tag);
                stepLabels[i] = label;
            }
            sidebar.Controls.Add(stepLabels[2]);
            sidebar.Controls.Add(stepLabels[1]);
            sidebar.Controls.Add(stepLabels[0]);

            mainHost.Dock = DockStyle.Fill;
            mainHost.BackColor = Canvas;
            mainHost.Padding = new Padding(34, 30, 34, 28);
            body.Controls.Add(mainHost);
            sidebar.BringToFront();
        }

        private void NavigateFromSidebar(int step)
        {
            if (step == 1) ShowStep(1);
            else if (step == 2 && File.Exists(databasePath.Text)) ShowStep(2);
            else if (step == 3 && gameList.CheckedItems.Count > 0) ShowStep(3);
        }

        private void ShowStep(int step)
        {
            currentStep = step;
            for (int i = 0; i < stepLabels.Length; i++)
            {
                bool active = i == step - 1;
                bool complete = i < step - 1;
                stepLabels[i].BackColor = active ? PaleGreen : Surface;
                stepLabels[i].ForeColor = active || complete ? Green : Ink;
                string name = i == 0 ? "G Hub database" : i == 1 ? "XBOX games" : "Create copy";
                stepLabels[i].Text = complete ? "✓     " + name : (i + 1) + "     " + name;
            }
            mainHost.Controls.Clear();
            if (step == 1) BuildDatabaseStep();
            else if (step == 2) BuildGamesStep();
            else BuildCreateStep();
        }

        private void BuildDatabaseStep()
        {
            var layout = NewPageLayout(4);
            layout.Controls.Add(PageHeading("G Hub database", "Select the G Hub settings database you want to create a fixed copy from."));

            var card = NewCard(132);
            var name = new Label { Text = "LGHUB settings.db", Font = new Font("Segoe UI Semibold", 11F), AutoSize = true, Location = new Point(22, 22) };
            card.Controls.Add(name);
            databasePath.BorderStyle = BorderStyle.FixedSingle;
            databasePath.BackColor = Surface;
            databasePath.Font = new Font("Segoe UI", 10F);
            databasePath.Location = new Point(22, 55);
            databasePath.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            databasePath.Width = card.Width - 154;
            card.Resize += (s, e) => databasePath.Width = Math.Max(300, card.ClientSize.Width - 154);
            card.Controls.Add(databasePath);
            var browse = SecondaryButton("Browse…", 96);
            browse.Location = new Point(card.Width - 116, 52);
            browse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            browse.Click += BrowseDatabase;
            card.Controls.Add(browse);
            databaseReady.Text = File.Exists(databasePath.Text) ? "✓  Ready" : "Select your settings.db";
            databaseReady.ForeColor = File.Exists(databasePath.Text) ? Green : Muted;
            databaseReady.AutoSize = true;
            databaseReady.Location = new Point(22, 92);
            card.Controls.Add(databaseReady);
            layout.Controls.Add(card);

            var note = new Label
            {
                Text = "Your original database will only be read. The app always creates a separate copy.",
                ForeColor = Muted,
                AutoSize = true,
                Margin = new Padding(0, 14, 0, 0)
            };
            layout.Controls.Add(note);
            var next = PrimaryButton("Continue", 150);
            next.Anchor = AnchorStyles.Right;
            next.Click += async (s, e) =>
            {
                if (!File.Exists(databasePath.Text)) { ShowError("Select a valid LGHUB settings.db first."); return; }
                ShowStep(2);
                if (discoveredGames.Count == 0) await ScanGames();
            };
            layout.Controls.Add(next);
            mainHost.Controls.Add(layout);
        }

        private void BuildGamesStep()
        {
            var layout = NewPageLayout(5);
            layout.RowStyles[3].SizeType = SizeType.Percent;
            layout.RowStyles[3].Height = 100F;
            layout.Controls.Add(PageHeading("XBOX games", "Select the games you want to add to G Hub."));
            layout.Controls.Add(BuildDatabaseStrip());
            layout.Controls.Add(BuildScanToolbar());
            layout.Controls.Add(BuildGameList());
            layout.Controls.Add(BuildGamesFooter());
            mainHost.Controls.Add(layout);
            PopulateGameList();
        }

        private Control BuildDatabaseStrip()
        {
            var strip = NewCard(82);
            var dbName = new Label { Text = "settings.db", Font = new Font("Segoe UI Semibold", 10.5F), AutoSize = true, Location = new Point(20, 16) };
            strip.Controls.Add(dbName);
            var path = new Label { Text = databasePath.Text, ForeColor = Muted, AutoEllipsis = true, Location = new Point(20, 42), Width = 500 };
            path.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            strip.Controls.Add(path);
            var ready = new Label { Text = "✓  Ready", ForeColor = Green, AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            ready.Location = new Point(strip.Width - 176, 28);
            strip.Controls.Add(ready);
            var change = SecondaryButton("Change", 82);
            change.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            change.Location = new Point(strip.Width - 104, 20);
            change.Click += (s, e) => ShowStep(1);
            strip.Controls.Add(change);
            strip.Resize += (s, e) =>
            {
                path.Width = Math.Max(260, strip.ClientSize.Width - 250);
                ready.Left = strip.ClientSize.Width - 176;
                change.Left = strip.ClientSize.Width - 104;
            };
            return strip;
        }

        private Control BuildScanToolbar()
        {
            var bar = new FlowLayoutPanel { Height = 50, Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 14, 0, 8), WrapContents = false };
            var scan = SecondaryButton("↻  Scan again", 120);
            scan.Click += async (s, e) => await ScanGames();
            bar.Controls.Add(scan);
            var addFolder = SecondaryButton("Add folder…", 108);
            addFolder.Click += AddInstallFolder;
            bar.Controls.Add(addFolder);
            var addExe = SecondaryButton("Add executable…", 128);
            addExe.Click += AddExecutable;
            bar.Controls.Add(addExe);
            scanSummary.Text = discoveredGames.Count == 0 ? "Not scanned yet" : discoveredGames.Count + " games found";
            scanSummary.ForeColor = Muted;
            scanSummary.AutoSize = true;
            scanSummary.Margin = new Padding(12, 10, 0, 0);
            bar.Controls.Add(scanSummary);
            return bar;
        }

        private Control BuildGameList()
        {
            gameIcons.ColorDepth = ColorDepth.Depth32Bit;
            gameIcons.ImageSize = new Size(40, 40);
            gameList.Clear();
            gameList.Dock = DockStyle.Fill;
            gameList.View = View.Details;
            gameList.CheckBoxes = true;
            gameList.FullRowSelect = true;
            gameList.GridLines = false;
            gameList.HideSelection = false;
            gameList.BorderStyle = BorderStyle.FixedSingle;
            gameList.BackColor = Surface;
            gameList.ForeColor = Ink;
            gameList.SmallImageList = gameIcons;
            gameList.Columns.Add("GAME", 210);
            gameList.Columns.Add("EXECUTABLE", 430);
            gameList.Columns.Add("STATUS", 155);
            gameList.ItemCheck -= GameListItemCheck;
            gameList.ItemCheck += GameListItemCheck;
            gameList.Resize += (s, e) =>
            {
                int available = gameList.ClientSize.Width - gameList.Columns[0].Width - gameList.Columns[2].Width - 6;
                if (available > 250) gameList.Columns[1].Width = available;
            };
            return gameList;
        }

        private Control BuildGamesFooter()
        {
            var footer = new TableLayoutPanel { Height = 72, Dock = DockStyle.Fill, ColumnCount = 3, Margin = new Padding(0, 12, 0, 0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            selectedCount.Text = "0 games selected";
            selectedCount.Font = new Font("Segoe UI Semibold", 10F);
            selectedCount.AutoSize = true;
            selectedCount.Anchor = AnchorStyles.Left;
            footer.Controls.Add(selectedCount, 0, 0);
            var safety = new Label { Text = "◇  Your original database will not be changed.", ForeColor = Muted, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(24, 0, 0, 0) };
            footer.Controls.Add(safety, 1, 0);
            var next = PrimaryButton("Continue", 150);
            next.Anchor = AnchorStyles.Right;
            next.Click += (s, e) =>
            {
                if (gameList.CheckedItems.Count == 0) { ShowError("Select at least one missing game first."); return; }
                ShowStep(3);
            };
            footer.Controls.Add(next, 2, 0);
            return footer;
        }

        private void BuildCreateStep()
        {
            var layout = NewPageLayout(5);
            layout.Controls.Add(PageHeading("Create fixed copy", "Review the selection, then create a new G Hub database."));
            var summary = NewCard(142);
            var count = new Label { Text = gameList.CheckedItems.Count + (gameList.CheckedItems.Count == 1 ? " game will be added" : " games will be added"), Font = new Font("Segoe UI Semibold", 12F), AutoSize = true, Location = new Point(22, 22) };
            summary.Controls.Add(count);
            var names = new Label
            {
                Text = String.Join("  •  ", gameList.CheckedItems.Cast<ListViewItem>().Select(i => ((XboxGame)i.Tag).Name)),
                ForeColor = Muted,
                AutoEllipsis = true,
                Location = new Point(22, 55),
                Width = summary.Width - 44,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            summary.Controls.Add(names);
            var source = new Label { Text = "Source: " + databasePath.Text, ForeColor = Muted, AutoEllipsis = true, Location = new Point(22, 88), Width = summary.Width - 44, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            summary.Controls.Add(source);
            layout.Controls.Add(summary);
            var safety = new Label
            {
                Text = "✓  The original settings.db will not be overwritten. The new copy must pass SQLite integrity and profile relationship checks.",
                ForeColor = Green,
                AutoSize = true,
                MaximumSize = new Size(720, 0),
                Margin = new Padding(0, 16, 0, 0)
            };
            layout.Controls.Add(safety);
            var create = PrimaryButton("Create fixed database…", 210);
            create.Click += CreateXboxDatabase;
            layout.Controls.Add(create);
            var back = new LinkLabel { Text = "Back to XBOX games", LinkColor = Green, AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
            back.Click += (s, e) => ShowStep(2);
            layout.Controls.Add(back);
            mainHost.Controls.Add(layout);
        }

        private async Task ScanGames()
        {
            scanSummary.Text = "Scanning XBOX game folders…";
            try
            {
                var roots = XboxScanner.FindXboxRoots().Concat(customRoots).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var games = await Task.Run(() => XboxScanner.Scan(roots));
                var db = GHubDatabase.Load(databasePath.Text);
                HashSet<string> registered = db.RegisteredPaths();
                foreach (XboxGame game in games)
                    game.AlreadyRegistered = registered.Contains(Path.GetFullPath(game.ExecutablePath).TrimEnd(Path.DirectorySeparatorChar));
                discoveredGames.Clear();
                discoveredGames.AddRange(games);
                PopulateGameList();
                scanSummary.Text = games.Count == 0 ? "No XBOX games found" : games.Count + (games.Count == 1 ? " game found" : " games found");
            }
            catch (Exception ex) { ShowError("The XBOX game scan failed.\r\n\r\n" + ex.Message); }
        }

        private void PopulateGameList()
        {
            if (currentStep != 2 || gameList.Parent == null) return;
            gameList.BeginUpdate();
            gameList.Items.Clear();
            gameIcons.Images.Clear();
            foreach (XboxGame game in discoveredGames)
            {
                string imageKey = game.ExecutablePath;
                try
                {
                    using (Icon icon = Icon.ExtractAssociatedIcon(game.ExecutablePath))
                        if (icon != null) gameIcons.Images.Add(imageKey, icon.ToBitmap());
                }
                catch { }
                var item = new ListViewItem(game.Name) { Tag = game, Checked = !game.AlreadyRegistered, ImageKey = gameIcons.Images.ContainsKey(imageKey) ? imageKey : String.Empty };
                item.SubItems.Add(game.ExecutablePath);
                item.SubItems.Add(game.AlreadyRegistered ? "Already added" : "Missing from G Hub");
                if (game.AlreadyRegistered) item.ForeColor = Muted;
                gameList.Items.Add(item);
            }
            gameList.EndUpdate();
            UpdateSelectedCount();
        }

        private void GameListItemCheck(object sender, ItemCheckEventArgs e)
            => BeginInvoke(new Action(UpdateSelectedCount));

        private void UpdateSelectedCount()
        {
            int count = gameList.CheckedItems.Cast<ListViewItem>().Count(i => !((XboxGame)i.Tag).AlreadyRegistered);
            selectedCount.Text = count + (count == 1 ? " game selected" : " games selected");
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
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    databasePath.Text = dialog.FileName;
                    databaseReady.Text = "✓  Ready";
                    databaseReady.ForeColor = Green;
                }
            }
        }

        private void AddInstallFolder(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog { Description = "Select an XBOXGames folder, a game folder, or its Content folder" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (!customRoots.Contains(dialog.SelectedPath, StringComparer.OrdinalIgnoreCase)) customRoots.Add(dialog.SelectedPath);
            }
        }

        private void AddExecutable(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { Title = "Select the game's main executable", Filter = "Applications (*.exe)|*.exe" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                discoveredGames.Add(new XboxGame
                {
                    Name = Path.GetFileNameWithoutExtension(dialog.FileName),
                    ExecutablePath = dialog.FileName,
                    SourceFolder = Path.GetDirectoryName(dialog.FileName),
                    ManifestDetected = false
                });
                PopulateGameList();
                scanSummary.Text = discoveredGames.Count + " games found";
            }
        }

        private void CreateXboxDatabase(object sender, EventArgs e)
        {
            if (!ValidateSource()) return;
            var selected = gameList.CheckedItems.Cast<ListViewItem>().Select(i => i.Tag as XboxGame).Where(g => g != null && !g.AlreadyRegistered).ToList();
            if (selected.Count == 0) { ShowError("Select at least one missing game first."); return; }
            try
            {
                var db = GHubDatabase.Load(databasePath.Text);
                int added = db.AddXboxGames(selected);
                if (added == 0) { MessageBox.Show(this, "The selected games are already registered in this database.", "Nothing to add", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                string output = ChooseOutput("settings-XBOX-profiles-fixed.db");
                if (output == null) return;
                db.SaveCopy(databasePath.Text, output);
                MessageBox.Show(this, String.Format("Created {0} new G Hub profile{1}.\r\n\r\nCreated:\r\n{2}\r\n\r\nYour original database was not changed.", added, added == 1 ? "" : "s", output), "Fixed database created", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { ShowError("The fixed database could not be created. Your original database was not changed.\r\n\r\n" + ex.Message); }
        }

        private bool ValidateSource()
        {
            if (!File.Exists(databasePath.Text)) { ShowError("Select a valid LGHUB settings.db first."); return false; }
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

        private string ChooseOutput(string filename)
        {
            using (var dialog = new SaveFileDialog
            {
                Title = "Save the fixed database",
                Filter = "Database files (*.db)|*.db",
                InitialDirectory = Path.GetDirectoryName(databasePath.Text),
                FileName = filename
            })
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
        }

        private TableLayoutPanel NewPageLayout(int rows)
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = rows, BackColor = Canvas };
            for (int i = 0; i < rows; i++) panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return panel;
        }

        private Control PageHeading(string title, string subtitle)
        {
            var panel = new Panel { Height = 92, Dock = DockStyle.Fill };
            panel.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI Semibold", 22F), AutoSize = true, Location = new Point(0, 0) });
            panel.Controls.Add(new Label { Text = subtitle, ForeColor = Muted, Font = new Font("Segoe UI", 10.5F), AutoSize = true, Location = new Point(2, 49) });
            return panel;
        }

        private Panel NewCard(int height)
        {
            var panel = new Panel { Height = height, Dock = DockStyle.Fill, BackColor = Surface, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 0, 0, 0) };
            return panel;
        }

        private Button PrimaryButton(string text, int width)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = 44,
                FlatStyle = FlatStyle.Flat,
                BackColor = Green,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10F),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private Button SecondaryButton(string text, int width)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = 38,
                FlatStyle = FlatStyle.Flat,
                BackColor = Surface,
                ForeColor = Ink,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(190, 194, 190);
            return button;
        }

        private void ShowError(string message) => MessageBox.Show(this, message, "G Hub XBOX Game Pass Profile Fixer", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
