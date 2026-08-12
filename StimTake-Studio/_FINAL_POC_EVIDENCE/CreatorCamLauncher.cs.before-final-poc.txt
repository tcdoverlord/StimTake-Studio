using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using StimTakeShared;

[assembly: AssemblyTitle("StimTake Studio 6.0")]
[assembly: AssemblyProduct("StimTake Studio 6.0")]
[assembly: AssemblyCompany("Talented Creative Design and TCDOVERLORD")]
[assembly: AssemblyCopyright("Copyright 2026 Talented Creative Design and TCDOVERLORD")]
[assembly: AssemblyVersion("6.0.0.0")]
[assembly: AssemblyFileVersion("6.0.0.0")]

namespace CreatorCamOverlayKit
{
    internal static partial class Program
    {
        private const int Port = 8787;
        private const string BaseUrl = "http://127.0.0.1:8787/";
        private static Mutex instanceMutex;

        internal static string LocalDataRoot()
        {
            string isolated = (Environment.GetEnvironmentVariable("STIMTAKE_RUNTIME_ROOT") ?? "").Trim();
            if (isolated.Length > 0 && Path.IsPathRooted(isolated))
            {
                Directory.CreateDirectory(isolated);
                return Path.GetFullPath(isolated);
            }
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        [STAThread]
        private static void Main(string[] args)
        {
            bool quiet = Array.IndexOf(args, "--quiet") >= 0;
            bool created;
            instanceMutex = new Mutex(true, "CreatorCamOverlayKit.Singleton", out created);
            if (!created)
            {
                MessageBox.Show("StimTake Studio is already running. Open it from the notification-area icon.",
                    "StimTake Studio 6.0", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs eventArgs)
            {
                LogRuntimeError("Windows interface", eventArgs.Exception);
                MessageBox.Show("Creator Cam recovered from an interface error and will remain open.\n\nA diagnostic log was saved in the CreatorCamOverlayKit app-data folder.",
                    "Creator Cam Overlay Kit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs eventArgs)
            {
                Exception error = eventArgs.ExceptionObject as Exception;
                if (error != null) LogRuntimeError("Unhandled runtime error", error);
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                Application.Run(new RustyContext(quiet));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Creator Cam could not start.\n\n" + ex.Message,
                    "Creator Cam Overlay Kit", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                instanceMutex.ReleaseMutex();
                instanceMutex.Dispose();
            }
        }

        internal static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception openError)
            {
                LogRuntimeError("Open overlay preview", openError);
                try
                {
                    Clipboard.SetText(url);
                    MessageBox.Show("Creator Cam could not open the default browser, so the OBS URL was copied to the clipboard:\n\n" + url,
                        "Creator Cam Overlay Kit", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception clipboardError)
                {
                    LogRuntimeError("Copy overlay URL", clipboardError);
                    MessageBox.Show("Creator Cam could not open the default browser.\n\nOpen this address manually:\n" + url,
                        "Creator Cam Overlay Kit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        internal static void LogRuntimeError(string area, Exception error)
        {
            try
            {
                string folder = Path.Combine(LocalDataRoot(), "CreatorCamOverlayKit");
                Directory.CreateDirectory(folder);
                string entry = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + area + Environment.NewLine +
                    error.GetType().FullName + ": " + error.Message + Environment.NewLine + error.StackTrace + Environment.NewLine + Environment.NewLine;
                File.AppendAllText(Path.Combine(folder, "runtime-errors.log"), entry, Encoding.UTF8);
            }
            catch { }
        }

        private sealed class RustyContext : ApplicationContext
        {
            internal readonly StaticServer server;
            private readonly NotifyIcon tray;
            private readonly ControlDeckForm controlDeck;
            private readonly StimTakeStudioV6Form studioV6;

            internal RustyContext(bool quiet)
            {
                server = new StaticServer(Port);
                server.Start();

                // Preserve the proven Creator Cam / Backstage implementation as the
                // backend and advanced/manual tool surface.  V6 is a new front-of-house
                // shell over the same local event server, not a second competing backend.
                controlDeck = new ControlDeckForm(server);
                studioV6 = new StimTakeStudioV6Form(
                    server,
                    ShowControlDeck,
                    delegate { ExitThread(); },
                    controlDeck.ActivateValidatedShowPack,
                    controlDeck.TriggerShowPackAction);

                var menu = new ContextMenuStrip();
                menu.Items.Add("Open StimTake Studio 6.0", null, delegate { ShowStudioV6(); });
                menu.Items.Add("Open Backstage / Manual Tools", null, delegate { ShowControlDeck(); });
                menu.Items.Add("Open Overlay Preview", null, delegate { OpenUrl(BaseUrl + "index.html"); });
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("Copy OBS URL", null, delegate
                {
                    try
                    {
                        Clipboard.SetText(BaseUrl + "index.html");
                        tray.ShowBalloonTip(1500, "StimTake Studio 6.0", "OBS URL copied to the clipboard.", ToolTipIcon.Info);
                    }
                    catch (Exception error)
                    {
                        LogRuntimeError("Copy OBS URL", error);
                        MessageBox.Show("Copying failed. Use this OBS URL:\n\n" + BaseUrl + "index.html",
                            "StimTake Studio 6.0", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                });
                menu.Items.Add("Exit StimTake Studio", null, delegate { ExitThread(); });

                tray = new NotifyIcon
                {
                    Icon = SystemIcons.Application,
                    Text = "StimTake Studio 6.0 - local backend running",
                    ContextMenuStrip = menu,
                    Visible = true
                };
                tray.DoubleClick += delegate { ShowStudioV6(); };

                if (!quiet)
                {
                    tray.ShowBalloonTip(2200, "StimTake Studio 6.0",
                        "Studio and the local backend are running. Backstage remains available from the tray.",
                        ToolTipIcon.Info);
                    ShowStudioV6();
                }
            }

            private void ShowStudioV6()
            {
                if (!studioV6.Visible) studioV6.Show();
                if (studioV6.WindowState == FormWindowState.Minimized) studioV6.WindowState = FormWindowState.Normal;
                studioV6.Activate();
                studioV6.BringToFront();
            }

            private void ShowControlDeck()
            {
                if (!controlDeck.Visible) controlDeck.Show();
                if (controlDeck.WindowState == FormWindowState.Minimized) controlDeck.WindowState = FormWindowState.Normal;
                controlDeck.Activate();
                controlDeck.BringToFront();
            }

            protected override void ExitThreadCore()
            {
                server.Dispose();

                studioV6.AllowClose = true;
                studioV6.Close();
                studioV6.Dispose();

                controlDeck.AllowClose = true;
                controlDeck.Close();
                controlDeck.Dispose();

                tray.Visible = false;
                tray.Dispose();
                base.ExitThreadCore();
            }
        }

        private sealed class CrewMember
        {
            internal string Name;
            internal string Role;
            internal string Level;
            internal long LifetimeSupport;
            internal CrewMember(string name, string role, string level, long lifetimeSupport = 0) { Name = name; Role = role; Level = level; LifetimeSupport = Math.Max(0, lifetimeSupport); }
        }

        internal sealed partial class ControlDeckForm : Form
        {
            private static readonly Color Background = Color.FromArgb(18, 20, 17);
            private static readonly Color PanelColor = Color.FromArgb(32, 35, 31);
            private static readonly Color Orange = Color.FromArgb(244, 122, 55);
            private static readonly Color Gold = Color.FromArgb(255, 195, 107);
            private static readonly Color TextColor = Color.FromArgb(242, 238, 228);
            internal readonly StaticServer server;
            private readonly List<CrewMember> crew = new List<CrewMember>();
            private readonly DataGridView crewGrid = new DataGridView();
            private readonly TextBox crewName = new TextBox();
            private readonly TextBox crewRole = new TextBox();
            private readonly ComboBox crewLevel = new ComboBox();
            private readonly TextBox crewSupport = new TextBox();
            private readonly string crewFile;
            private readonly string crewProfileFile;
            private readonly string crewBackupFile;
            private readonly string crewBackupProfileFile;
            private readonly string crewLastSessionFile;
            private readonly string crewLastSessionProfileFile;
            private readonly TextBox deckRecentName = new TextBox();
            private readonly TextBox deckRecentMessage = new TextBox();
            private readonly NumericUpDown deckRecentAmount = new NumericUpDown();
            private readonly TextBox brandName = new TextBox();
            private readonly TextBox brandTagline = new TextBox();
            private readonly string brandFile;
            private readonly TextBox goalName = new TextBox();
            private readonly TextBox goalCurrent = new TextBox();
            private readonly TextBox goalTarget = new TextBox();
            private readonly string goalFile;
            private readonly string tipMenuFile;
            private readonly Button brandColor1Button = new Button();
            private readonly Button brandColor2Button = new Button();
            private string brandColor1 = "#FF69C9";
            private string brandColor2 = "#8B5CF6";
            private readonly TextBox wheelChoices = new TextBox();
            private readonly string wheelFile;
            private readonly ComboBox backgroundStyle = new ComboBox();
            private readonly string backgroundFile;
            private bool loadingBackgroundStyle;
            internal bool AllowClose;

            internal ControlDeckForm(StaticServer server)
            {
                this.server = server;
                string dataFolder = Path.Combine(LocalDataRoot(), "CreatorCamOverlayKit");
                crewFile = Path.Combine(dataFolder, "tippers.tsv");
                crewProfileFile = Path.Combine(dataFolder, "viewer-profiles-v3.json");
                crewBackupFile = Path.Combine(dataFolder, "tippers-manual-backup.tsv");
                crewBackupProfileFile = Path.Combine(dataFolder, "viewer-profiles-manual-backup-v3.json");
                crewLastSessionFile = Path.Combine(dataFolder, "tippers-last-session.tsv");
                crewLastSessionProfileFile = Path.Combine(dataFolder, "viewer-profiles-last-session-v3.json");
                SnapshotLastSessionData();
                brandFile = Path.Combine(dataFolder, "brand.tsv");
                goalFile = Path.Combine(LocalDataRoot(), "CreatorCamOverlayKit", "goal.tsv");
                tipMenuFile = Path.Combine(LocalDataRoot(), "CreatorCamOverlayKit", "tip-menu.txt");
                wheelFile = Path.Combine(LocalDataRoot(), "CreatorCamOverlayKit", "wheel.txt");
                backgroundFile = Path.Combine(LocalDataRoot(), "CreatorCamOverlayKit", "background.txt");
                Text = "Creator Cam Overlay Kit - Control Deck";
                Icon = SystemIcons.Application;
                BackColor = Background;
                ForeColor = TextColor;
                Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
                ClientSize = new Size(Math.Min(1040, Math.Max(760, workingArea.Width - 80)), Math.Min(920, Math.Max(640, workingArea.Height - 80)));
                MinimumSize = new Size(Math.Min(960, workingArea.Width), Math.Min(720, workingArea.Height));
                StartPosition = FormStartPosition.CenterScreen;
                Font = new Font("Segoe UI", 9.5f);
                BuildInterface();
                LoadBackgroundStyle();
                LoadBrand();
                LoadGoal();
                LoadWheelChoices();
                LoadCrew();
                RenderCrew();
                server.EventPublished += PlatformEventPublishedForCrew;
                Disposed += delegate { server.EventPublished -= PlatformEventPublishedForCrew; };
                InitializeStudioV3();
                LoadDeckRecentSupporter();
                FormClosing += delegate(object sender, FormClosingEventArgs e)
                {
                    if (!AllowClose && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
                };
            }

            private void BuildInterface()
            {
                var viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Background };
                var root = new TableLayoutPanel { Location = new Point(0, 0), Size = new Size(Math.Max(930, ClientSize.Width), Math.Max(808, ClientSize.Height)), ColumnCount = 2, RowCount = 4, Padding = new Padding(18, 6, 18, 18), BackColor = Background };
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
                viewport.Controls.Add(root);
                Controls.Add(viewport);
                viewport.Resize += delegate
                {
                    root.Width = Math.Max(930, viewport.ClientSize.Width);
                    root.Height = Math.Max(808, viewport.ClientSize.Height);
                };

                var title = new Label { Text = "CREATOR CAM CONTROL DECK", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 17, FontStyle.Bold), ForeColor = Gold, TextAlign = ContentAlignment.MiddleLeft };
                var status = new Label { Text = "LOCAL LINK  |  127.0.0.1:8787", Dock = DockStyle.Fill, ForeColor = Orange, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
                root.Controls.Add(title, 0, 0); root.Controls.Add(status, 1, 0);

                var alertPanel = Section("TIP / FAN ALERT");
                var alertName = Input("Viewer username", "TopFan", alertPanel, 35);
                var alertMessage = Input("Message", "sent an amazing tip!", alertPanel, 83);
                var fire = ActionButton("FIRE ALERT", 300, 56, 120);
                fire.Click += delegate { server.Publish("alert", "{\"name\":\"" + Json(alertName.Text) + "\",\"message\":\"" + Json(alertMessage.Text) + "\"}"); };
                alertPanel.Controls.Add(fire);
                root.Controls.Add(alertPanel, 0, 1);

                var goalPanel = Section("TOKEN GOAL");
                LabelledInputSized("Goal name", goalName, goalPanel, 18, 40, 180);
                LabelledInputSized("Current", goalCurrent, goalPanel, 208, 40, 82);
                LabelledInputSized("Target", goalTarget, goalPanel, 300, 40, 100);
                var updateGoal = ActionButton("UPDATE TOKEN GOAL", 230, 101, 170);
                updateGoal.Click += delegate { UpdateGoal(); };
                var resetGoal = SecondaryButton("RESET GOAL", 18, 101, 120);
                resetGoal.Click += delegate { goalCurrent.Text = "0"; UpdateGoal(); };
                goalPanel.Controls.Add(resetGoal);
                goalPanel.Controls.Add(updateGoal);
                root.Controls.Add(goalPanel, 1, 1);

                var crewPanel = Section("TOP TIPPERS / FANS");
                root.SetColumnSpan(crewPanel, 2); root.Controls.Add(crewPanel, 0, 2);
                crewGrid.Location = new Point(18, 37); crewGrid.Size = new Size(535, 235); crewGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
                crewGrid.BackgroundColor = Color.FromArgb(15, 17, 15); crewGrid.ForeColor = Color.Black; crewGrid.ReadOnly = true; crewGrid.AllowUserToAddRows = false; crewGrid.AllowUserToDeleteRows = false;
                crewGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; crewGrid.MultiSelect = false; crewGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                crewGrid.Columns.Add("Name", "USERNAME"); crewGrid.Columns.Add("Role", "CUSTOM LABEL"); crewGrid.Columns.Add("Level", "TIER"); crewGrid.Columns.Add("Lifetime", "LIFETIME SUPPORT");
                crewGrid.SelectionChanged += delegate { FillCrewEditor(); };
                crewPanel.Controls.Add(crewGrid);

                LabelledInput("Viewer username", crewName, crewPanel, 580, 42);
                LabelledInput("Custom label", crewRole, crewPanel, 580, 105);
                var levelLabel = SmallLabel("Tipper tier", 580, 166); crewPanel.Controls.Add(levelLabel);
                crewLevel.Items.AddRange(new object[] { "Bronze", "Silver", "Gold", "VIP" }); crewLevel.SelectedIndex = 0; crewLevel.DropDownStyle = ComboBoxStyle.DropDownList; crewLevel.Location = new Point(580, 188); crewLevel.Width = 120; crewPanel.Controls.Add(crewLevel);
                LabelledInputSized("Lifetime support", crewSupport, crewPanel, 710, 166, 135); crewSupport.Text = "0";
                var add = ActionButton("ADD OR UPDATE", 580, 230, 125); add.Click += delegate { AddOrUpdateCrew(); };
                var delete = SecondaryButton("DELETE SELECTED", 710, 230, 135); delete.Click += delegate { DeleteSelectedCrew(); };
                var clearSession = SecondaryButton("CLEAR SESSION", 580, 272, 125); clearSession.Click += delegate { ClearSessionCrew(); };
                var clearAll = SecondaryButton("CLEAR ALL DATA", 710, 272, 135); clearAll.Click += delegate { ClearAllCrew(); };
                var saveBackup = ActionButton("SAVE BACKUP", 580, 314, 82); saveBackup.Click += delegate { SaveCrewBackup(); };
                var loadBackup = SecondaryButton("LOAD BACKUP", 667, 314, 88); loadBackup.Click += delegate { LoadCrewBackup(false); };
                var loadLast = SecondaryButton("LOAD LAST", 760, 314, 85); loadLast.Click += delegate { LoadCrewBackup(true); };
                crewPanel.Controls.Add(add); crewPanel.Controls.Add(delete); crewPanel.Controls.Add(clearSession); crewPanel.Controls.Add(clearAll); crewPanel.Controls.Add(saveBackup); crewPanel.Controls.Add(loadBackup); crewPanel.Controls.Add(loadLast);

                crewPanel.Controls.Add(SmallLabel("LAST SUPPORTER DISPLAY", 18, 284));
                crewPanel.Controls.Add(SmallLabel("Username", 18, 307));
                deckRecentName.Location = new Point(18, 329); deckRecentName.Size = new Size(175, 27); crewPanel.Controls.Add(deckRecentName);
                crewPanel.Controls.Add(SmallLabel("Tokens", 205, 307));
                deckRecentAmount.Location = new Point(205, 329); deckRecentAmount.Size = new Size(75, 27); deckRecentAmount.Minimum = 0; deckRecentAmount.Maximum = 999999; crewPanel.Controls.Add(deckRecentAmount);
                crewPanel.Controls.Add(SmallLabel("Label", 292, 307));
                deckRecentMessage.Location = new Point(292, 329); deckRecentMessage.Size = new Size(145, 27); deckRecentMessage.Text = "MOST RECENT"; crewPanel.Controls.Add(deckRecentMessage);
                var setRecent = ActionButton("SET / EDIT", 449, 327, 92); setRecent.Click += delegate { SetDeckRecentSupporter(); };
                var clearRecent = SecondaryButton("CLEAR", 449, 363, 92); clearRecent.Click += delegate { ClearDeckRecentSupporter(); };
                crewPanel.Controls.Add(setRecent); crewPanel.Controls.Add(clearRecent);

                var visibility = Section("OVERLAY CONTROLS + CUSTOMIZATION");
                root.SetColumnSpan(visibility, 2); root.Controls.Add(visibility, 0, 3);
                visibility.Controls.Add(SmallLabel("Backdrop style", 558, 18));
                backgroundStyle.Items.AddRange(new object[] { "FEMALE - PINK / BLUE", "MALE - BLUE / STEEL" });
                backgroundStyle.DropDownStyle = ComboBoxStyle.DropDownList;
                backgroundStyle.Location = new Point(665, 12); backgroundStyle.Size = new Size(195, 27);
                backgroundStyle.BackColor = Color.FromArgb(15, 17, 15); backgroundStyle.ForeColor = TextColor;
                backgroundStyle.SelectedIndexChanged += delegate { ApplyBackgroundStyle(true); };
                visibility.Controls.Add(backgroundStyle);
                AddVisibilityButton(visibility, "BRAND", "brand", 18);
                AddVisibilityButton(visibility, "BACKDROP", "background", 123);
                AddVisibilityButton(visibility, "CAMERA FRAME", "camera", 228);
                AddVisibilityButton(visibility, "TOP TIPPERS", "supporters", 333);
                AddVisibilityButton(visibility, "TOKEN GOAL", "goal", 438);
                AddVisibilityButton(visibility, "TIP MENU", "ticker", 543);
                LabelledInput("Stage / model name", brandName, visibility, 18, 98);
                LabelledInput("Room tagline", brandTagline, visibility, 300, 98);
                visibility.Controls.Add(SmallLabel("Overlay theme colors", 582, 98));
                ConfigureColorButton(brandColor1Button, "START #FF69C9", 582, 119); brandColor1Button.Click += delegate { PickBrandColor(true); };
                ConfigureColorButton(brandColor2Button, "END #8B5CF6", 715, 119); brandColor2Button.Click += delegate { PickBrandColor(false); };
                visibility.Controls.Add(brandColor1Button); visibility.Controls.Add(brandColor2Button);
                var updateBrand = ActionButton("UPDATE TEXT + FULL THEME", 582, 159, 266);
                updateBrand.Click += delegate { SaveBrand(); server.Publish("brand", "{\"name\":\"" + Json(brandName.Text) + "\",\"tagline\":\"" + Json(brandTagline.Text) + "\",\"color1\":\"" + brandColor1 + "\",\"color2\":\"" + brandColor2 + "\"}"); };
                visibility.Controls.Add(updateBrand);
                var editTipMenu = SecondaryButton("EDIT TIP MENU / TICKER", 300, 159, 260); editTipMenu.Click += delegate { EditTipMenu(); }; visibility.Controls.Add(editTipMenu);
                var studio = ActionButton("OPEN BACKSTAGE DASHBOARD", 18, 159, 260); studio.Click += delegate { OpenStudioDashboard(); }; visibility.Controls.Add(studio);
                visibility.Controls.Add(SmallLabel("MANUAL CONTROL: games, custom HTML wheels/dice, and Action slots 1-20 are managed in Backstage. No site APIs are connected.", 18, 202));
            }

            private Panel Section(string title)
            {
                var panel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(6), BackColor = PanelColor };
                panel.Controls.Add(new Label { Text = title, ForeColor = Orange, Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(16, 10), AutoSize = true });
                return panel;
            }

            private TextBox Input(string label, string value, Control parent, int y)
            {
                var box = new TextBox { Text = value, Location = new Point(18, y + 20), Width = 260, BackColor = Color.FromArgb(15, 17, 15), ForeColor = TextColor, BorderStyle = BorderStyle.FixedSingle };
                parent.Controls.Add(SmallLabel(label, 18, y)); parent.Controls.Add(box); return box;
            }

            private void LabelledInput(string label, TextBox box, Control parent, int x, int y)
            {
                box.Location = new Point(x, y + 21); box.Width = 260; box.BackColor = Color.FromArgb(15, 17, 15); box.ForeColor = TextColor; box.BorderStyle = BorderStyle.FixedSingle;
                parent.Controls.Add(SmallLabel(label, x, y)); parent.Controls.Add(box);
            }

            private void LabelledInputSized(string label, TextBox box, Control parent, int x, int y, int width)
            {
                box.Location = new Point(x, y + 21); box.Width = width; box.BackColor = Color.FromArgb(15, 17, 15); box.ForeColor = TextColor; box.BorderStyle = BorderStyle.FixedSingle;
                parent.Controls.Add(SmallLabel(label, x, y)); parent.Controls.Add(box);
            }

            private Label SmallLabel(string text, int x, int y) { return new Label { Text = text, ForeColor = Color.FromArgb(190, 195, 185), Location = new Point(x, y), AutoSize = true }; }
            private Button ActionButton(string text, int x, int y, int width) { return StyledButton(text, x, y, width, Orange, Color.FromArgb(25, 20, 15)); }
            private Button SecondaryButton(string text, int x, int y, int width) { return StyledButton(text, x, y, width, Color.FromArgb(74, 78, 70), TextColor); }
            private Button StyledButton(string text, int x, int y, int width, Color back, Color fore) { return new Button { Text = text, Location = new Point(x, y), Size = new Size(width, 34), BackColor = back, ForeColor = fore, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Cursor = Cursors.Hand }; }

            private void AddVisibilityButton(Control parent, string text, string module, int x)
            {
                var check = new CheckBox { Text = text, Appearance = Appearance.Button, Checked = true, TextAlign = ContentAlignment.MiddleCenter, Location = new Point(x, 50), Size = new Size(100, 34), BackColor = Orange, ForeColor = Color.FromArgb(25, 20, 15), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 7.6f, FontStyle.Bold) };
                check.CheckedChanged += delegate { check.BackColor = check.Checked ? Orange : Color.FromArgb(74, 78, 70); check.ForeColor = check.Checked ? Color.FromArgb(25, 20, 15) : TextColor; server.Publish("visibility", "{\"module\":\"" + module + "\",\"visible\":" + check.Checked.ToString().ToLowerInvariant() + "}"); };
                parent.Controls.Add(check);
            }

            private void LoadCrew()
            {
                crew.Clear();
                try
                {
                    if (File.Exists(crewFile)) foreach (string line in File.ReadAllLines(crewFile))
                    {
                        string[] p = line.Split('\t'); long lifetime = 0;
                        if (p.Length > 3) Int64.TryParse(p[3], out lifetime);
                        if (p.Length >= 3 && Clean(p[0]).Length > 0) crew.Add(new CrewMember(p[0], p[1], p[2], lifetime));
                    }
                }
                catch { }
            }

            private void SnapshotLastSessionData()
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(crewFile));
                    if (File.Exists(crewFile)) File.Copy(crewFile, crewLastSessionFile, true);
                    if (File.Exists(crewProfileFile)) File.Copy(crewProfileFile, crewLastSessionProfileFile, true);
                }
                catch { }
            }

            private void SaveCrewBackup()
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(crewFile));
                    SaveCrew();
                    if (File.Exists(crewFile)) File.Copy(crewFile, crewBackupFile, true); else File.WriteAllText(crewBackupFile, "");
                    if (File.Exists(crewProfileFile)) File.Copy(crewProfileFile, crewBackupProfileFile, true);
                    MessageBox.Show("Top Tippers / Fans backup saved.\n\nUse LOAD BACKUP to restore this exact list later.", "StimTake Studio");
                }
                catch (Exception error) { MessageBox.Show("Backup could not be saved.\n\n" + error.Message, "StimTake Studio"); }
            }

            private void LoadCrewBackup(bool lastSession)
            {
                string listSource = lastSession ? crewLastSessionFile : crewBackupFile;
                string profileSource = lastSession ? crewLastSessionProfileFile : crewBackupProfileFile;
                string label = lastSession ? "last session" : "saved backup";
                if (!File.Exists(listSource) && !File.Exists(profileSource)) { MessageBox.Show("No " + label + " backup is available yet.", "StimTake Studio"); return; }
                if (MessageBox.Show("Restore the " + label + " Top Tippers / Fans data?\n\nThis replaces the current list and viewer profile state.", "StimTake Studio", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(crewFile));
                    if (File.Exists(listSource)) File.Copy(listSource, crewFile, true); else if (File.Exists(crewFile)) File.Delete(crewFile);
                    if (File.Exists(profileSource)) File.Copy(profileSource, crewProfileFile, true);
                    LoadCrew(); RenderCrew(); PublishCrewRestore();
                    MessageBox.Show("The " + label + " Top Tippers / Fans data was restored.", "StimTake Studio");
                }
                catch (Exception error) { MessageBox.Show("The backup could not be restored.\n\n" + error.Message, "StimTake Studio"); }
            }

            private string CrewMembersPayload()
            {
                var members = new List<string>();
                foreach (CrewMember member in crew)
                    members.Add("{\"name\":\"" + Json(member.Name) + "\",\"role\":\"" + Json(member.Role) + "\",\"level\":\"" + Json(member.Level.ToLowerInvariant()) + "\",\"lifetimeSupport\":" + Math.Max(0, member.LifetimeSupport) + "}");
                return "{\"members\":[" + String.Join(",", members.ToArray()) + "]}";
            }

            private void PublishCrewSync()
            {
                server.Publish("supporters-sync", CrewMembersPayload());
            }

            private void PublishCrewRestore()
            {
                // Legacy overlay pages already understand session-load as a reload signal.
                // The current overlay also consumes the attached exact Studio member list.
                server.Publish("session-load", CrewMembersPayload());
            }

            private void LoadDeckRecentSupporter()
            {
                try
                {
                    string[] values = ManualRecentSupporterValues();
                    deckRecentName.Text = values[0];
                    decimal amount; if (!Decimal.TryParse(values[1], out amount)) amount = 0;
                    deckRecentAmount.Value = Math.Max(deckRecentAmount.Minimum, Math.Min(deckRecentAmount.Maximum, amount));
                    deckRecentMessage.Text = values[2].Length > 0 ? values[2] : "MOST RECENT";
                }
                catch { }
            }

            private void SetDeckRecentSupporter()
            {
                string name = Clean(deckRecentName.Text);
                if (name.Length == 0) { MessageBox.Show("Enter a username for Last Supporter.", "StimTake Studio"); return; }
                if (!SaveManualRecentSupporterSettings(name, (int)deckRecentAmount.Value, deckRecentMessage.Text)) { MessageBox.Show("Last Supporter could not be saved.", "StimTake Studio"); return; }
                MessageBox.Show("Last Supporter updated. The OBS overlay will use this value until a newer supporter event replaces it or you click CLEAR.", "StimTake Studio");
            }

            private void ClearDeckRecentSupporter()
            {
                ClearManualRecentSupporterSettings();
                deckRecentName.Text = ""; deckRecentAmount.Value = 0; deckRecentMessage.Text = "MOST RECENT";
            }

            private void LoadBackgroundStyle()
            {
                string style = "female";
                try
                {
                    if (File.Exists(backgroundFile) && Clean(File.ReadAllText(backgroundFile)).ToLowerInvariant() == "male") style = "male";
                }
                catch { }
                loadingBackgroundStyle = true;
                backgroundStyle.SelectedIndex = style == "male" ? 1 : 0;
                loadingBackgroundStyle = false;
                ApplyBackgroundStyle(false);
            }

            private void ApplyBackgroundStyle(bool save)
            {
                if (loadingBackgroundStyle || backgroundStyle.SelectedIndex < 0) return;
                string style = backgroundStyle.SelectedIndex == 1 ? "male" : "female";
                if (save)
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(backgroundFile));
                        File.WriteAllText(backgroundFile, style);
                    }
                    catch { }
                }
                server.Publish("background-style", "{\"style\":\"" + style + "\"}");
            }

            private void LoadBrand()
            {
                brandName.Text = "YOUR STAGE NAME";
                brandTagline.Text = "LIVE • GOOD VIBES • HAVE FUN";
                try
                {
                    if (!File.Exists(brandFile)) return;
                    string[] values = File.ReadAllText(brandFile).Split('\t');
                    if (values.Length > 0 && Clean(values[0]).Length > 0) brandName.Text = Clean(values[0]);
                    if (values.Length > 1 && Clean(values[1]).Length > 0) brandTagline.Text = Clean(values[1]);
                    if (values.Length > 2 && IsHexColor(values[2])) brandColor1 = values[2];
                    if (values.Length > 3 && IsHexColor(values[3])) brandColor2 = values[3];
                }
                catch { }
                UpdateColorButtons();
            }

            private void SaveBrand()
            {
                if (Clean(brandName.Text).Length == 0) brandName.Text = "YOUR STAGE NAME";
                if (Clean(brandTagline.Text).Length == 0) brandTagline.Text = "LIVE • GOOD VIBES • HAVE FUN";
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(brandFile));
                    File.WriteAllText(brandFile, Clean(brandName.Text) + "\t" + Clean(brandTagline.Text) + "\t" + brandColor1 + "\t" + brandColor2);
                }
                catch { }
            }

            private void LoadGoal()
            {
                goalName.Text = "ROOM GOAL"; goalCurrent.Text = "350"; goalTarget.Text = "1000";
                try
                {
                    if (!File.Exists(goalFile)) return;
                    string[] values = File.ReadAllText(goalFile).Split('\t');
                    if (values.Length > 0 && Clean(values[0]).Length > 0) goalName.Text = Clean(values[0]);
                    if (values.Length > 1) goalCurrent.Text = Clean(values[1]);
                    if (values.Length > 2) goalTarget.Text = Clean(values[2]);
                }
                catch { }
            }

            private void UpdateGoal()
            {
                int current, target;
                if (!Int32.TryParse(Clean(goalCurrent.Text), out current) || current < 0) { MessageBox.Show("Enter a valid current token amount."); return; }
                if (!Int32.TryParse(Clean(goalTarget.Text), out target) || target < 1) { MessageBox.Show("Enter a target of at least 1 token."); return; }
                if (current > target) current = target;
                string label = Clean(goalName.Text); if (label.Length == 0) label = "ROOM GOAL"; if (label.Length > 40) label = label.Substring(0, 40);
                goalName.Text = label; goalCurrent.Text = current.ToString(); goalTarget.Text = target.ToString();
                try { Directory.CreateDirectory(Path.GetDirectoryName(goalFile)); File.WriteAllText(goalFile, label + "\t" + current + "\t" + target); } catch { }
                server.Publish("goal", "{\"label\":\"" + Json(label) + "\",\"value\":" + current + ",\"target\":" + target + "}");
            }

            private static string[] DefaultTipMenu()
            {
                return new string[] { "WELCOME TO MY ROOM", "25 TOKENS • SPECIAL SHOUTOUT", "50 TOKENS • SONG REQUEST", "100 TOKENS • SPIN THE REWARD WHEEL", "FOLLOW • CHAT • ENJOY THE SHOW" };
            }

            private void EditTipMenu()
            {
                string[] messages = DefaultTipMenu();
                try { if (File.Exists(tipMenuFile) && File.ReadAllLines(tipMenuFile).Length > 0) messages = File.ReadAllLines(tipMenuFile); } catch { }
                using (var dialog = new Form { Text = "Edit Tip Menu / Ticker", Size = new Size(620, 430), StartPosition = FormStartPosition.CenterParent, BackColor = Background, ForeColor = TextColor, Font = Font })
                {
                    var info = new Label { Text = "Enter 1-10 messages, one per line. Long messages auto-size in OBS.", Location = new Point(18, 18), AutoSize = true, ForeColor = Gold };
                    var editor = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Location = new Point(18, 48), Size = new Size(565, 275), BackColor = Color.FromArgb(15, 17, 15), ForeColor = TextColor, BorderStyle = BorderStyle.FixedSingle, Lines = messages };
                    var save = ActionButton("SAVE TIP MENU", 408, 337, 175); save.Click += delegate
                    {
                        var cleanMessages = new List<string>();
                        foreach (string line in editor.Lines) { string item = Clean(line); if (item.Length > 0) cleanMessages.Add(item.Length > 80 ? item.Substring(0, 80) : item); }
                        if (cleanMessages.Count < 1 || cleanMessages.Count > 10) { MessageBox.Show("Enter between 1 and 10 tip-menu messages."); return; }
                        try { Directory.CreateDirectory(Path.GetDirectoryName(tipMenuFile)); File.WriteAllLines(tipMenuFile, cleanMessages.ToArray()); } catch { }
                        server.Publish("ticker", "{\"messages\":" + JsonArray(cleanMessages.ToArray()) + "}");
                        dialog.DialogResult = DialogResult.OK; dialog.Close();
                    };
                    dialog.Controls.Add(info); dialog.Controls.Add(editor); dialog.Controls.Add(save);
                    dialog.ShowDialog(this);
                }
            }

            private static string[] DefaultWheelChoices()
            {
                return new string[] { "Song Request", "Special Shoutout", "Choose My Look", "Bonus Surprise", "Dance Break", "Pick the Theme", "VIP Message", "Spin Again" };
            }

            private string[] CurrentWheelChoices()
            {
                var choices = new List<string>();
                foreach (string line in wheelChoices.Lines)
                {
                    string choice = Clean(line);
                    if (choice.Length > 0 && !choices.Contains(choice)) choices.Add(choice.Length > 28 ? choice.Substring(0, 28) : choice);
                }
                return choices.ToArray();
            }

            private void LoadWheelChoices()
            {
                string[] choices = DefaultWheelChoices();
                try
                {
                    if (File.Exists(wheelFile))
                    {
                        string[] saved = File.ReadAllLines(wheelFile);
                        if (saved.Length >= 2) choices = saved;
                    }
                }
                catch { }
                wheelChoices.Lines = choices;
            }

            private bool SaveWheelChoices(bool notify)
            {
                string[] choices = CurrentWheelChoices();
                if (choices.Length < 2 || choices.Length > 12)
                {
                    if (notify) MessageBox.Show("Enter between 2 and 12 wheel choices, one choice per line.");
                    return false;
                }
                wheelChoices.Lines = choices;
                try { Directory.CreateDirectory(Path.GetDirectoryName(wheelFile)); File.WriteAllLines(wheelFile, choices); } catch { }
                server.Publish("wheel-options", "{\"prizes\":" + JsonArray(choices) + "}");
                return true;
            }

            private void ResetWheelChoices()
            {
                wheelChoices.Lines = DefaultWheelChoices();
                SaveWheelChoices(false);
            }

            internal void PublishWheel()
            {
                if (!SaveWheelChoices(true)) return;
                server.Publish("wheel", "{\"prizes\":" + JsonArray(CurrentWheelChoices()) + "}");
            }

            private void ConfigureColorButton(Button button, string text, int x, int y)
            {
                button.Text = text; button.Location = new Point(x, y); button.Size = new Size(125, 31); button.FlatStyle = FlatStyle.Flat; button.Font = new Font("Segoe UI", 8, FontStyle.Bold); button.Cursor = Cursors.Hand;
            }

            private void PickBrandColor(bool first)
            {
                using (var dialog = new ColorDialog { FullOpen = true, Color = ColorTranslator.FromHtml(first ? brandColor1 : brandColor2) })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    string value = "#" + dialog.Color.R.ToString("X2") + dialog.Color.G.ToString("X2") + dialog.Color.B.ToString("X2");
                    if (first) brandColor1 = value; else brandColor2 = value;
                    UpdateColorButtons();
                }
            }

            private void UpdateColorButtons()
            {
                brandColor1Button.Text = "START " + brandColor1; brandColor1Button.BackColor = ColorTranslator.FromHtml(brandColor1); brandColor1Button.ForeColor = Contrast(ColorTranslator.FromHtml(brandColor1));
                brandColor2Button.Text = "END " + brandColor2; brandColor2Button.BackColor = ColorTranslator.FromHtml(brandColor2); brandColor2Button.ForeColor = Contrast(ColorTranslator.FromHtml(brandColor2));
            }

            private static bool IsHexColor(string value) { return !String.IsNullOrEmpty(value) && System.Text.RegularExpressions.Regex.IsMatch(value, "^#[0-9A-Fa-f]{6}$"); }
            private static Color Contrast(Color color) { return (color.R * 299 + color.G * 587 + color.B * 114) / 1000 >= 145 ? Color.Black : Color.White; }

            private void AddStarterCrew()
            {
                crew.Add(new CrewMember("DiamondFan", "Top Tipper", "Gold", 5000));
                crew.Add(new CrewMember("SweetSupporter", "Room Regular", "Silver", 1200));
                crew.Add(new CrewMember("NewAdmirer", "New Fan", "Bronze", 250));
                crew.Add(new CrewMember("NightOwl", "VIP Viewer", "VIP", 8000));
            }

            private static string PlatformEventJsonString(string json, string field)
            {
                if (String.IsNullOrWhiteSpace(json) || String.IsNullOrWhiteSpace(field)) return "";
                System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
                    json,
                    "\"" + System.Text.RegularExpressions.Regex.Escape(field) + "\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!match.Success) return "";

                string value = match.Groups["value"].Value;
                try { value = System.Text.RegularExpressions.Regex.Unescape(value); } catch { }
                return Clean(value);
            }

            private static long PlatformEventJsonLong(string json, string field)
            {
                if (String.IsNullOrWhiteSpace(json) || String.IsNullOrWhiteSpace(field)) return 0;
                System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
                    json,
                    "\"" + System.Text.RegularExpressions.Regex.Escape(field) + "\"\\s*:\\s*(?<value>-?[0-9]+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                long value;
                return match.Success && Int64.TryParse(match.Groups["value"].Value, out value) ? value : 0;
            }

            private void PlatformEventPublishedForCrew(string type, string payloadJson)
            {
                if (!String.Equals(type, "platform-event", StringComparison.OrdinalIgnoreCase)) return;
                if (!String.Equals(PlatformEventJsonString(payloadJson, "type"), "tip", StringComparison.OrdinalIgnoreCase)) return;

                // StaticServer has already validated the locked room, persisted the
                // event_id and updated the authoritative lifetime file. Backstage only
                // reloads that accepted state; it must not add the amount a second time.
                MethodInvoker apply = delegate { LoadCrew(); RenderCrew(); PublishCrewSync(); };

                try
                {
                    if (IsDisposed || Disposing) return;
                    if (InvokeRequired)
                    {
                        if (IsHandleCreated) BeginInvoke(apply);
                    }
                    else apply();
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            }

            private void AddPlatformTipToCrew(string username, long amount)
            {
                username = Clean(username);
                if (username.Length == 0 || amount <= 0) return;

                int index = crew.FindIndex(delegate(CrewMember member)
                {
                    return String.Equals(member.Name, username, StringComparison.OrdinalIgnoreCase);
                });

                if (index >= 0)
                {
                    CrewMember existing = crew[index];
                    long updated = existing.LifetimeSupport > Int64.MaxValue - amount
                        ? Int64.MaxValue
                        : existing.LifetimeSupport + amount;

                    // Preserve any creator-edited label and tier.
                    crew[index] = new CrewMember(existing.Name, existing.Role, existing.Level, updated);
                }
                else
                {
                    // New real tipper enters the saved board automatically.
                    crew.Add(new CrewMember(username, "Supporter", "Bronze", amount));
                }

                // Top supporters should actually rank as top supporters.
                crew.Sort(delegate(CrewMember left, CrewMember right)
                {
                    int supportOrder = right.LifetimeSupport.CompareTo(left.LifetimeSupport);
                    return supportOrder != 0
                        ? supportOrder
                        : StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
                });

                SaveCrew();
                RenderCrew();
                PublishCrewSync();
            }

            private void SaveCrew()
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(crewFile));
                    var lines = new List<string>();
                    foreach (CrewMember m in crew) lines.Add(Clean(m.Name) + "\t" + Clean(m.Role) + "\t" + Clean(m.Level) + "\t" + Math.Max(0, m.LifetimeSupport));
                    File.WriteAllLines(crewFile, lines.ToArray());
                }
                catch { }
            }

            private static string Clean(string text) { return (text ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ").Trim(); }
            internal static string Json(string text) { return Clean(text).Replace("\\", "\\\\").Replace("\"", "\\\""); }
            private static string JsonArray(string[] values) { var items = new List<string>(); foreach (string value in values) items.Add("\"" + Json(value) + "\""); return "[" + String.Join(",", items.ToArray()) + "]"; }

            private void RenderCrew()
            {
                crewGrid.Rows.Clear();
                if (crew.Count == 0)
                {
                    int emptyRow = crewGrid.Rows.Add("Waiting for supporters...", "", "", "");
                    crewGrid.Rows[emptyRow].DefaultCellStyle.ForeColor = Color.DimGray;
                    return;
                }
                foreach (CrewMember member in crew) crewGrid.Rows.Add(member.Name, member.Role, member.Level, member.LifetimeSupport);
            }

            private void FillCrewEditor()
            {
                if (crewGrid.SelectedRows.Count == 0) return;
                int i = crewGrid.SelectedRows[0].Index; if (i < 0 || i >= crew.Count) return;
                crewName.Text = crew[i].Name; crewRole.Text = crew[i].Role; crewLevel.SelectedItem = crew[i].Level; crewSupport.Text = crew[i].LifetimeSupport.ToString();
            }

            private void AddOrUpdateCrew()
            {
                string name = Clean(crewName.Text); if (name.Length == 0) { MessageBox.Show("Enter a viewer username."); return; }
                string role = Clean(crewRole.Text); if (role.Length == 0) role = "Supporter";
                string level = crewLevel.SelectedItem == null ? "Bronze" : crewLevel.SelectedItem.ToString();
                long lifetime; if (!Int64.TryParse(Clean(crewSupport.Text), out lifetime) || lifetime < 0) { MessageBox.Show("Enter a valid lifetime support amount."); return; }
                int index = crew.FindIndex(delegate(CrewMember member) { return String.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase); });
                if (index >= 0) crew[index] = new CrewMember(name, role, level, lifetime); else crew.Add(new CrewMember(name, role, level, lifetime));
                SaveCrew(); RenderCrew(); server.Publish("supporter", "{\"name\":\"" + Json(name) + "\",\"role\":\"" + Json(role) + "\",\"level\":\"" + level.ToLowerInvariant() + "\",\"lifetimeSupport\":" + lifetime + "}");
            }

            private void DeleteSelectedCrew()
            {
                if (crewGrid.SelectedRows.Count == 0) { MessageBox.Show("Select a tipper or fan to delete."); return; }
                int i = crewGrid.SelectedRows[0].Index; if (i < 0 || i >= crew.Count) return;
                string name = crew[i].Name; crew.RemoveAt(i); SaveCrew(); RenderCrew(); server.Publish("supporter-remove", "{\"name\":\"" + Json(name) + "\"}");
            }

            private void ClearSessionCrew()
            {
                server.Publish("supporter-clear-session", "{}");
            }

            private void ClearAllCrew()
            {
                if (MessageBox.Show("Clear current-session supporters and all saved fan history?", "Creator Cam Studio", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                crew.Clear(); SaveCrew(); RenderCrew(); server.Publish("supporter-clear-all", "{}");
            }

            private void LoadTestCrew()
            {
                crew.Clear(); AddStarterCrew(); SaveCrew(); RenderCrew(); server.Publish("supporter-test-data", "{}");
            }
        }

        internal sealed partial class StaticServer : IDisposable
        {
            private readonly int port;
            private readonly Dictionary<string, Asset> assets = new Dictionary<string, Asset>(StringComparer.OrdinalIgnoreCase);
            private TcpListener listener;
            private Thread thread;
            private volatile bool running;
            private volatile string eventJson = "{\"type\":\"ready\",\"payload\":{},\"at\":0}";
            private readonly object eventGate = new object();
            private long lastEventAt;

            internal StaticServer(int port) : this(port, null) { }

            internal StaticServer(int port, string runtimeRoot)
            {
                this.port = port;
                LoadAssets();
                InitializeV6Runtime(runtimeRoot);
            }

            private void LoadAssets()
            {
                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CreatorCamPayload.zip");
                if (stream == null) throw new InvalidOperationException("The embedded overlay package is missing.");
                using (stream)
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (String.IsNullOrEmpty(entry.Name)) continue;
                        using (Stream source = entry.Open())
                        using (var memory = new MemoryStream())
                        {
                            source.CopyTo(memory);
                            string path = "/" + entry.FullName.Replace('\\', '/').TrimStart('/');
                            assets[path] = new Asset(memory.ToArray(), Mime(path));
                        }
                    }
                }
            }

            internal void Start()
            {
                try
                {
                    listener = new TcpListener(IPAddress.Loopback, port);
                    listener.Start();
                }
                catch (SocketException)
                {
                    throw new InvalidOperationException("Port 8787 is already in use. Close the stale or competing local backend, then start StimTake Studio again.");
                }
                running = true;
                thread = new Thread(ListenLoop) { IsBackground = true, Name = "Creator Cam local server" };
                thread.Start();
            }

            internal void Publish(string type, string payloadJson)
            {
                ClearPersistedManualRecentSupporterForTip(type, payloadJson);
                lock (eventGate)
                {
                    long now = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
                    if (now <= lastEventAt) now = lastEventAt + 1;
                    lastEventAt = now;
                    eventJson = "{\"type\":\"" + type + "\",\"payload\":" + payloadJson + ",\"at\":" + now + "}";
                }
                NotifyEvolutionEvent(type, payloadJson);
            }

            private void ListenLoop()
            {
                while (running)
                {
                    try
                    {
                        TcpClient client = listener.AcceptTcpClient();
                        ThreadPool.QueueUserWorkItem(delegate
                        {
                            try { Serve(client); }
                            catch (IOException) { }
                            catch (SocketException) { }
                            catch (ObjectDisposedException) { }
                            catch (Exception error) { LogRuntimeError("Preview request", error); }
                            finally { try { client.Close(); } catch { } }
                        });
                    }
                    catch (SocketException) { if (running) Thread.Sleep(50); }
                    catch (ObjectDisposedException) { return; }
                }
            }

            private void Serve(TcpClient client)
            {
                using (client)
                using (NetworkStream network = client.GetStream())
                {
                    client.ReceiveTimeout = 3000;
                    string requestLine;
                    using (var reader = new StreamReader(network, Encoding.ASCII, false, 1024, true))
                    {
                        requestLine = reader.ReadLine();
                        string line;
                        do { line = reader.ReadLine(); } while (!String.IsNullOrEmpty(line));
                    }
                    if (String.IsNullOrEmpty(requestLine)) return;
                    string[] parts = requestLine.Split(' ');
                    string target = parts.Length > 1 ? parts[1] : "/";
                    string path = target.Split('?')[0];
                    path = Uri.UnescapeDataString(path);
                    if (path == "/") path = "/index.html";

                    if (path == "/api/event")
                    {
                        int dataIndex = target.IndexOf("?data=", StringComparison.Ordinal);
                        if (dataIndex >= 0)
                        {
                            eventJson = Uri.UnescapeDataString(target.Substring(dataIndex + 6).Replace("+", " "));
                            WriteResponse(network, "204 No Content", "text/plain", new byte[0], true);
                        }
                        else
                        {
                            byte[] eventBody = Encoding.UTF8.GetBytes(eventJson);
                            WriteResponse(network, "200 OK", "application/json; charset=utf-8", eventBody, requestLine.StartsWith("HEAD "));
                        }
                        return;
                    }

                    if (path == "/api/platform-event")
                    {
                        int dataIndex = target.IndexOf("?data=", StringComparison.Ordinal);
                        if (dataIndex < 0)
                        {
                            byte[] help = Encoding.UTF8.GetBytes("Send a URL-encoded event object in the data query parameter.");
                            WriteResponse(network, "400 Bad Request", "text/plain; charset=utf-8", help, requestLine.StartsWith("HEAD "));
                            return;
                        }
                        string raw = Uri.UnescapeDataString(target.Substring(dataIndex + 6).Replace("+", " "));
                        string responseStatus;
                        string responseText;
                        AcceptPlatformEvent(raw, out responseStatus, out responseText);
                        byte[] responseBody = Encoding.UTF8.GetBytes(responseText ?? "");
                        WriteResponse(network, responseStatus, "text/plain; charset=utf-8", responseBody, responseStatus.StartsWith("204 "));
                        return;
                    }

                    if (path == "/api/studio-status")
                    {
                        byte[] statusBody = Encoding.UTF8.GetBytes(GetStudioStatusJson());
                        WriteResponse(network, "200 OK", "application/json; charset=utf-8", statusBody, requestLine.StartsWith("HEAD "));
                        return;
                    }

                    if (path == "/api/profile-state")
                    {
                        string profilePath = Path.Combine(LocalDataRoot(), "CreatorCamOverlayKit", "viewer-profiles-v3.json");
                        int dataIndex = target.IndexOf("?data=", StringComparison.Ordinal);
                        if (dataIndex >= 0)
                        {
                            string profileJson = Uri.UnescapeDataString(target.Substring(dataIndex + 6).Replace("+", " "));
                            Directory.CreateDirectory(Path.GetDirectoryName(profilePath));
                            File.WriteAllText(profilePath, profileJson, Encoding.UTF8);
                            WriteResponse(network, "204 No Content", "text/plain", new byte[0], true);
                        }
                        else
                        {
                            byte[] profileBody = Encoding.UTF8.GetBytes(File.Exists(profilePath) ? File.ReadAllText(profilePath, Encoding.UTF8) : "{}");
                            WriteResponse(network, "200 OK", "application/json; charset=utf-8", profileBody, requestLine.StartsWith("HEAD "));
                        }
                        return;
                    }

                    if (TryServeEvolution(path, target, requestLine, network)) return;

                    Asset asset;
                    if (!assets.TryGetValue(path, out asset))
                    {
                        byte[] missing = Encoding.UTF8.GetBytes("Creator Cam could not find that page.");
                        WriteResponse(network, "404 Not Found", "text/plain; charset=utf-8", missing, requestLine.StartsWith("HEAD "));
                        return;
                    }
                    WriteResponse(network, "200 OK", asset.ContentType, asset.Bytes, requestLine.StartsWith("HEAD "));
                }
            }

            private static void WriteResponse(Stream stream, string status, string type, byte[] body, bool headOnly)
            {
                string headers = "HTTP/1.1 " + status + "\r\nContent-Type: " + type +
                    "\r\nContent-Length: " + body.Length +
                    "\r\nCache-Control: no-store\r\nAccess-Control-Allow-Origin: *\r\nConnection: close\r\n\r\n";
                byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
                stream.Write(headerBytes, 0, headerBytes.Length);
                if (!headOnly) stream.Write(body, 0, body.Length);
            }

            private static string Mime(string path)
            {
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension == ".html") return "text/html; charset=utf-8";
                if (extension == ".css") return "text/css; charset=utf-8";
                if (extension == ".js" || extension == ".mjs") return "text/javascript; charset=utf-8";
                if (extension == ".json") return "application/json; charset=utf-8";
                if (extension == ".png") return "image/png";
                if (extension == ".jpg" || extension == ".jpeg") return "image/jpeg";
                if (extension == ".gif") return "image/gif";
                if (extension == ".webp") return "image/webp";
                if (extension == ".wav") return "audio/wav";
                if (extension == ".mp3") return "audio/mpeg";
                if (extension == ".ogg") return "audio/ogg";
                if (extension == ".svg") return "image/svg+xml";
                if (extension == ".woff") return "font/woff";
                if (extension == ".woff2") return "font/woff2";
                if (extension == ".ttf") return "font/ttf";
                if (extension == ".otf") return "font/otf";
                if (extension == ".mp4") return "video/mp4";
                if (extension == ".webm") return "video/webm";
                return "application/octet-stream";
            }

            public void Dispose()
            {
                running = false;
                if (listener != null) listener.Stop();
            }

            private sealed class Asset
            {
                internal readonly byte[] Bytes;
                internal readonly string ContentType;
                internal Asset(byte[] bytes, string contentType) { Bytes = bytes; ContentType = contentType; }
            }
        }
    }
}
