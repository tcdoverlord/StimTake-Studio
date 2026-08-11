using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using StimTakeShared;

namespace CreatorCamOverlayKit
{
    internal static partial class Program
    {
        internal sealed partial class ControlDeckForm
        {
            private const int WmHotkey = 0x0312;
            private const int SafeHotkeyId = 7301;
            private const uint ModControl = 0x0002;
            private const uint ModShift = 0x0004;

            [DllImport("user32.dll")]
            private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

            [DllImport("user32.dll")]
            private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

            private StudioDashboardForm studioDashboard;
            private string studioDataFolder;
            private bool streamSafeMode;

            private void InitializeStudioV3()
            {
                studioDataFolder = Path.Combine(LocalDataRoot(), "CreatorCamOverlayKit");
                EnsureBundledWheelBackups();
                DisableLegacyActionPreloadsForV4();
                EnsureSecretShowStartsInactive();
                PublishStudioBootstrap();
            }

            private void EnsureSecretShowStartsInactive()
            {
                try
                {
                    string path = StudioPath("enabled-modules-v3.txt");
                    var enabled = LoadEnabledModules();
                    enabled.RemoveAll(delegate(string item) { return String.Equals(item, "my-secret-show", StringComparison.OrdinalIgnoreCase); });
                    File.WriteAllLines(path, enabled.ToArray());
                }
                catch { }
            }

            internal void PublishStudioBootstrap()
            {
                try
                {
                    string layout = File.Exists(StudioPath("layout-v3.txt")) ? Clean(File.ReadAllText(StudioPath("layout-v3.txt"))).ToLowerInvariant() : "creator";
                    string theme = File.Exists(StudioPath("theme-v3.txt")) ? Clean(File.ReadAllText(StudioPath("theme-v3.txt"))).ToLowerInvariant() : "custom";
                    string background = File.Exists(backgroundFile) && Clean(File.ReadAllText(backgroundFile)).ToLowerInvariant() == "male" ? "male" : "female";
                    // Automatic Tip Action Rules remain preserved for a future connector-enabled version,
                    // but manual-control releases intentionally publish an empty rule set.
                    var rules = new List<string>();
                    var styles = new List<string>();
                    string stylesPath = StudioPath("module-styles-v3.txt");
                    if (File.Exists(stylesPath)) foreach (string line in File.ReadAllLines(stylesPath))
                    {
                        string[] parts = line.Split('|'); if (parts.Length < 6) continue;
                        decimal x, y, scale, opacity, width;
                        if (!Decimal.TryParse(parts[1], out x) || !Decimal.TryParse(parts[2], out y) || !Decimal.TryParse(parts[3], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out scale) || !Decimal.TryParse(parts[4], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out opacity) || !Decimal.TryParse(parts[5], out width)) continue;
                        styles.Add("{\"module\":\"" + Json(parts[0]) + "\",\"x\":" + x.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"y\":" + y.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"scale\":" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"opacity\":" + opacity.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"width\":" + width.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");
                    }
                    int duration = 6000, maximum = 5; string animation = "slide"; bool sound = false;
                    string alertsPath = StudioPath("alert-settings-v3.txt");
                    if (File.Exists(alertsPath))
                    {
                        string[] values = File.ReadAllText(alertsPath).Split('\t'); int seconds;
                        if (values.Length > 0 && Int32.TryParse(values[0], out seconds)) duration = Math.Max(1000, Math.Min(30000, seconds * 1000));
                        if (values.Length > 1) animation = Clean(values[1]).ToLowerInvariant();
                        if (values.Length > 2) Boolean.TryParse(values[2], out sound);
                        if (values.Length > 3) Int32.TryParse(values[3], out maximum);
                    }
                    string payload = "{\"layout\":\"" + Json(layout) + "\",\"theme\":\"" + Json(theme) + "\",\"background\":\"" + background + "\",\"alertSettings\":{\"duration\":" + duration + ",\"animation\":\"" + Json(animation) + "\",\"sound\":" + sound.ToString().ToLowerInvariant() + "},\"leaderboardMaximum\":" + Math.Max(1, Math.Min(10, maximum)) + ",\"rules\":[" + String.Join(",", rules.ToArray()) + "],\"styles\":[" + String.Join(",", styles.ToArray()) + "]" + EvolutionBootstrapFragment() + "}";
                    server.Publish("studio-bootstrap", payload);
                }
                catch { }
            }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                RegisterHotKey(Handle, SafeHotkeyId, ModControl | ModShift, (uint)Keys.H);
            }

            protected override void OnHandleDestroyed(EventArgs e)
            {
                UnregisterHotKey(Handle, SafeHotkeyId);
                base.OnHandleDestroyed(e);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WmHotkey && m.WParam.ToInt32() == SafeHotkeyId)
                {
                    ToggleStreamSafeMode();
                    return;
                }
                base.WndProc(ref m);
            }

            private void OpenStudioDashboard()
            {
                if (studioDashboard == null || studioDashboard.IsDisposed) studioDashboard = new StudioDashboardForm(this);
                if (!studioDashboard.Visible) studioDashboard.Show(this);
                if (studioDashboard.WindowState == FormWindowState.Minimized) studioDashboard.WindowState = FormWindowState.Normal;
                studioDashboard.Activate();
                studioDashboard.BringToFront();
            }

            internal void ToggleStreamSafeMode()
            {
                streamSafeMode = !streamSafeMode;
                server.Publish("safe-mode", "{\"enabled\":" + streamSafeMode.ToString().ToLowerInvariant() + "}");
                if (studioDashboard != null && !studioDashboard.IsDisposed) studioDashboard.UpdateSafeMode(streamSafeMode);
                traySafeNotice();
            }

            private void traySafeNotice()
            {
                Text = streamSafeMode ? "Creator Cam Overlay Kit - STREAM SAFE" : "Creator Cam Overlay Kit - Control Deck";
            }

            internal void PublishStudioTip(string username, int amount)
            {
                string name = Clean(username); if (name.Length == 0) return;
                server.Publish("tip", "{\"username\":\"" + Json(name) + "\",\"amount\":" + Math.Max(0, amount) + "}");
            }

            internal void PublishStudioFollow(string username)
            {
                string name = Clean(username); if (name.Length == 0) name = "NewViewer";
                server.Publish("follow", "{\"username\":\"" + Json(name) + "\",\"message\":\"followed the room\"}");
            }

            internal void PublishStudioDice(string username, int count, int sides, string match, string targetText, string reward, string successMessage, string failureMessage, string outcomesText)
            {
                int target = sides, minimum = sides, maximum = sides;
                string[] range = (targetText ?? "").Split('-');
                if (range.Length > 0) Int32.TryParse(Clean(range[0]), out target);
                if (target < 1) target = sides;
                minimum = target; maximum = target;
                if (range.Length > 1) { Int32.TryParse(Clean(range[0]), out minimum); Int32.TryParse(Clean(range[1]), out maximum); }
                if (minimum < 1) minimum = 1; if (maximum < minimum) maximum = minimum;

                var outcomes = new List<string>();
                foreach (string line in (outcomesText ?? "").Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int split = line.IndexOf('='); if (split < 1) continue;
                    string number = Clean(line.Substring(0, split)); int roll;
                    if (!Int32.TryParse(number, out roll)) continue;
                    outcomes.Add("\"" + roll + "\":\"" + Json(line.Substring(split + 1)) + "\"");
                }
                string payload = "{\"username\":\"" + Json(username) + "\",\"count\":" + count + ",\"sides\":" + sides +
                    ",\"match\":\"" + Json(match.ToLowerInvariant()) + "\",\"target\":" + target + ",\"minimum\":" + minimum + ",\"maximum\":" + maximum +
                    ",\"reward\":\"" + Json(reward) + "\",\"successMessage\":\"" + Json(successMessage) + "\",\"failureMessage\":\"" + Json(failureMessage) +
                    "\",\"outcomes\":{" + String.Join(",", outcomes.ToArray()) + "}}";
                server.Publish("dice", payload);
            }

            internal void PublishStudioWheel(string username, string wheelName, string[] prizes)
            {
                if (prizes == null || prizes.Length < 2) { MessageBox.Show("The selected wheel needs at least two prizes."); return; }
                server.Publish("wheel", "{\"username\":\"" + Json(username) + "\",\"name\":\"" + Json(wheelName) + "\",\"prizes\":" + JsonArray(prizes) + "}");
            }

            internal string StudioPath(string filename)
            {
                Directory.CreateDirectory(studioDataFolder);
                return Path.Combine(studioDataFolder, filename);
            }

            internal void ExportStudioProfile()
            {
                using (var dialog = new SaveFileDialog { Filter = "Overlay Profile (*.zip)|*.zip", FileName = "Overlay_Profile.zip", OverwritePrompt = true })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    Directory.CreateDirectory(studioDataFolder);
                    if (File.Exists(dialog.FileName)) File.Delete(dialog.FileName);
                    ZipFile.CreateFromDirectory(studioDataFolder, dialog.FileName, CompressionLevel.Optimal, false);
                    MessageBox.Show("Overlay profile exported successfully.", "Creator Cam Studio");
                }
            }

            internal void ImportStudioProfile()
            {
                using (var dialog = new OpenFileDialog { Filter = "Overlay Profile (*.zip)|*.zip", CheckFileExists = true })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    Directory.CreateDirectory(studioDataFolder);
                    string root = Path.GetFullPath(studioDataFolder) + Path.DirectorySeparatorChar;
                    using (var archive = ZipFile.OpenRead(dialog.FileName))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            if (String.IsNullOrEmpty(entry.Name)) continue;
                            string destination = Path.GetFullPath(Path.Combine(studioDataFolder, entry.FullName));
                            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsafe profile entry was blocked.");
                            Directory.CreateDirectory(Path.GetDirectoryName(destination));
                            entry.ExtractToFile(destination, true);
                        }
                    }
                    server.Publish("session-load", "{}");
                    MessageBox.Show("Profile imported. Reopen the Control Deck to reload every saved field.", "Creator Cam Studio");
                }
            }
        }

        private sealed partial class StudioDashboardForm : Form
        {
            private static readonly Color Bg = Color.FromArgb(18, 20, 17);
            private static readonly Color Panel = Color.FromArgb(32, 35, 31);
            private static readonly Color Orange = Color.FromArgb(244, 122, 55);
            private static readonly Color Gold = Color.FromArgb(255, 195, 107);
            private static readonly Color TextColor = Color.FromArgb(242, 238, 228);
            private readonly ControlDeckForm owner;
            private readonly Label currentEvent = new Label();
            private readonly Label lastTip = new Label();
            private readonly Label currentGame = new Label();
            private readonly Label topSupporter = new Label();
            private readonly Label statistics = new Label();
            private Button safeButton;
            private readonly List<Button> safeButtons = new List<Button>();
            private int tipCount, tokenCount, diceRollStats, wheelCount;

            private TextBox simulatorName, simulatorAmount;
            private TextBox diceName, diceTarget, diceReward, diceSuccess, diceFailure, diceOutcomes;
            private NumericUpDown diceAmount;
            private ComboBox diceSides, diceMatch;
            private TextBox wheelsEditor;
            private ComboBox wheelSelector;
            private ComboBox wheelScriptSelector;
            private TextBox wheelScriptName;
            private Label wheelScriptStatus;
            private ComboBox htmlGameSelector, htmlGameKind;
            private TextBox htmlGameName;
            private TextBox tipRules;
            private NumericUpDown leaderboardMaximum, alertDuration;
            private ComboBox alertAnimation;
            private CheckBox alertSound;
            private ComboBox layoutPreset, themePreset, moduleName;
            private NumericUpDown moduleX, moduleY, moduleScale, moduleOpacity, moduleWidth, moduleMoveStep;
            private Label modulePositionStatus;
            private TabControl mainTabs;

            internal StudioDashboardForm(ControlDeckForm ownerForm)
            {
                owner = ownerForm;
                Text = "Creator Cam Studio v3 - Backstage Dashboard (Manual Control)";
                Icon = SystemIcons.Application;
                BackColor = Bg; ForeColor = TextColor;
                Font = new Font("Segoe UI", 9.5f);
                StartPosition = FormStartPosition.CenterParent;
                ClientSize = new Size(980, 720);
                MinimumSize = new Size(900, 650);
                BuildInterface();
                LoadSavedEditors();
                LoadEvolutionEditors();
                FormClosing += delegate(object sender, FormClosingEventArgs e) { e.Cancel = true; Hide(); };
            }

            private void BuildInterface()
            {
                var title = new Label { Text = "CREATOR CAM STUDIO • BACKSTAGE CONTROL • CHATURBATE CONNECTOR READY", Dock = DockStyle.Top, Height = 52, Padding = new Padding(18, 14, 0, 0), ForeColor = Gold, Font = new Font("Segoe UI", 14, FontStyle.Bold) };
                mainTabs = new TabControl { Dock = DockStyle.Fill };
                mainTabs.TabPages.Add(BuildDashboardTab());
                mainTabs.TabPages.Add(BuildGamesTab());
                mainTabs.TabPages.Add(BuildActionDeckTab());
                mainTabs.TabPages.Add(BuildModulesTab());
                mainTabs.TabPages.Add(BuildConnectorTab());
                mainTabs.TabPages.Add(BuildRulesTab());
                mainTabs.TabPages.Add(BuildLayoutTab());
                mainTabs.TabPages.Add(BuildSessionTab());
                Controls.Add(mainTabs); Controls.Add(title);
            }

            private TabPage Page(string text)
            {
                return new TabPage(text) { BackColor = Bg, ForeColor = TextColor, AutoScroll = true, Padding = new Padding(14) };
            }

            private TabPage BuildDashboardTab()
            {
                var page = Page("DASHBOARD");
                var status = Group("LIVE STATUS", 15, 15, 915, 225); page.Controls.Add(status);
                StatusLabel(currentEvent, "Current Event: Ready", 18, 35, status);
                StatusLabel(lastTip, "Last Tip: None", 18, 93, status);
                StatusLabel(currentGame, "Current Game: Idle", 460, 35, status);
                StatusLabel(topSupporter, "Top Supporter: Waiting", 460, 93, status);
                StatusLabel(statistics, "Today: 0 tips • 0 tokens • 0 dice • 0 wheels", 18, 158, status, 875);

                var simulator = Group("MANUAL EVENT BUTTONS", 15, 255, 915, 260); page.Controls.Add(simulator);
                simulator.Controls.Add(LabelAt("Viewer username", 18, 36)); simulatorName = Box("", 18, 58, 220); simulator.Controls.Add(simulatorName);
                simulator.Controls.Add(LabelAt("Tip amount", 255, 36)); simulatorAmount = Box("25", 255, 58, 110); simulator.Controls.Add(simulatorAmount);
                simulator.Controls.Add(ButtonAt("FAKE TIP", 18, 105, 145, delegate { FakeTip(); }));
                simulator.Controls.Add(ButtonAt("FAKE FOLLOW", 172, 105, 145, delegate { FakeFollow(); }));
                simulator.Controls.Add(ButtonAt("TEST ALERT", 326, 105, 145, delegate { owner.server.Publish("alert", "{\"name\":\"Test Alert\",\"message\":\"The reusable alert engine is working.\"}"); SetEvent("Test alert"); }));
                safeButton = ButtonAt("STREAM SAFE MODE", 18, 165, 190, delegate { owner.ToggleStreamSafeMode(); }); safeButtons.Add(safeButton); simulator.Controls.Add(safeButton);
                simulator.Controls.Add(ButtonAt("HIDE UI", 218, 165, 130, delegate { owner.ToggleStreamSafeMode(); }));
                simulator.Controls.Add(ButtonAt("RESET SESSION", 358, 165, 160, delegate { owner.server.Publish("session-reset", "{}"); tipCount = tokenCount = diceRollStats = wheelCount = 0; SetEvent("Session reset"); RefreshStats(); }));
                simulator.Controls.Add(LabelAt("Emergency hotkey: CTRL + SHIFT + H", 555, 173));
                simulator.Controls.Add(LabelAt("Wheel and dice controls are available only in the GAMES tab.", 500, 115));
                page.Controls.Add(BuildMySecretShowGroup());
                page.Controls.Add(BuildMovingWatermarkGroup());
                page.Controls.Add(BuildManualLastSupporterGroup());
                return page;
            }

            private TabPage BuildGamesTab()
            {
                var page = Page("GAMES");
                var dice = Group("DICE ENGINE", 15, 15, 440, 570); page.Controls.Add(dice);
                dice.Controls.Add(LabelAt("Viewer (optional)", 18, 36)); diceName = Box("", 18, 57, 180); dice.Controls.Add(diceName);
                dice.Controls.Add(LabelAt("Dice count", 215, 36)); diceAmount = NumberAt(1, 1, 3, 215, 57, 85, 0); dice.Controls.Add(diceAmount);
                dice.Controls.Add(LabelAt("Dice type", 315, 36)); diceSides = Combo(new string[] { "D6", "D12", "D20" }, 315, 57, 85); dice.Controls.Add(diceSides);
                dice.Controls.Add(LabelAt("Success rule", 18, 102)); diceMatch = Combo(new string[] { "Exact", "Range", "Combination" }, 18, 123, 180); dice.Controls.Add(diceMatch);
                dice.Controls.Add(LabelAt("Target or range (example 5-8)", 215, 102)); diceTarget = Box("6", 215, 123, 185); dice.Controls.Add(diceTarget);
                dice.Controls.Add(LabelAt("Reward", 18, 168)); diceReward = Box("Jackpot", 18, 189, 382); dice.Controls.Add(diceReward);
                dice.Controls.Add(LabelAt("Success message", 18, 234)); diceSuccess = Box("JACKPOT!", 18, 255, 382); dice.Controls.Add(diceSuccess);
                dice.Controls.Add(LabelAt("Failure message", 18, 300)); diceFailure = Box("Failed attempt - try again", 18, 321, 382); dice.Controls.Add(diceFailure);
                dice.Controls.Add(LabelAt("Outcome editor: roll=reward", 18, 366)); diceOutcomes = MultiBox("1=Small Reward\r\n2=Shoutout\r\n3=Song Choice\r\n4=Bonus\r\n5=Special\r\n6=Jackpot", 18, 389, 382, 105); dice.Controls.Add(diceOutcomes);
                dice.Controls.Add(ButtonAt("ROLL DICE", 245, 510, 155, delegate { RollDice(); }));

                var wheels = Group("BUILT-IN WHEEL + 1920 x 1080 HTML WHEEL CATALOG", 470, 15, 460, 570); page.Controls.Add(wheels);
                wheels.Controls.Add(LabelAt("HTML wheel overlays (maximum 20 backups)", 18, 34));
                wheelScriptSelector = Combo(new string[] { }, 18, 56, 270); wheels.Controls.Add(wheelScriptSelector);
                wheelScriptName = Box("My Seasonal Wheel", 298, 56, 140); wheels.Controls.Add(wheelScriptName);
                wheels.Controls.Add(ButtonAt("UPLOAD HTML", 18, 96, 125, delegate { UploadWheelScript(); }));
                wheels.Controls.Add(ButtonAt("LOAD SCRIPT", 151, 96, 125, delegate { LoadSelectedWheelScript(); }));
                wheels.Controls.Add(ButtonAt("DELETE SCRIPT", 284, 96, 135, delegate { DeleteSelectedWheelScript(); }));
                wheels.Controls.Add(ButtonAt("RUN HTML WHEEL", 264, 140, 155, delegate { SpinSelectedWheelScript(); }));
                wheelScriptStatus = LabelAt("Backups: backupscripts_sw", 18, 148); wheelScriptStatus.Size = new Size(235, 40); wheelScriptStatus.AutoSize = false; wheels.Controls.Add(wheelScriptStatus);
                wheels.Controls.Add(LabelAt("Built-in prize wheels (optional, 2-20 prizes): NAME|Prize|Prize", 18, 190));
                string defaultWheels = "MAIN|Song Request|Special Shoutout|Bonus Surprise|Spin Again\r\nVIP|VIP Message|Choose My Look|Pick the Theme|Jackpot\r\nSPECIAL|Dance Break|Secret Reward|Double Prize|Try Again";
                string savedWheelsPath = owner.StudioPath("wheels-v3.txt");
                string savedWheels = File.Exists(savedWheelsPath) ? File.ReadAllText(savedWheelsPath) : "";
                wheelsEditor = MultiBox(String.IsNullOrWhiteSpace(savedWheels) ? defaultWheels : savedWheels, 18, 214, 420, 150); wheels.Controls.Add(wheelsEditor);
                wheels.Controls.Add(LabelAt("Selected built-in wheel", 18, 380)); wheelSelector = Combo(new string[] { "MAIN", "VIP", "SPECIAL" }, 18, 402, 190); wheels.Controls.Add(wheelSelector);
                wheels.Controls.Add(ButtonAt("SAVE PRIZE WHEELS", 18, 446, 175, delegate { SaveWheels(); }));
                wheels.Controls.Add(ButtonAt("SPIN BUILT-IN", 203, 446, 175, delegate { SpinSelectedWheel(); }));
                Dictionary<string, string[]> loadedWheels = ParseWheels();
                wheelSelector.Items.Clear();
                foreach (string loadedWheelName in loadedWheels.Keys) wheelSelector.Items.Add(loadedWheelName);
                if (wheelSelector.Items.Count > 0) wheelSelector.SelectedIndex = 0;
                wheels.Controls.Add(LabelAt("All uploads, loads, spins, dice rolls, and actions are manual.", 18, 510));
                RefreshWheelScripts();
                page.Controls.Add(BuildGameResultSettingsGroup());
                page.Controls.Add(BuildHtmlGameModulesGroup());
                return page;
            }

            private TabPage BuildRulesTab()
            {
                var page = Page("ALERTS + LEADERBOARD");
                var alerts = Group("ALERTS + LEADERBOARD", 15, 15, 915, 255); page.Controls.Add(alerts);
                alerts.Controls.Add(LabelAt("Alert duration (seconds)", 18, 40)); alertDuration = NumberAt(6, 1, 30, 18, 63, 145, 0); alerts.Controls.Add(alertDuration);
                alerts.Controls.Add(LabelAt("Animation", 190, 40)); alertAnimation = Combo(new string[] { "Fade", "Slide", "Zoom" }, 190, 63, 145); alertAnimation.SelectedItem = "Slide"; alerts.Controls.Add(alertAnimation);
                alertSound = new CheckBox { Text = "Alert sound", Location = new Point(370, 62), AutoSize = true, ForeColor = TextColor }; alerts.Controls.Add(alertSound);
                alerts.Controls.Add(LabelAt("Maximum Top Tippers", 510, 40)); leaderboardMaximum = NumberAt(5, 1, 10, 510, 63, 145, 0); alerts.Controls.Add(leaderboardMaximum);
                alerts.Controls.Add(ButtonAt("SAVE SETTINGS", 700, 57, 193, delegate { SaveRuleSettings(); }));
                alerts.Controls.Add(ButtonAt("RESET TODAY", 510, 125, 170, delegate { owner.server.Publish("leaderboard-reset", "{\"scope\":\"today\"}"); }));
                alerts.Controls.Add(ButtonAt("RESET ALL", 690, 125, 170, delegate { owner.server.Publish("leaderboard-reset", "{\"scope\":\"all\"}"); }));
                alerts.Controls.Add(LabelAt("Controls alert timing, animation, optional sound, visible Top Tippers count, and supporter-total resets.", 18, 195));
                return page;
            }

            private TabPage BuildLayoutTab()
            {
                var page = Page("LAYOUT + THEMES");
                var presets = Group("LAYOUT PRESETS", 15, 15, 915, 150); page.Controls.Add(presets);
                presets.Controls.Add(LabelAt("Layout", 18, 36)); layoutPreset = Combo(new string[] { "Creator", "Full", "Minimal", "Chat" }, 18, 58, 180); presets.Controls.Add(layoutPreset);
                presets.Controls.Add(ButtonAt("APPLY LAYOUT", 215, 55, 165, delegate { string value = layoutPreset.Text.ToLowerInvariant(); File.WriteAllText(owner.StudioPath("layout-v3.txt"), value); owner.server.Publish("layout", "{\"layout\":\"" + value + "\"}"); }));
                presets.Controls.Add(LabelAt("Theme", 430, 36)); themePreset = Combo(new string[] { "Neon", "Cyber Blue", "Halloween", "Christmas", "Minimal", "Dark", "Custom" }, 430, 58, 180); presets.Controls.Add(themePreset);
                presets.Controls.Add(ButtonAt("APPLY THEME", 625, 55, 165, delegate { string value = themePreset.Text.ToLowerInvariant().Replace(" ", "-"); File.WriteAllText(owner.StudioPath("theme-v3.txt"), value); owner.server.Publish("theme-preset", "{\"preset\":\"" + value + "\"}"); }));
                presets.Controls.Add(LabelAt("Creator Focus is the default. Position changes below are saved per element and use the 1920 × 1080 overlay canvas.", 18, 108));

                var module = Group("OVERLAY POSITION DESIGNER • 1920 × 1080 • PIXEL CONTROL", 15, 185, 915, 390); page.Controls.Add(module);

                module.Controls.Add(LabelAt("Overlay element", 18, 35));
                moduleName = Combo(new string[] {
                    "Brand Panel", "Camera Frame", "Token Goal", "Top Tippers / Fans",
                    "Last Tipper", "Recent Supporter", "Tip Ticker", "Alert Display",
                    "Game Overlay Zone", "VIP Badge", "DMCA Watermark", "Background"
                }, 18, 57, 195);
                module.Controls.Add(moduleName);

                module.Controls.Add(LabelAt("X position offset (px)", 230, 35));
                moduleX = NumberAt(0, -1500, 1500, 230, 57, 120, 0); module.Controls.Add(moduleX);

                module.Controls.Add(LabelAt("Y position offset (px)", 365, 35));
                moduleY = NumberAt(0, -900, 900, 365, 57, 120, 0); module.Controls.Add(moduleY);

                module.Controls.Add(LabelAt("Nudge step (px)", 500, 35));
                moduleMoveStep = NumberAt(10, 1, 500, 500, 57, 105, 0); module.Controls.Add(moduleMoveStep);

                module.Controls.Add(LabelAt("Size %", 620, 35));
                moduleScale = NumberAt(100, 25, 300, 620, 57, 90, 0); moduleScale.Increment = 5; module.Controls.Add(moduleScale);

                module.Controls.Add(LabelAt("Opacity %", 725, 35));
                moduleOpacity = NumberAt(100, 0, 100, 725, 57, 90, 0); moduleOpacity.Increment = 5; module.Controls.Add(moduleOpacity);

                module.Controls.Add(LabelAt("Width %", 825, 35));
                moduleWidth = NumberAt(0, 0, 100, 825, 57, 70, 0); module.Controls.Add(moduleWidth);

                module.Controls.Add(ButtonAt("← LEFT", 18, 112, 125, delegate { NudgeSelectedModule(-1, 0); }));
                module.Controls.Add(ButtonAt("RIGHT →", 153, 112, 125, delegate { NudgeSelectedModule(1, 0); }));
                module.Controls.Add(ButtonAt("↑ UP", 288, 112, 125, delegate { NudgeSelectedModule(0, -1); }));
                module.Controls.Add(ButtonAt("DOWN ↓", 423, 112, 125, delegate { NudgeSelectedModule(0, 1); }));
                module.Controls.Add(ButtonAt("APPLY / SAVE", 568, 112, 150, delegate { ApplyModuleStyle(); }));
                module.Controls.Add(ButtonAt("RESET SELECTED", 728, 112, 167, delegate {
                    moduleX.Value = 0; moduleY.Value = 0; moduleWidth.Value = 0;
                    moduleScale.Value = 100; moduleOpacity.Value = 100;
                    ApplyModuleStyle();
                }));

                module.Controls.Add(LabelAt("Examples: X = -110 moves left 110 px • X = 110 moves right • Y = -80 moves up • Y = 80 moves down.", 18, 170));
                module.Controls.Add(LabelAt("Size and opacity are percentages. Width 0 keeps the overlay's original width. Nudge buttons save immediately.", 18, 197));

                modulePositionStatus = LabelAt("Selected element: loading saved position…", 18, 235);
                modulePositionStatus.ForeColor = Color.LightGreen;
                modulePositionStatus.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                module.Controls.Add(modulePositionStatus);

                module.Controls.Add(LabelAt("Every item remains independently visible/hidden from the Control Deck. This designer only changes presentation.", 18, 270));
                safeButton = ButtonAt("STREAM SAFE MODE", 18, 310, 190, delegate { owner.ToggleStreamSafeMode(); }); safeButtons.Add(safeButton); module.Controls.Add(safeButton);

                moduleName.SelectedIndexChanged += delegate { LoadSelectedModuleStyle(); };
                LoadSelectedModuleStyle();

                page.Controls.Add(BuildTemplateGroup(595));
                return page;
            }

            private TabPage BuildSessionTab()
            {
                var page = Page("SESSION + BACKUP");
                var session = Group("SESSION DATA", 15, 15, 915, 190); page.Controls.Add(session);
                session.Controls.Add(ButtonAt("SAVE SESSION", 18, 55, 165, delegate { SaveSession(); }));
                session.Controls.Add(ButtonAt("LOAD SESSION", 193, 55, 165, delegate { LoadSession(); }));
                session.Controls.Add(ButtonAt("RESET SESSION", 368, 55, 165, delegate { owner.server.Publish("session-reset", "{}"); tipCount = tokenCount = diceRollStats = wheelCount = 0; RefreshStats(); }));
                session.Controls.Add(LabelAt("The overlay automatically tracks tips, supporter totals, dice rolls, wheel spins, goals, and viewer levels.", 18, 120));

                var profile = Group("EXPORT / IMPORT PROFILE", 15, 225, 915, 180); page.Controls.Add(profile);
                profile.Controls.Add(ButtonAt("EXPORT PROFILE ZIP", 18, 58, 200, delegate { owner.ExportStudioProfile(); }));
                profile.Controls.Add(ButtonAt("IMPORT PROFILE ZIP", 230, 58, 200, delegate { owner.ImportStudioProfile(); }));
                profile.Controls.Add(LabelAt("Profile ZIP includes session settings, layouts, themes, UI skins, wheels, actions, six ticker slots, and the one DMCA HTML backup.", 18, 118));

                var connector = Group("MANUAL CONTROL MODE", 15, 425, 915, 150); page.Controls.Add(connector);
                connector.Controls.Add(LabelAt("Creator Cam is currently button-controlled from the Control Deck and Backstage Dashboard.", 18, 38));
                connector.Controls.Add(LabelAt("No Chaturbate, Stripchat, or other adult-site API is connected in this version.", 18, 72));
                connector.Controls.Add(LabelAt("The modular foundation is preserved for a future connector upgrade without changing today's workflow.", 18, 106));
                return page;
            }

            private GroupBox Group(string text, int x, int y, int width, int height) { return new GroupBox { Text = text, Location = new Point(x, y), Size = new Size(width, height), ForeColor = Orange, BackColor = Panel, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) }; }
            private Label LabelAt(string text, int x, int y) { return new Label { Text = text, Location = new Point(x, y), AutoSize = true, ForeColor = TextColor, Font = new Font("Segoe UI", 9, FontStyle.Regular) }; }
            private TextBox Box(string text, int x, int y, int width) { return new TextBox { Text = text, Location = new Point(x, y), Width = width, BackColor = Color.FromArgb(15, 17, 15), ForeColor = TextColor, BorderStyle = BorderStyle.FixedSingle }; }
            private TextBox MultiBox(string text, int x, int y, int width, int height) { return new TextBox { Text = text, Location = new Point(x, y), Size = new Size(width, height), Multiline = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(15, 17, 15), ForeColor = TextColor, BorderStyle = BorderStyle.FixedSingle }; }
            private ComboBox Combo(string[] items, int x, int y, int width) { var box = new ComboBox { Location = new Point(x, y), Width = width, DropDownStyle = ComboBoxStyle.DropDownList }; box.Items.AddRange(items); if (box.Items.Count > 0) box.SelectedIndex = 0; return box; }
            private NumericUpDown NumberAt(decimal value, decimal min, decimal max, int x, int y, int width, int decimals) { return new NumericUpDown { Value = value, Minimum = min, Maximum = max, Location = new Point(x, y), Width = width, DecimalPlaces = decimals }; }
            private Button ButtonAt(string text, int x, int y, int width, EventHandler click) { var button = new Button { Text = text, Location = new Point(x, y), Size = new Size(width, 36), BackColor = Orange, ForeColor = Color.FromArgb(25, 20, 15), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Cursor = Cursors.Hand }; button.Click += click; return button; }
            private void StatusLabel(Label label, string text, int x, int y, Control parent, int width = 410)
            {
                label.Text = text;
                label.Location = new Point(x, y);
                label.Size = new Size(width, 48);
                label.AutoSize = false;
                label.AutoEllipsis = false;
                label.UseMnemonic = false;
                label.ForeColor = TextColor;
                label.Font = new Font("Segoe UI", 9, FontStyle.Regular);
                parent.Controls.Add(label);
            }

            private void SetEvent(string value) { currentEvent.Text = "Current Event: " + value; }
            private void RefreshStats() { statistics.Text = "Today: " + tipCount + " tips • " + tokenCount + " tokens • " + diceRollStats + " dice • " + wheelCount + " wheels"; }

            private void FakeTip()
            {
                string username = (simulatorName.Text ?? "").Trim();
                if (username.Length == 0) { MessageBox.Show("Enter a viewer username before sending a manual test event."); return; }
                int amount; if (!Int32.TryParse(simulatorAmount.Text, out amount) || amount < 0) { MessageBox.Show("Enter a valid fake tip amount."); return; }
                owner.PublishStudioTip(username, amount); tipCount++; tokenCount += amount;
                lastTip.Text = "Last Tip: " + username + " • " + amount + " tokens"; topSupporter.Text = "Top Supporter: " + username; SetEvent("Fake tip"); RefreshStats();
            }

            private void FakeFollow() { owner.PublishStudioFollow(simulatorName.Text); SetEvent("Fake follow"); }

            private void RollDice()
            {
                try
                {
                    if (diceName == null)
                    {
                        owner.PublishStudioDice(simulatorName.Text, 1, 6, "exact", "6", "Jackpot", "JACKPOT!", "Failed attempt", "1=Small Reward\n2=Shoutout\n3=Song Choice\n4=Bonus\n5=Special\n6=Jackpot");
                    }
                    else
                    {
                        int sides;
                        if (!Int32.TryParse((diceSides.Text ?? "D6").TrimStart('D', 'd'), out sides) || (sides != 6 && sides != 12 && sides != 20)) sides = 6;
                        File.WriteAllLines(owner.StudioPath("dice-rules-v3.txt"), new string[] { diceName.Text, diceAmount.Value.ToString(), "D" + sides, diceMatch.Text, diceTarget.Text, diceReward.Text, diceSuccess.Text, diceFailure.Text, Convert.ToBase64String(Encoding.UTF8.GetBytes(diceOutcomes.Text)) });
                        owner.PublishStudioDice(diceName.Text, (int)diceAmount.Value, sides, diceMatch.Text, diceTarget.Text, diceReward.Text, diceSuccess.Text, diceFailure.Text, diceOutcomes.Text);
                    }
                    diceRollStats++;
                    currentGame.Text = "Current Game: Dice rolling";
                    SetEvent("Dice game");
                    RefreshStats();
                }
                catch (Exception error)
                {
                    MessageBox.Show("The Dice game could not start.\r\n\r\n" + error.Message, "Creator Cam Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            private Dictionary<string, string[]> ParseWheels()
            {
                var wheels = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                if (wheelsEditor == null) return wheels;
                foreach (string line in wheelsEditor.Lines)
                {
                    string[] parts = line.Split('|'); if (parts.Length < 3) continue;
                    var prizes = new List<string>(); for (int i = 1; i < parts.Length && prizes.Count < 20; i++) if (parts[i].Trim().Length > 0) prizes.Add(parts[i].Trim());
                    if (prizes.Count >= 2) wheels[parts[0].Trim()] = prizes.ToArray();
                }
                return wheels;
            }

            private void SaveWheels()
            {
                Dictionary<string, string[]> wheels = ParseWheels(); if (wheels.Count == 0) { MessageBox.Show("Enter at least one valid wheel."); return; }
                File.WriteAllText(owner.StudioPath("wheels-v3.txt"), wheelsEditor.Text);
                wheelSelector.Items.Clear(); foreach (string name in wheels.Keys) wheelSelector.Items.Add(name); if (wheelSelector.Items.Count > 0) wheelSelector.SelectedIndex = 0;
                SetEvent("Built-in prize wheels saved");
            }

            private void SpinSelectedWheel()
            {
                Dictionary<string, string[]> wheels = ParseWheels(); string name = wheelSelector.Text;
                if (!wheels.ContainsKey(name)) { MessageBox.Show("Save and select a valid wheel first."); return; }
                owner.PublishStudioWheel(diceName == null ? "" : diceName.Text, name, wheels[name]);
                wheelCount++;
                currentGame.Text = "Current Game: " + name + " built-in wheel";
                SetEvent("Built-in wheel started");
                RefreshStats();
            }

            private GameModuleChoice SelectedWheelScript()
            {
                return wheelScriptSelector == null ? null : wheelScriptSelector.SelectedItem as GameModuleChoice;
            }

            private void RefreshWheelScripts()
            {
                if (wheelScriptSelector == null) return;
                string selectedId = "";
                var selected = SelectedWheelScript();
                if (selected != null) selectedId = selected.Id;
                wheelScriptSelector.Items.Clear();
                List<string[]> scripts = owner.WheelScriptCatalog();
                foreach (string[] row in scripts)
                {
                    var choice = new GameModuleChoice(row[0], row[1]);
                    wheelScriptSelector.Items.Add(choice);
                    if (String.Equals(choice.Id, selectedId, StringComparison.OrdinalIgnoreCase)) wheelScriptSelector.SelectedItem = choice;
                }
                if (wheelScriptSelector.SelectedIndex < 0 && wheelScriptSelector.Items.Count > 0) wheelScriptSelector.SelectedIndex = 0;
                if (wheelScriptStatus != null) wheelScriptStatus.Text = "Backups: backupscripts_sw  •  " + scripts.Count + "/20";
            }

            private void UploadWheelScript()
            {
                string id = owner.InstallHtmlGameModule("Wheel", wheelScriptName.Text);
                if (id.Length == 0) return;
                RefreshWheelScripts();
                foreach (object item in wheelScriptSelector.Items)
                {
                    var choice = item as GameModuleChoice;
                    if (choice != null && String.Equals(choice.Id, id, StringComparison.OrdinalIgnoreCase)) { wheelScriptSelector.SelectedItem = choice; break; }
                }
                wheelScriptStatus.Text = "Uploaded and backed up. Click SPIN WHEEL.";
                SetEvent("Wheel script uploaded");
            }

            private void LoadSelectedWheelScript()
            {
                var choice = SelectedWheelScript();
                if (choice == null) { MessageBox.Show("Choose a wheel script first."); return; }
                if (!owner.LoadWheelScript(choice.Id)) { MessageBox.Show("The selected wheel backup could not be loaded."); return; }
                wheelScriptStatus.Text = "Loaded: " + choice.Name;
                SetEvent("Wheel script loaded");
            }

            private void DeleteSelectedWheelScript()
            {
                var choice = SelectedWheelScript();
                if (choice == null) { MessageBox.Show("Choose a wheel script first."); return; }
                if (!owner.DeleteWheelScript(choice.Id, this)) return;
                RefreshWheelScripts();
                wheelScriptStatus.Text = "Wheel script deleted.";
                SetEvent("Wheel script deleted");
            }

            private void SpinSelectedWheelScript()
            {
                var choice = SelectedWheelScript();
                if (choice == null) { MessageBox.Show("Choose a wheel script first."); return; }
                if (!owner.LoadWheelScript(choice.Id)) { MessageBox.Show("The selected wheel backup could not be loaded."); return; }
                wheelScriptStatus.Text = "Starting: " + choice.Name;
                var launchTimer = new Timer { Interval = 900 };
                launchTimer.Tick += delegate
                {
                    launchTimer.Stop();
                    launchTimer.Dispose();
                    owner.server.Publish("module-action", "{\"id\":\"" + ControlDeckForm.Json(choice.Id) + "\",\"action\":\"run\",\"duration\":30,\"name\":\"" + ControlDeckForm.Json(choice.Name) + "\"}");
                    wheelScriptStatus.Text = "Running: " + choice.Name;
                };
                launchTimer.Start();
                wheelCount++;
                currentGame.Text = "Current Game: " + choice.Name;
                SetEvent("Manual wheel script started");
                RefreshStats();
            }

            private void SaveTipRules()
            {
                if (tipRules == null) return;
                var rules = new List<string>();
                foreach (string line in tipRules.Lines)
                {
                    string[] parts = line.Split('|'); int amount;
                    if (parts.Length < 3 || !Int32.TryParse(parts[0].Trim(), out amount)) continue;
                    rules.Add("{\"amount\":" + amount + ",\"action\":\"" + ControlDeckForm.Json(parts[1]) + "\",\"reward\":\"" + ControlDeckForm.Json(parts[2]) + "\"}");
                }
                if (rules.Count == 0) { MessageBox.Show("Enter at least one valid tip rule."); return; }
                File.WriteAllText(owner.StudioPath("tip-rules-v3.txt"), tipRules.Text);
                owner.server.Publish("tip-rules", "{\"rules\":[" + String.Join(",", rules.ToArray()) + "]}");
            }

            private void SaveRuleSettings()
            {
                string animation = alertAnimation.Text.ToLowerInvariant();
                owner.server.Publish("studio-settings", "{\"alertSettings\":{\"duration\":" + ((int)alertDuration.Value * 1000) + ",\"animation\":\"" + animation + "\",\"sound\":" + alertSound.Checked.ToString().ToLowerInvariant() + "},\"leaderboardMaximum\":" + (int)leaderboardMaximum.Value + "}");
                File.WriteAllText(owner.StudioPath("alert-settings-v3.txt"), alertDuration.Value + "\t" + animation + "\t" + alertSound.Checked + "\t" + leaderboardMaximum.Value);
            }

            private void LoadSelectedModuleStyle()
            {
                if (moduleName == null || moduleX == null || moduleY == null || moduleScale == null || moduleOpacity == null || moduleWidth == null) return;

                moduleX.Value = 0;
                moduleY.Value = 0;
                moduleScale.Value = 100;
                moduleOpacity.Value = 100;
                moduleWidth.Value = 0;

                string moduleKey = ModuleKey(moduleName.Text);
                string path = owner.StudioPath("module-styles-v3.txt");

                try
                {
                    if (File.Exists(path))
                    {
                        foreach (string line in File.ReadAllLines(path))
                        {
                            string[] parts = line.Split('|');
                            if (parts.Length < 6 || !parts[0].Equals(moduleKey, StringComparison.OrdinalIgnoreCase)) continue;

                            decimal x, y, scale, opacity, width;
                            if (Decimal.TryParse(parts[1], out x)) moduleX.Value = Math.Max(moduleX.Minimum, Math.Min(moduleX.Maximum, x));
                            if (Decimal.TryParse(parts[2], out y)) moduleY.Value = Math.Max(moduleY.Minimum, Math.Min(moduleY.Maximum, y));
                            if (Decimal.TryParse(parts[3], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out scale))
                                moduleScale.Value = Math.Max(moduleScale.Minimum, Math.Min(moduleScale.Maximum, Decimal.Round(scale * 100m, 0)));
                            if (Decimal.TryParse(parts[4], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out opacity))
                                moduleOpacity.Value = Math.Max(moduleOpacity.Minimum, Math.Min(moduleOpacity.Maximum, Decimal.Round(opacity * 100m, 0)));
                            if (Decimal.TryParse(parts[5], out width)) moduleWidth.Value = Math.Max(moduleWidth.Minimum, Math.Min(moduleWidth.Maximum, width));
                            break;
                        }
                    }
                }
                catch { }

                UpdateModulePositionStatus();
            }

            private void NudgeSelectedModule(int xDirection, int yDirection)
            {
                if (moduleMoveStep == null) return;
                decimal step = moduleMoveStep.Value;
                decimal nextX = moduleX.Value + (xDirection * step);
                decimal nextY = moduleY.Value + (yDirection * step);
                moduleX.Value = Math.Max(moduleX.Minimum, Math.Min(moduleX.Maximum, nextX));
                moduleY.Value = Math.Max(moduleY.Minimum, Math.Min(moduleY.Maximum, nextY));
                ApplyModuleStyle();
            }

            private void UpdateModulePositionStatus()
            {
                if (modulePositionStatus == null || moduleName == null) return;
                modulePositionStatus.Text = moduleName.Text + " • X " + moduleX.Value + " px • Y " + moduleY.Value +
                    " px • Size " + moduleScale.Value + "% • Opacity " + moduleOpacity.Value + "% • Width " +
                    (moduleWidth.Value == 0 ? "default" : moduleWidth.Value + "%");
            }

            private void ApplyModuleStyle()
            {
                string moduleKey = ModuleKey(moduleName.Text);
                decimal scale = moduleScale.Value / 100m;
                decimal opacity = moduleOpacity.Value / 100m;

                string value = moduleKey + "|" + moduleX.Value + "|" + moduleY.Value + "|" +
                    scale.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" +
                    opacity.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + moduleWidth.Value;

                string path = owner.StudioPath("module-styles-v3.txt");
                var lines = new List<string>();
                if (File.Exists(path)) lines.AddRange(File.ReadAllLines(path));
                lines.RemoveAll(delegate(string line) { return line.StartsWith(moduleKey + "|", StringComparison.OrdinalIgnoreCase); });
                lines.Add(value);
                File.WriteAllLines(path, lines.ToArray());

                owner.server.Publish("module-style", "{\"module\":\"" + moduleKey + "\",\"x\":" + moduleX.Value +
                    ",\"y\":" + moduleY.Value + ",\"scale\":" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    ",\"opacity\":" + opacity.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    ",\"width\":" + moduleWidth.Value + "}");

                UpdateModulePositionStatus();
            }

            private void SaveSession()
            {
                File.WriteAllText(owner.StudioPath("session-v3.txt"), tipCount + "\t" + tokenCount + "\t" + diceRollStats + "\t" + wheelCount);
                owner.server.Publish("alert", "{\"name\":\"Session Saved\",\"message\":\"Backstage statistics saved.\"}");
            }

            private void LoadSession()
            {
                string path = owner.StudioPath("session-v3.txt"); if (!File.Exists(path)) { MessageBox.Show("No saved session was found."); return; }
                string[] parts = File.ReadAllText(path).Split('\t');
                if (parts.Length >= 4) { Int32.TryParse(parts[0], out tipCount); Int32.TryParse(parts[1], out tokenCount); Int32.TryParse(parts[2], out diceRollStats); Int32.TryParse(parts[3], out wheelCount); }
                RefreshStats(); owner.server.Publish("session-load", "{}");
            }

            private void LoadSavedEditors()
            {
                try { string path = owner.StudioPath("wheels-v3.txt"); if (File.Exists(path)) wheelsEditor.Text = File.ReadAllText(path); SaveWheels(); } catch { }
                try { string path = owner.StudioPath("tip-rules-v3.txt"); if (tipRules != null && File.Exists(path)) tipRules.Text = File.ReadAllText(path); } catch { }
                try
                {
                    string path = owner.StudioPath("dice-rules-v3.txt"); if (File.Exists(path))
                    {
                        string[] values = File.ReadAllLines(path); decimal count;
                        if (values.Length > 0) diceName.Text = String.Equals(values[0], "TestViewer", StringComparison.OrdinalIgnoreCase) ? "" : values[0]; if (values.Length > 1 && Decimal.TryParse(values[1], out count)) diceAmount.Value = Math.Max(diceAmount.Minimum, Math.Min(diceAmount.Maximum, count));
                        if (values.Length > 2) diceSides.SelectedItem = values[2]; if (values.Length > 3) diceMatch.SelectedItem = values[3]; if (values.Length > 4) diceTarget.Text = values[4];
                        if (values.Length > 5) diceReward.Text = values[5]; if (values.Length > 6) diceSuccess.Text = values[6]; if (values.Length > 7) diceFailure.Text = values[7];
                        if (values.Length > 8) diceOutcomes.Text = Encoding.UTF8.GetString(Convert.FromBase64String(values[8]));
                    }
                }
                catch { }
                try { string path = owner.StudioPath("layout-v3.txt"); if (File.Exists(path)) layoutPreset.SelectedItem = Char.ToUpper(File.ReadAllText(path)[0]) + File.ReadAllText(path).Substring(1); } catch { }
                try { string path = owner.StudioPath("theme-v3.txt"); if (File.Exists(path)) themePreset.SelectedItem = Char.ToUpper(File.ReadAllText(path)[0]) + File.ReadAllText(path).Substring(1); } catch { }
                try
                {
                    string path = owner.StudioPath("alert-settings-v3.txt"); if (!File.Exists(path)) return;
                    string[] values = File.ReadAllText(path).Split('\t');
                    decimal number; if (values.Length > 0 && Decimal.TryParse(values[0], out number)) alertDuration.Value = Math.Max(alertDuration.Minimum, Math.Min(alertDuration.Maximum, number));
                    if (values.Length > 1) alertAnimation.SelectedItem = Char.ToUpper(values[1][0]) + values[1].Substring(1);
                    bool sound; if (values.Length > 2 && Boolean.TryParse(values[2], out sound)) alertSound.Checked = sound;
                    if (values.Length > 3 && Decimal.TryParse(values[3], out number)) leaderboardMaximum.Value = Math.Max(leaderboardMaximum.Minimum, Math.Min(leaderboardMaximum.Maximum, number));
                }
                catch { }
            }

            internal void UpdateSafeMode(bool enabled)
            {
                foreach (Button button in safeButtons) { button.Text = enabled ? "SAFE MODE: ON" : "STREAM SAFE MODE"; button.BackColor = enabled ? Color.FromArgb(200, 65, 75) : Orange; }
                SetEvent(enabled ? "Stream Safe Mode enabled" : "Stream Safe Mode disabled");
            }
        }
    }
}

namespace CreatorCamOverlayKit
{
    internal static partial class Program
    {
        internal sealed partial class ControlDeckForm
        {
            private string EvolutionBootstrapFragment()
            {
                var supporters = new List<string>();
                foreach (CrewMember member in crew)
                {
                    supporters.Add("{\"name\":\"" + Json(member.Name) + "\",\"role\":\"" + Json(member.Role) + "\",\"level\":\"" + Json(member.Level.ToLowerInvariant()) + "\",\"lifetimeSupport\":" + Math.Max(0, member.LifetimeSupport) + "}");
                }

                int resultDuration = 20, fadeDuration = 2;
                string resultAnimation = "slide";
                bool showUsername = true, showPrize = true, showResult = true;
                string gamePath = StudioPath("game-result-v3.txt");
                if (File.Exists(gamePath))
                {
                    string[] values = File.ReadAllText(gamePath).Split('\t');
                    if (values.Length > 0) Int32.TryParse(values[0], out resultDuration);
                    if (values.Length > 1) Int32.TryParse(values[1], out fadeDuration);
                    if (values.Length > 2) resultAnimation = Clean(values[2]).ToLowerInvariant();
                    if (values.Length > 3) Boolean.TryParse(values[3], out showUsername);
                    if (values.Length > 4) Boolean.TryParse(values[4], out showPrize);
                    if (values.Length > 5) Boolean.TryParse(values[5], out showResult);
                }
                resultDuration = Math.Max(2, Math.Min(120, resultDuration));
                fadeDuration = Math.Max(1, Math.Min(10, fadeDuration));

                string tickerAnimation = "scroll", tickerSource = "activity";
                string tickerSettings = StudioPath("ticker-settings-v3.txt");
                if (File.Exists(tickerSettings))
                {
                    string[] values = File.ReadAllText(tickerSettings).Split('\t');
                    if (values.Length > 0) tickerAnimation = Clean(values[0]).ToLowerInvariant();
                    if (values.Length > 1) tickerSource = Clean(values[1]).ToLowerInvariant();
                }

                var enabledModules = LoadEnabledModules();
                string enabledJson = JsonArray(enabledModules.ToArray());
                string actionsJson = ActionDefinitionsJson();
                string activeTemplate = StudioPath("active-template-v3.json");
                string templateJson = File.Exists(activeTemplate) && IsTemplateJson(File.ReadAllText(activeTemplate)) ? File.ReadAllText(activeTemplate) : "null";

                bool lastTipperVisible = true, lastSupporterVisible = true, vipBadgeVisible = true;
                string visibilityPath = StudioPath("supporter-overlay-visibility-v1.txt");
                if (File.Exists(visibilityPath))
                {
                    string[] visibility = File.ReadAllText(visibilityPath).Split('\t');
                    if (visibility.Length > 0) Boolean.TryParse(visibility[0], out lastTipperVisible);
                    if (visibility.Length > 1) Boolean.TryParse(visibility[1], out lastSupporterVisible);
                    if (visibility.Length > 2) Boolean.TryParse(visibility[2], out vipBadgeVisible);
                }

                return ",\"supporters\":[" + String.Join(",", supporters.ToArray()) + "]" +
                    ",\"recentSupporter\":" + ManualRecentSupporterJson() +
                    ",\"supporterOverlayVisibility\":{\"lastTipper\":" + lastTipperVisible.ToString().ToLowerInvariant() + ",\"lastSupporter\":" + lastSupporterVisible.ToString().ToLowerInvariant() + ",\"vipBadge\":" + vipBadgeVisible.ToString().ToLowerInvariant() + "}" +
                    ",\"gameResultSettings\":{\"duration\":" + (resultDuration * 1000) + ",\"fadeDuration\":" + (fadeDuration * 1000) + ",\"animation\":\"" + Json(resultAnimation) + "\",\"showUsername\":" + showUsername.ToString().ToLowerInvariant() + ",\"showPrize\":" + showPrize.ToString().ToLowerInvariant() + ",\"showResult\":" + showResult.ToString().ToLowerInvariant() + "}" +
                    ",\"tickerSettings\":{\"animation\":\"" + Json(tickerAnimation) + "\",\"source\":\"" + Json(tickerSource) + "\"}" +
                    ",\"dmcaSettings\":" + MovingWatermarkSettingsJson() +
                    ",\"actions\":[" + actionsJson + "]" +
                    ",\"enabledModules\":" + enabledJson +
                    ",\"secretShowSettings\":" + SecretShowSettingsJson() +
                    ",\"template\":" + templateJson;
            }

            internal string SecretShowSettingsJson()
            {
                string[] values = new string[] {
                    "MY SECRET SHOW", "SECRET SHOW LOCKED", "Tip {price} Tokens to Unlock the Camera", "25",
                    "Waiting for an approved tipper…", "Welcome to My Secret Show", "5", "My Secret Show Blue", "Medium", "True", "100", "35", "3"
                };
                try
                {
                    string path = StudioPath("my-secret-show-v4.txt");
                    if (File.Exists(path))
                    {
                        string[] saved = File.ReadAllText(path).Split('\t');
                        for (int index = 0; index < values.Length && index < saved.Length; index++) if (!String.IsNullOrWhiteSpace(saved[index])) values[index] = Clean(saved[index]);
                    }
                }
                catch { }
                int price, duration, teaseDuration;
                if (!Int32.TryParse(values[3], out price)) price = 25;
                if (!Int32.TryParse(values[6], out duration)) duration = 5;
                if (!Int32.TryParse(values[12], out teaseDuration)) teaseDuration = 3;
                price = Math.Max(1, Math.Min(999999, price)); duration = Math.Max(4, Math.Min(6, duration));
                teaseDuration = Math.Max(1, Math.Min(180, teaseDuration));
                string themeText = values[7].ToLowerInvariant();
                string theme = themeText.Contains("purple") ? "purple" : themeText.Contains("emerald") ? "emerald" : themeText.Contains("crimson") ? "crimson" : "blue";
                string intensityText = values[8].ToLowerInvariant();
                string intensity = intensityText == "off" || intensityText == "low" || intensityText == "medium" || intensityText == "high" ? intensityText : "medium";
                bool rotation = true; Boolean.TryParse(values[9], out rotation);
                return "{\"version\":2,\"theme\":\"" + theme + "\",\"showTitle\":\"" + Json(values[0]) + "\",\"lockedHeadline\":\"" + Json(values[1]) +
                    "\",\"subtitle\":\"" + Json(values[2]) + "\",\"price\":\"" + price + "\",\"waitingMessage\":\"" + Json(values[4]) +
                    "\",\"welcomeMessage\":\"" + Json(values[5]) + "\",\"unlockDuration\":" + duration + ",\"rotationEnabled\":" + rotation.ToString().ToLowerInvariant() +
                    ",\"rotationInterval\":5,\"teaseOpacity\":0,\"teaseDuration\":" + teaseDuration +
                    ",\"effects\":{\"particles\":true,\"grid\":true,\"scan\":true,\"logoPulse\":true,\"border\":true,\"burst\":true,\"intensity\":\"" + intensity + "\",\"backgroundOpacity\":1,\"panelOpacity\":0.84}}";
            }

            private static string ManualRecentSupporterField(string value, int maximum)
            {
                string clean = Clean(value ?? "");
                return clean.Length > maximum ? clean.Substring(0, maximum) : clean;
            }

            internal string[] ManualRecentSupporterValues()
            {
                string[] values = new string[] { "", "0", "MOST RECENT" };
                string path = StudioPath("recent-supporter-v3.txt");
                if (File.Exists(path))
                {
                    string[] saved = File.ReadAllText(path, Encoding.UTF8).Split('\t');
                    if (saved.Length > 0) values[0] = saved[0];
                    if (saved.Length > 1) values[1] = saved[1];
                    if (saved.Length > 2) values[2] = saved[2];
                }
                values[0] = ManualRecentSupporterField(values[0], 64);
                values[2] = ManualRecentSupporterField(values[2], 80);
                int amount;
                if (!Int32.TryParse(values[1], out amount)) amount = 0;
                values[1] = Math.Max(0, Math.Min(999999, amount)).ToString();
                return values;
            }

            internal string ManualRecentSupporterJson()
            {
                string[] values = ManualRecentSupporterValues();
                if (values[0].Length == 0) return "null";
                return "{\"username\":\"" + Json(values[0]) + "\",\"amount\":" + values[1] + ",\"message\":\"" + Json(values[2]) + "\",\"manual\":true,\"at\":0}";
            }

            internal bool SaveManualRecentSupporterSettings(string username, int amount, string message)
            {
                string cleanName = ManualRecentSupporterField(username, 64);
                if (cleanName.Length == 0) return false;
                string[] values = new string[] { cleanName, Math.Max(0, Math.Min(999999, amount)).ToString(), ManualRecentSupporterField(message, 80) };
                File.WriteAllText(StudioPath("recent-supporter-v3.txt"), String.Join("\t", values), new UTF8Encoding(false));
                server.Publish("recent-supporter-manual", "{\"username\":\"" + Json(values[0]) + "\",\"amount\":" + values[1] + ",\"message\":\"" + Json(values[2]) + "\",\"manual\":true,\"at\":0}");
                return true;
            }

            internal void ClearManualRecentSupporterSettings()
            {
                string path = StudioPath("recent-supporter-v3.txt");
                if (File.Exists(path)) File.Delete(path);
                server.Publish("recent-supporter-clear", "{}");
            }

            private static string MovingWatermarkText(string value, string fallback, int maximum)
            {
                string clean = Clean(value ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ").Trim();
                if (clean.Length == 0) clean = fallback;
                return clean.Length > maximum ? clean.Substring(0, maximum) : clean;
            }

            internal string[] MovingWatermarkSettingsValues()
            {
                // Fifth value is opacity percent. Older four-field files remain valid and default to 82%.
                string[] values = new string[] { "OBSIDIAN", "STALLION", "LIVE • VERIFIED • HD", "False", "82" };
                string path = StudioPath("moving-watermark-v4.txt");
                if (File.Exists(path))
                {
                    string[] saved = File.ReadAllText(path).Split('\t');
                    for (int index = 0; index < values.Length && index < saved.Length; index++) if (!String.IsNullOrWhiteSpace(saved[index])) values[index] = saved[index];
                }
                else
                {
                    string oldPath = StudioPath("dmca-v3.txt");
                    if (File.Exists(oldPath))
                    {
                        string[] old = File.ReadAllText(oldPath).Split('\t');
                        if (old.Length > 0 && !String.IsNullOrWhiteSpace(old[0])) values[1] = old[0].TrimStart('@');
                        if (old.Length > 1) values[3] = old[1];
                    }
                }
                values[0] = MovingWatermarkText(values[0], "OBSIDIAN", 32);
                values[1] = MovingWatermarkText(values[1], "STALLION", 40);
                values[2] = MovingWatermarkText(values[2], "LIVE • VERIFIED • HD", 60);
                bool enabled; values[3] = (Boolean.TryParse(values[3], out enabled) && enabled).ToString();
                int opacityPercent;
                if (!Int32.TryParse(values[4], out opacityPercent)) opacityPercent = 82;
                values[4] = Math.Max(10, Math.Min(95, opacityPercent)).ToString();
                return values;
            }

            private string MovingWatermarkSettingsJson(string[] values)
            {
                bool enabled; Boolean.TryParse(values[3], out enabled);
                int opacityPercent;
                if (values.Length < 5 || !Int32.TryParse(values[4], out opacityPercent)) opacityPercent = 82;
                opacityPercent = Math.Max(10, Math.Min(95, opacityPercent));
                string opacity = (opacityPercent / 100.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                return "{\"title\":\"" + Json(values[0]) + "\",\"username\":\"" + Json(values[1]) + "\",\"tagline\":\"" + Json(values[2]) +
                    "\",\"enabled\":" + enabled.ToString().ToLowerInvariant() + ",\"opacity\":" + opacity + ",\"speed\":36}";
            }

            internal string MovingWatermarkSettingsJson()
            {
                return MovingWatermarkSettingsJson(MovingWatermarkSettingsValues());
            }

            internal void SaveMovingWatermarkSettings(string title, string username, string tagline, bool enabled, int opacityPercent)
            {
                opacityPercent = Math.Max(10, Math.Min(95, opacityPercent));
                string[] values = new string[] {
                    MovingWatermarkText(title, "OBSIDIAN", 32), MovingWatermarkText(username, "STALLION", 40),
                    MovingWatermarkText(tagline, "LIVE • VERIFIED • HD", 60), enabled.ToString(), opacityPercent.ToString()
                };
                File.WriteAllText(StudioPath("moving-watermark-v4.txt"), String.Join("\t", values), new UTF8Encoding(false));
                SetEvolutionModule("dmca", enabled);
            }

            internal List<string> LoadEnabledModules()
            {
                string path = StudioPath("enabled-modules-v3.txt");
                var enabled = new List<string>();
                if (File.Exists(path))
                {
                    foreach (string line in File.ReadAllLines(path))
                    {
                        string id = Clean(line).ToLowerInvariant();
                        if (Regex.IsMatch(id, "^[a-z0-9][a-z0-9-]{1,48}$") && !enabled.Contains(id)) enabled.Add(id);
                    }
                }
                else
                {
                    enabled.AddRange(new string[] { "top-tipper-card", "fan-card", "recent-supporter", "tip-ticker", "vip-badge" });
                }
                return enabled;
            }

            internal void SaveEnabledModules(List<string> enabled)
            {
                File.WriteAllLines(StudioPath("enabled-modules-v3.txt"), enabled.ToArray());
                server.Publish("modules-config", "{\"enabled\":" + JsonArray(enabled.ToArray()) + "}");
            }

            internal string ActionDefinitionsJson()
            {
                string[] lines = ActionSlotLines();
                var actions = new List<string>();
                foreach (string line in lines)
                {
                    string[] values = line.Split('|'); int slot;
                    if (values.Length < 5 || !Int32.TryParse(values[0], out slot) || slot < 1 || slot > 20) continue;
                    actions.Add("{\"slot\":" + slot + ",\"name\":\"" + Json(values[1]) + "\",\"module\":\"" + Json(values[2]) + "\",\"action\":\"" + Json(values[3]) + "\",\"animation\":\"" + Json(values[4]) + "\"}");
                }
                return String.Join(",", actions.ToArray());
            }

            private string ActionSlotsFolder()
            {
                string folder = StudioPath("backupscripts_action_v4");
                Directory.CreateDirectory(folder);
                return folder;
            }

            internal static string ActionSlotId(int slot)
            {
                return "action-slot-" + Math.Max(1, Math.Min(20, slot)).ToString("00");
            }

            private string ActionSlotBackupFolder(int slot)
            {
                return Path.Combine(ActionSlotsFolder(), "slot-" + Math.Max(1, Math.Min(20, slot)).ToString("00"));
            }

            private static string[] EmptyActionSlotLines()
            {
                var lines = new List<string>();
                for (int slot = 1; slot <= 20; slot++) lines.Add(slot + "|Empty||run|fade");
                return lines.ToArray();
            }

            internal string[] ActionSlotLines()
            {
                string path = StudioPath("actions-v4.txt");
                var slots = new Dictionary<int, string>();
                if (File.Exists(path))
                {
                    foreach (string line in File.ReadAllLines(path))
                    {
                        string[] values = line.Split('|'); int slot;
                        if (values.Length < 5 || !Int32.TryParse(values[0], out slot) || slot < 1 || slot > 20) continue;
                        string expectedId = ActionSlotId(slot);
                        string id = Clean(values[2]).ToLowerInvariant();
                        if (id.Length > 0 && !String.Equals(id, expectedId, StringComparison.OrdinalIgnoreCase)) continue;
                        string name = id.Length == 0 ? "Empty" : Clean(values[1]);
                        if (name.Length == 0) name = "Action " + slot;
                        slots[slot] = slot + "|" + CleanActionField(name) + "|" + id + "|run|fade";
                    }
                }
                var normalized = new List<string>();
                for (int slot = 1; slot <= 20; slot++) normalized.Add(slots.ContainsKey(slot) ? slots[slot] : slot + "|Empty||run|fade");
                if (!File.Exists(path) || !String.Equals(String.Join("\n", File.ReadAllLines(path)), String.Join("\n", normalized.ToArray()), StringComparison.Ordinal))
                    File.WriteAllLines(path, normalized.ToArray());
                return normalized.ToArray();
            }

            private static string CleanActionField(string value)
            {
                return (value ?? "").Replace("|", " ").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
            }

            private void SaveActionSlotLine(int slot, string name, string id)
            {
                slot = Math.Max(1, Math.Min(20, slot));
                var lines = new List<string>(ActionSlotLines());
                lines.RemoveAll(delegate(string line) { return line.StartsWith(slot + "|", StringComparison.Ordinal); });
                lines.Add(slot + "|" + CleanActionField(name) + "|" + CleanActionField(id).ToLowerInvariant() + "|run|fade");
                lines.Sort(delegate(string left, string right)
                {
                    int leftSlot = 0, rightSlot = 0;
                    Int32.TryParse(left.Split('|')[0], out leftSlot);
                    Int32.TryParse(right.Split('|')[0], out rightSlot);
                    return leftSlot.CompareTo(rightSlot);
                });
                File.WriteAllLines(StudioPath("actions-v4.txt"), lines.ToArray());
            }

            private static bool IsActionSlotModuleId(string id)
            {
                return Regex.IsMatch(id ?? "", "^action-slot-(0[1-9]|1[0-9]|20)$", RegexOptions.IgnoreCase);
            }

            private void DisableLegacyActionPreloadsForV4()
            {
                try
                {
                    var enabled = LoadEnabledModules();
                    int removed = enabled.RemoveAll(delegate(string id) { return (id ?? "").StartsWith("action-", StringComparison.OrdinalIgnoreCase) && !IsActionSlotModuleId(id); });
                    if (removed > 0) File.WriteAllLines(StudioPath("enabled-modules-v3.txt"), enabled.ToArray());
                }
                catch (Exception error) { Program.LogRuntimeError("Disable legacy Action preloads", error); }
            }

            private static void CommitActionDirectory(string staging, string destination)
            {
                string previous = destination + ".previous-" + Guid.NewGuid().ToString("N");
                if (Directory.Exists(destination)) Directory.Move(destination, previous);
                try
                {
                    Directory.Move(staging, destination);
                    if (Directory.Exists(previous)) Directory.Delete(previous, true);
                }
                catch
                {
                    try { if (Directory.Exists(destination)) Directory.Delete(destination, true); } catch { }
                    try { if (Directory.Exists(previous)) Directory.Move(previous, destination); } catch { }
                    throw;
                }
            }

            private static void CopyActionHtmlPackage(string selectedHtml, string destinationRoot)
            {
                Directory.CreateDirectory(destinationRoot);
                File.Copy(selectedHtml, Path.Combine(destinationRoot, "overlay.html"), true);
                string sourceFolder = Path.GetDirectoryName(selectedHtml);
                foreach (string file in Directory.GetFiles(sourceFolder, "*", SearchOption.TopDirectoryOnly))
                {
                    if (String.Equals(Path.GetFullPath(file), Path.GetFullPath(selectedHtml), StringComparison.OrdinalIgnoreCase)) continue;
                    var info = new FileInfo(file);
                    if (info.Length > 32L * 1024L * 1024L) continue;
                    if (!Regex.IsMatch(info.Extension, "^\\.(json|html|css|js|mjs|png|jpg|jpeg|gif|webp|svg|wav|mp3|ogg|txt|woff|woff2|ttf|otf|mp4|webm)$", RegexOptions.IgnoreCase)) continue;
                    File.Copy(file, Path.Combine(destinationRoot, info.Name), true);
                }

                string html = File.ReadAllText(selectedHtml, Encoding.UTF8);
                var folders = new HashSet<string>(new string[] { "assets", "cardimages", "images", "audio", "sounds", "scripts", "styles", "css", "js", "fonts", "video" }, StringComparer.OrdinalIgnoreCase);
                foreach (Match match in Regex.Matches(html, "(?:src|href)\\s*=\\s*[\\\"'](?<path>[^\\\"']+)[\\\"']|url\\(\\s*[\\\"']?(?<url>[^\\\"')]+)", RegexOptions.IgnoreCase))
                {
                    string reference = match.Groups["path"].Success ? match.Groups["path"].Value : match.Groups["url"].Value;
                    reference = reference.Split('?', '#')[0].Replace('\\', '/').Trim();
                    if (reference.Length == 0 || reference.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || reference.Contains("://") || reference.StartsWith("/")) continue;
                    while (reference.StartsWith("./", StringComparison.Ordinal)) reference = reference.Substring(2);
                    string[] parts = reference.Split('/');
                    if (parts.Length > 1 && parts[0] != ".." && Regex.IsMatch(parts[0], "^[A-Za-z0-9._ -]+$")) folders.Add(parts[0]);
                }
                foreach (string folder in folders)
                {
                    string source = Path.Combine(sourceFolder, folder);
                    if (Directory.Exists(source)) CopyHtmlModuleAssets(source, Path.Combine(destinationRoot, folder));
                }
            }

            internal bool InstallActionSlotHtml(int slot, string displayName)
            {
                slot = Math.Max(1, Math.Min(20, slot));
                using (var dialog = new OpenFileDialog { Filter = "HTML Overlay (*.html;*.htm)|*.html;*.htm", CheckFileExists = true, Title = "Choose HTML overlay for Action slot " + slot })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                    if (new FileInfo(dialog.FileName).Length > 32L * 1024L * 1024L) { MessageBox.Show("The HTML file exceeds the 32 MB safety limit."); return false; }
                    string name = CleanActionField(displayName);
                    if (name.Length == 0) name = Path.GetFileNameWithoutExtension(dialog.FileName);
                    if (name.Length > 80) name = name.Substring(0, 80);
                    string id = ActionSlotId(slot);
                    string moduleDestination = Path.Combine(ModulesFolder(), id);
                    string backupDestination = ActionSlotBackupFolder(slot);
                    string moduleStaging = Path.Combine(ModulesFolder(), ".installing-" + Guid.NewGuid().ToString("N"));
                    string backupStaging = Path.Combine(ActionSlotsFolder(), ".backup-" + Guid.NewGuid().ToString("N"));
                    try
                    {
                        CopyActionHtmlPackage(dialog.FileName, moduleStaging);
                        WriteActionBackupManifest(moduleStaging, id, name);
                        CopyHtmlModuleAssets(moduleStaging, backupStaging);
                        CommitActionDirectory(backupStaging, backupDestination);
                        CommitActionDirectory(moduleStaging, moduleDestination);
                        SaveActionSlotLine(slot, name, id);
                        var enabled = LoadEnabledModules();
                        if (!enabled.Contains(id)) enabled.Add(id);
                        SaveEnabledModules(enabled);
                        server.Publish("modules-reload", "{}");
                        server.Publish("action-config", "{\"actions\":[" + ActionDefinitionsJson() + "]}");
                        MessageBox.Show(name + " is assigned to Action " + slot + ".\r\n\r\nThe HTML and its local assets were copied into the managed slot backup.", "Creator Cam Studio");
                        return true;
                    }
                    catch (Exception error)
                    {
                        try { if (Directory.Exists(moduleStaging)) Directory.Delete(moduleStaging, true); } catch { }
                        try { if (Directory.Exists(backupStaging)) Directory.Delete(backupStaging, true); } catch { }
                        MessageBox.Show("Action " + slot + " could not import that HTML safely.\r\n\r\n" + error.Message, "Creator Cam Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
            }

            internal string ActivateValidatedShowPack(ShowPackValidation pack)
            {
                if (pack == null || !pack.IsValid || String.IsNullOrWhiteSpace(pack.InstalledPath))
                    return "The Show Pack was not validated and installed.";
                string installedRoot = Path.GetFullPath(pack.InstalledPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!Directory.Exists(installedRoot)) return "The installed Show Pack folder is missing.";

                string snapshot = "";
                var stagingFolders = new List<string>();
                try
                {
                    snapshot = CreateActionPackRecoverySnapshot(pack.PackId + "-before-activation");
                    var preparedModules = new Dictionary<int, string>();
                    var preparedBackups = new Dictionary<int, string>();

                    foreach (ShowPackAction action in pack.Actions)
                    {
                        string source = Path.GetFullPath(Path.Combine(installedRoot, "actions", "action-" + action.Slot.ToString("00")));
                        if (!source.StartsWith(installedRoot, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(source) || !File.Exists(Path.Combine(source, "overlay.html")))
                            throw new InvalidDataException("Action " + action.Slot.ToString("00") + " is missing its installed overlay.");

                        string id = ActionSlotId(action.Slot);
                        string moduleStage = Path.Combine(ModulesFolder(), ".show-pack-" + Guid.NewGuid().ToString("N"));
                        string backupStage = Path.Combine(ActionSlotsFolder(), ".show-pack-" + Guid.NewGuid().ToString("N"));
                        CopyHtmlModuleAssets(source, moduleStage);
                        WriteActionBackupManifest(moduleStage, id, action.Name);
                        CopyHtmlModuleAssets(moduleStage, backupStage);
                        preparedModules[action.Slot] = moduleStage;
                        preparedBackups[action.Slot] = backupStage;
                        stagingFolders.Add(moduleStage);
                        stagingFolders.Add(backupStage);
                    }

                    var lines = new List<string>();
                    var enabled = LoadEnabledModules();
                    enabled.RemoveAll(delegate(string id) { return IsActionSlotModuleId(id); });
                    for (int slot = 1; slot <= 20; slot++)
                    {
                        ShowPackAction action = pack.Actions.Find(delegate(ShowPackAction item) { return item.Slot == slot; });
                        if (action == null)
                        {
                            lines.Add(slot + "|Empty||run|fade");
                            continue;
                        }
                        string id = ActionSlotId(slot);
                        CommitActionDirectory(preparedBackups[slot], ActionSlotBackupFolder(slot));
                        CommitActionDirectory(preparedModules[slot], Path.Combine(ModulesFolder(), id));
                        lines.Add(slot + "|" + CleanActionField(action.Name) + "|" + id + "|run|fade");
                        enabled.Add(id);
                    }

                    File.WriteAllLines(StudioPath("actions-v4.txt"), lines.ToArray(), new UTF8Encoding(false));
                    SaveEnabledModules(enabled);
                    server.Publish("modules-reload", "{}");
                    server.Publish("action-config", "{\"actions\":[" + ActionDefinitionsJson() + "]}");
                    return "";
                }
                catch (Exception error)
                {
                    Program.LogRuntimeError("Activate Show Pack", error);
                    if (snapshot.Length > 0)
                    {
                        try { RestoreActionDeckSnapshot(snapshot); }
                        catch (Exception restoreError) { Program.LogRuntimeError("Restore Action Deck after failed Show Pack activation", restoreError); }
                    }
                    return error.Message;
                }
                finally
                {
                    foreach (string folder in stagingFolders)
                        try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch { }
                }
            }

            internal bool TriggerShowPackAction(ShowPackAction action)
            {
                if (action == null || action.Slot < 1 || action.Slot > 20) return false;
                if (!LoadActionSlotHtml(action.Slot)) return false;
                string id = ActionSlotId(action.Slot);
                string definition = "{\"slot\":" + action.Slot + ",\"name\":\"" + Json(action.Name) + "\",\"module\":\"" + Json(id) + "\",\"action\":\"run\",\"animation\":\"fade\"}";
                var launchTimer = new System.Windows.Forms.Timer { Interval = 250 };
                launchTimer.Tick += delegate
                {
                    launchTimer.Stop();
                    launchTimer.Dispose();
                    server.Publish("action-trigger", "{\"slot\":" + action.Slot + ",\"definition\":" + definition + "}");
                    server.Publish("show-action-triggered", "{\"slot\":" + action.Slot + ",\"id\":\"" + Json(action.Id) + "\",\"name\":\"" + Json(action.Name) + "\"}");

                    var stopTimer = new System.Windows.Forms.Timer { Interval = Math.Max(1, Math.Min(3600, action.DurationSeconds)) * 1000 };
                    stopTimer.Tick += delegate
                    {
                        stopTimer.Stop();
                        stopTimer.Dispose();
                        server.Publish("module-action", "{\"id\":\"" + Json(id) + "\",\"action\":\"stop\",\"name\":\"" + Json(action.Name) + "\"}");
                    };
                    stopTimer.Start();
                };
                launchTimer.Start();
                return true;
            }


            private string ActionPackHistoryFolder()
            {
                string folder = StudioPath("backupscripts_action_v4_pack_history");
                Directory.CreateDirectory(folder);
                return folder;
            }

            private string CreateActionPackRecoverySnapshot(string label)
            {
                string safeLabel = Regex.Replace(CleanActionField(label), "[^A-Za-z0-9._ -]+", "-").Trim();
                if (safeLabel.Length == 0) safeLabel = "action-pack";
                if (safeLabel.Length > 48) safeLabel = safeLabel.Substring(0, 48);
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string snapshot = Path.Combine(ActionPackHistoryFolder(), stamp + "-" + safeLabel);
                Directory.CreateDirectory(snapshot);

                string actionConfig = StudioPath("actions-v4.txt");
                if (File.Exists(actionConfig)) File.Copy(actionConfig, Path.Combine(snapshot, "actions-v4.txt"), true);

                for (int slot = 1; slot <= 20; slot++)
                {
                    string source = ActionSlotBackupFolder(slot);
                    if (!Directory.Exists(source)) continue;
                    string destination = Path.Combine(snapshot, "slot-" + slot.ToString("00"));
                    CopyHtmlModuleAssets(source, destination);
                }

                File.WriteAllText(
                    Path.Combine(snapshot, "RECOVERY.txt"),
                    "StimTake Action Deck recovery snapshot\r\nCreated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                    "\r\nReason: " + safeLabel +
                    "\r\nRestore from the Action Deck using RESTORE PREVIOUS 20-PACK.\r\n",
                    new UTF8Encoding(false));
                return snapshot;
            }

            private static bool IsSafeActionPackAssetExtension(string extension)
            {
                return Regex.IsMatch(extension ?? "", "^\\.(json|html|htm|css|js|mjs|png|jpg|jpeg|gif|webp|svg|wav|mp3|ogg|txt|woff|woff2|ttf|otf|mp4|webm)$", RegexOptions.IgnoreCase);
            }

            private static string ActionPackFolderDisplayName(string prefix, string manifest)
            {
                string name = ManifestValue(manifest, "name");
                if (name.Length > 0) return name.Length > 80 ? name.Substring(0, 80) : name;
                string trimmed = (prefix ?? "").TrimEnd('/');
                int slash = trimmed.LastIndexOf('/');
                string folder = slash >= 0 ? trimmed.Substring(slash + 1) : trimmed;
                folder = Regex.Replace(folder, "^(action|slot)-(0[1-9]|1[0-9]|20)-?", "", RegexOptions.IgnoreCase).Replace('-', ' ').Trim();
                if (folder.Length == 0) folder = "Action";
                return folder.Length > 80 ? folder.Substring(0, 80) : folder;
            }

            private void RestoreActionDeckSnapshot(string snapshot)
            {
                if (String.IsNullOrWhiteSpace(snapshot) || !Directory.Exists(snapshot)) throw new DirectoryNotFoundException("The Action Pack recovery snapshot is missing.");

                string restoreStage = Path.Combine(ActionSlotsFolder(), ".restore-pack-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(restoreStage);
                try
                {
                    var restoredLines = new List<string>();
                    string savedConfig = Path.Combine(snapshot, "actions-v4.txt");
                    var names = new Dictionary<int, string>();
                    if (File.Exists(savedConfig))
                    {
                        foreach (string line in File.ReadAllLines(savedConfig))
                        {
                            string[] parts = line.Split('|'); int slot;
                            if (parts.Length >= 3 && Int32.TryParse(parts[0], out slot) && slot >= 1 && slot <= 20)
                                names[slot] = parts[1];
                        }
                    }

                    for (int slot = 1; slot <= 20; slot++)
                    {
                        string source = Path.Combine(snapshot, "slot-" + slot.ToString("00"));
                        string id = ActionSlotId(slot);
                        if (!Directory.Exists(source) || !File.Exists(Path.Combine(source, "overlay.html")))
                        {
                            restoredLines.Add(slot + "|Empty||run|fade");
                            continue;
                        }

                        string slotStage = Path.Combine(restoreStage, "slot-" + slot.ToString("00"));
                        CopyHtmlModuleAssets(source, slotStage);
                        string displayName = names.ContainsKey(slot) ? CleanActionField(names[slot]) : "Action " + slot;
                        if (displayName.Length == 0) displayName = "Action " + slot;
                        WriteActionBackupManifest(slotStage, id, displayName);
                        restoredLines.Add(slot + "|" + displayName + "|" + id + "|run|fade");
                    }

                    for (int slot = 1; slot <= 20; slot++)
                    {
                        string id = ActionSlotId(slot);
                        string staged = Path.Combine(restoreStage, "slot-" + slot.ToString("00"));
                        string backupDestination = ActionSlotBackupFolder(slot);
                        string moduleDestination = Path.Combine(ModulesFolder(), id);

                        if (Directory.Exists(staged))
                        {
                            string backupStage = Path.Combine(ActionSlotsFolder(), ".restore-backup-" + Guid.NewGuid().ToString("N"));
                            string moduleStage = Path.Combine(ModulesFolder(), ".restore-module-" + Guid.NewGuid().ToString("N"));
                            CopyHtmlModuleAssets(staged, backupStage);
                            CopyHtmlModuleAssets(staged, moduleStage);
                            CommitActionDirectory(backupStage, backupDestination);
                            CommitActionDirectory(moduleStage, moduleDestination);
                        }
                        else
                        {
                            if (Directory.Exists(backupDestination)) Directory.Delete(backupDestination, true);
                            if (Directory.Exists(moduleDestination)) Directory.Delete(moduleDestination, true);
                        }
                    }

                    File.WriteAllLines(StudioPath("actions-v4.txt"), restoredLines.ToArray());
                    var enabled = LoadEnabledModules();
                    enabled.RemoveAll(delegate(string id) { return IsActionSlotModuleId(id); });
                    foreach (string line in restoredLines)
                    {
                        string[] parts = line.Split('|');
                        if (parts.Length >= 3 && parts[2].Length > 0 && !enabled.Contains(parts[2])) enabled.Add(parts[2]);
                    }
                    SaveEnabledModules(enabled);
                    server.Publish("modules-reload", "{}");
                    server.Publish("action-config", "{\"actions\":[" + ActionDefinitionsJson() + "]}");
                }
                finally
                {
                    try { if (Directory.Exists(restoreStage)) Directory.Delete(restoreStage, true); } catch { }
                }
            }

            internal bool InstallActionPackZip()
            {
                using (var dialog = new OpenFileDialog
                {
                    Filter = "StimTake 20-Action Pack (*.zip)|*.zip",
                    CheckFileExists = true,
                    Title = "Choose a StimTake 20-Action Pack"
                })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                    if (new FileInfo(dialog.FileName).Length > 300L * 1024L * 1024L)
                    {
                        MessageBox.Show("Action Pack is larger than the 300 MB safety limit.");
                        return false;
                    }

                    string stagingRoot = Path.Combine(ActionSlotsFolder(), ".pack-import-" + Guid.NewGuid().ToString("N"));
                    string recoverySnapshot = "";
                    try
                    {
                        var prefixes = new Dictionary<int, string>();
                        var manifests = new Dictionary<int, string>();
                        long expandedBytes = 0;

                        using (var archive = ZipFile.OpenRead(dialog.FileName))
                        {
                            if (archive.Entries.Count > 1200) throw new InvalidDataException("Action Pack contains too many files.");

                            foreach (ZipArchiveEntry entry in archive.Entries)
                            {
                                string normalized = entry.FullName.Replace('\\', '/').TrimStart('/');
                                Match match = Regex.Match(normalized, "(?:^|/)(?<folder>(?:action|slot)-(?<slot>0[1-9]|1[0-9]|20)(?:-[^/]+)?)/overlay\\.html$", RegexOptions.IgnoreCase);
                                if (!match.Success) continue;
                                int slot = Int32.Parse(match.Groups["slot"].Value);
                                string prefix = normalized.Substring(0, normalized.Length - "overlay.html".Length);
                                if (prefixes.ContainsKey(slot) && !String.Equals(prefixes[slot], prefix, StringComparison.OrdinalIgnoreCase))
                                    throw new InvalidDataException("Action Pack contains more than one overlay for slot " + slot + ".");
                                prefixes[slot] = prefix;
                            }

                            var missing = new List<string>();
                            for (int slot = 1; slot <= 20; slot++) if (!prefixes.ContainsKey(slot)) missing.Add(slot.ToString());
                            if (missing.Count > 0) throw new InvalidDataException("This is not a complete 20-Action Pack. Missing slot(s): " + String.Join(", ", missing.ToArray()));

                            Directory.CreateDirectory(stagingRoot);
                            for (int slot = 1; slot <= 20; slot++)
                            {
                                string prefix = prefixes[slot];
                                string slotStage = Path.Combine(stagingRoot, "slot-" + slot.ToString("00"));
                                string safeStage = Path.GetFullPath(slotStage) + Path.DirectorySeparatorChar;
                                Directory.CreateDirectory(slotStage);

                                foreach (ZipArchiveEntry entry in archive.Entries)
                                {
                                    if (String.IsNullOrEmpty(entry.Name)) continue;
                                    string normalized = entry.FullName.Replace('\\', '/').TrimStart('/');
                                    if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                                    string relative = normalized.Substring(prefix.Length).TrimStart('/');
                                    if (relative.Length == 0) continue;
                                    if (relative.Contains("../") || relative.StartsWith("..", StringComparison.Ordinal)) throw new InvalidDataException("Unsafe Action Pack path was blocked.");
                                    if (entry.Length > 32L * 1024L * 1024L) throw new InvalidDataException("An Action Pack file exceeds the 32 MB safety limit.");
                                    expandedBytes += entry.Length;
                                    if (expandedBytes > 500L * 1024L * 1024L) throw new InvalidDataException("Expanded Action Pack exceeds the 500 MB safety limit.");
                                    string extension = Path.GetExtension(relative);
                                    if (!IsSafeActionPackAssetExtension(extension)) continue;
                                    string destination = Path.GetFullPath(Path.Combine(slotStage, relative.Replace('/', Path.DirectorySeparatorChar)));
                                    if (!destination.StartsWith(safeStage, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsafe Action Pack destination was blocked.");
                                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                                    entry.ExtractToFile(destination, true);
                                }

                                string overlay = Path.Combine(slotStage, "overlay.html");
                                if (!File.Exists(overlay)) throw new InvalidDataException("Slot " + slot + " did not stage overlay.html correctly.");

                                string manifestPath = Path.Combine(slotStage, "module.json");
                                string manifest = File.Exists(manifestPath) ? File.ReadAllText(manifestPath, Encoding.UTF8) : "";
                                string displayName = ActionPackFolderDisplayName(prefix, manifest);
                                manifests[slot] = displayName;
                                WriteActionBackupManifest(slotStage, ActionSlotId(slot), displayName);
                            }
                        }

                        string preview = "";
                        for (int slot = 1; slot <= 20; slot++) preview += slot.ToString("00") + "  " + manifests[slot] + "\r\n";
                        DialogResult approval = MessageBox.Show(
                            "20-Action Pack validated successfully.\r\n\r\n" + preview +
                            "\r\nThis will replace ALL 20 current Action slots.\r\nA recovery snapshot will be created first.\r\n\r\nContinue?",
                            "Import 20-Action Pack",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);
                        if (approval != DialogResult.Yes) return false;

                        recoverySnapshot = CreateActionPackRecoverySnapshot(Path.GetFileNameWithoutExtension(dialog.FileName));

                        var newLines = new List<string>();
                        for (int slot = 1; slot <= 20; slot++)
                        {
                            string id = ActionSlotId(slot);
                            string displayName = manifests[slot];
                            string staged = Path.Combine(stagingRoot, "slot-" + slot.ToString("00"));

                            string backupStage = Path.Combine(ActionSlotsFolder(), ".pack-backup-" + Guid.NewGuid().ToString("N"));
                            string moduleStage = Path.Combine(ModulesFolder(), ".pack-module-" + Guid.NewGuid().ToString("N"));
                            CopyHtmlModuleAssets(staged, backupStage);
                            CopyHtmlModuleAssets(staged, moduleStage);
                            CommitActionDirectory(backupStage, ActionSlotBackupFolder(slot));
                            CommitActionDirectory(moduleStage, Path.Combine(ModulesFolder(), id));
                            newLines.Add(slot + "|" + CleanActionField(displayName) + "|" + id + "|run|fade");
                        }

                        File.WriteAllLines(StudioPath("actions-v4.txt"), newLines.ToArray());
                        var enabled = LoadEnabledModules();
                        enabled.RemoveAll(delegate(string id) { return IsActionSlotModuleId(id); });
                        for (int slot = 1; slot <= 20; slot++) enabled.Add(ActionSlotId(slot));
                        SaveEnabledModules(enabled);
                        server.Publish("modules-reload", "{}");
                        server.Publish("action-config", "{\"actions\":[" + ActionDefinitionsJson() + "]}");
                        MessageBox.Show(
                            "20-Action Pack installed.\r\n\r\nThe previous Action Deck was preserved at:\r\n" + recoverySnapshot,
                            "Creator Cam Studio");
                        return true;
                    }
                    catch (Exception error)
                    {
                        if (recoverySnapshot.Length > 0)
                        {
                            try { RestoreActionDeckSnapshot(recoverySnapshot); }
                            catch (Exception restoreError)
                            {
                                MessageBox.Show(
                                    "Action Pack import failed and automatic rollback also reported an error.\r\n\r\nImport error:\r\n" + error.Message +
                                    "\r\n\r\nRollback error:\r\n" + restoreError.Message +
                                    "\r\n\r\nRecovery snapshot:\r\n" + recoverySnapshot,
                                    "Creator Cam Studio",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                                return false;
                            }
                        }
                        MessageBox.Show("Action Pack was not installed.\r\n\r\n" + error.Message, "Creator Cam Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                    finally
                    {
                        try { if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true); } catch { }
                    }
                }
            }

            internal bool RestorePreviousActionPack()
            {
                string history = ActionPackHistoryFolder();
                string[] snapshots = Directory.GetDirectories(history);
                if (snapshots.Length == 0)
                {
                    MessageBox.Show("No previous 20-Action Pack recovery snapshot exists yet.");
                    return false;
                }
                Array.Sort(snapshots, StringComparer.OrdinalIgnoreCase);
                string snapshot = snapshots[snapshots.Length - 1];
                if (MessageBox.Show(
                    "Restore the most recent Action Deck snapshot?\r\n\r\n" + Path.GetFileName(snapshot) +
                    "\r\n\r\nThe current Action Deck will be preserved first.",
                    "Restore Previous 20-Pack",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes) return false;

                string currentSnapshot = CreateActionPackRecoverySnapshot("before-restore");
                try
                {
                    RestoreActionDeckSnapshot(snapshot);
                    MessageBox.Show("Previous Action Deck restored successfully.", "Creator Cam Studio");
                    return true;
                }
                catch (Exception error)
                {
                    try { RestoreActionDeckSnapshot(currentSnapshot); } catch { }
                    MessageBox.Show("Previous Action Deck could not be restored.\r\n\r\n" + error.Message, "Creator Cam Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }

            internal bool LoadActionSlotHtml(int slot)
            {
                slot = Math.Max(1, Math.Min(20, slot));
                string id = ActionSlotId(slot);
                string backup = ActionSlotBackupFolder(slot);
                if (!Directory.Exists(backup) || !File.Exists(Path.Combine(backup, "overlay.html")) || !File.Exists(Path.Combine(backup, "module.json"))) return false;
                string destination = Path.Combine(ModulesFolder(), id);
                string staging = destination + ".loading-" + Guid.NewGuid().ToString("N");
                try
                {
                    CopyHtmlModuleAssets(backup, staging);
                    CommitActionDirectory(staging, destination);
                    var enabled = LoadEnabledModules();
                    if (!enabled.Contains(id)) enabled.Add(id);
                    SaveEnabledModules(enabled);
                    server.Publish("modules-reload", "{}");
                    return true;
                }
                catch (Exception error)
                {
                    try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
                    Program.LogRuntimeError("Load Action slot " + slot, error);
                    return false;
                }
            }

            internal bool DeleteActionSlotHtml(int slot)
            {
                slot = Math.Max(1, Math.Min(20, slot));
                string id = ActionSlotId(slot);
                string backup = Path.GetFullPath(ActionSlotBackupFolder(slot));
                string backupRoot = Path.GetFullPath(ActionSlotsFolder()) + Path.DirectorySeparatorChar;
                string module = Path.GetFullPath(Path.Combine(ModulesFolder(), id));
                string moduleRoot = Path.GetFullPath(ModulesFolder()) + Path.DirectorySeparatorChar;
                if (backup.StartsWith(backupRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(backup)) Directory.Delete(backup, true);
                if (module.StartsWith(moduleRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(module)) Directory.Delete(module, true);
                var enabled = LoadEnabledModules();
                enabled.RemoveAll(delegate(string item) { return String.Equals(item, id, StringComparison.OrdinalIgnoreCase); });
                SaveEnabledModules(enabled);
                SaveActionSlotLine(slot, "Empty", "");
                server.Publish("modules-reload", "{}");
                server.Publish("action-config", "{\"actions\":[" + ActionDefinitionsJson() + "]}");
                return true;
            }

            internal string ModulesFolder()
            {
                string folder = StudioPath("Modules");
                Directory.CreateDirectory(folder);
                return folder;
            }

            internal string WheelScriptsFolder()
            {
                string folder = StudioPath("backupscripts_sw");
                Directory.CreateDirectory(folder);
                return folder;
            }

            private string DeletedWheelScriptsPath()
            {
                return StudioPath("deleted-wheel-scripts-v3.txt");
            }

            private void EnsureBundledWheelBackups()
            {
                try
                {
                    var deleted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (File.Exists(DeletedWheelScriptsPath())) foreach (string line in File.ReadAllLines(DeletedWheelScriptsPath())) if (Clean(line).Length > 0) deleted.Add(Clean(line));
                    string[][] bundled = new string[][] {
                        new string[] { "wheel-new-year-2025", "New Year 2025 Wheel", "/Modules/NewYearWheel2025/overlay.html" },
                        new string[] { "wheel-christmas", "Christmas Wheel", "/Modules/ChristmasWheel/overlay.html" },
                        new string[] { "wheel-halloween", "Halloween Wheel", "/Modules/HalloweenWheel/overlay.html" }
                    };
                    foreach (string[] wheel in bundled)
                    {
                        if (deleted.Contains(wheel[0])) continue;
                        string folder = Path.Combine(WheelScriptsFolder(), wheel[0]);
                        string overlay = Path.Combine(folder, "overlay.html");
                        Directory.CreateDirectory(folder);
                        if (!File.Exists(overlay)) server.ExportBundledAsset(wheel[2], overlay);
                        WriteWheelBackupManifest(folder, wheel[0], wheel[1]);
                    }
                }
                catch (Exception error) { Program.LogRuntimeError("Seed wheel script backups", error); }
            }

            private static void WriteWheelBackupManifest(string folder, string id, string name)
            {
                string manifest = "{\r\n" +
                    "  \"id\": \"" + Json(id) + "\",\r\n" +
                    "  \"name\": \"" + Json(name) + "\",\r\n" +
                    "  \"version\": \"1.0.0\",\r\n" +
                    "  \"type\": \"GAME\",\r\n" +
                    "  \"gameKind\": \"WHEEL\",\r\n" +
                    "  \"canvas\": { \"width\": 1920, \"height\": 1080 },\r\n" +
                    "  \"overlay\": \"overlay.html\",\r\n" +
                    "  \"defaultEnabled\": false,\r\n" +
                    "  \"restartOnTrigger\": true,\r\n" +
                    "  \"duration\": 30,\r\n" +
                    "  \"actions\": [\"run\"]\r\n" +
                    "}\r\n";
                File.WriteAllText(Path.Combine(folder, "module.json"), manifest, new UTF8Encoding(false));
            }

            private void BackupWheelModule(string id)
            {
                string source = Path.Combine(ModulesFolder(), id);
                if (!Directory.Exists(source)) return;
                string destination = Path.Combine(WheelScriptsFolder(), id);
                string staging = destination + ".backup-" + Guid.NewGuid().ToString("N");
                try
                {
                    CopyHtmlModuleAssets(source, staging);
                    if (Directory.Exists(destination)) Directory.Delete(destination, true);
                    Directory.Move(staging, destination);
                    var deleted = File.Exists(DeletedWheelScriptsPath()) ? new List<string>(File.ReadAllLines(DeletedWheelScriptsPath())) : new List<string>();
                    deleted.RemoveAll(delegate(string item) { return String.Equals(Clean(item), id, StringComparison.OrdinalIgnoreCase); });
                    File.WriteAllLines(DeletedWheelScriptsPath(), deleted.ToArray());
                }
                finally
                {
                    try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
                }
            }

            internal List<string[]> WheelScriptCatalog()
            {
                EnsureBundledWheelBackups();
                var rows = new List<string[]>();
                foreach (string directory in Directory.GetDirectories(WheelScriptsFolder()))
                {
                    try
                    {
                        string manifestPath = Path.Combine(directory, "module.json");
                        string overlayPath = Path.Combine(directory, "overlay.html");
                        if (!File.Exists(manifestPath) || !File.Exists(overlayPath)) continue;
                        string manifest = File.ReadAllText(manifestPath, Encoding.UTF8);
                        string id = ManifestValue(manifest, "id").ToLowerInvariant();
                        string name = ManifestValue(manifest, "name");
                        if (!Regex.IsMatch(id, "^[a-z0-9][a-z0-9-]{1,48}$")) continue;
                        rows.Add(new string[] { id, name.Length == 0 ? id : name });
                    }
                    catch { }
                }
                rows.Sort(delegate(string[] a, string[] b) { return String.Compare(a[1], b[1], StringComparison.OrdinalIgnoreCase); });
                return rows;
            }

            internal bool LoadWheelScript(string id)
            {
                id = Clean(id).ToLowerInvariant();
                if (!Regex.IsMatch(id, "^[a-z0-9][a-z0-9-]{1,48}$")) return false;
                string backup = Path.Combine(WheelScriptsFolder(), id);
                if (!Directory.Exists(backup) || !File.Exists(Path.Combine(backup, "overlay.html"))) return false;
                bool bundled = id == "wheel-new-year-2025" || id == "wheel-christmas" || id == "wheel-halloween";
                if (!bundled)
                {
                    string destination = Path.Combine(ModulesFolder(), id);
                    string staging = destination + ".loading-" + Guid.NewGuid().ToString("N");
                    try
                    {
                        CopyHtmlModuleAssets(backup, staging);
                        if (Directory.Exists(destination)) Directory.Delete(destination, true);
                        Directory.Move(staging, destination);
                    }
                    finally
                    {
                        try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
                    }
                }
                var enabled = LoadEnabledModules();
                if (!enabled.Contains(id)) enabled.Add(id);
                SaveEnabledModules(enabled);
                server.Publish("modules-reload", "{}");
                return true;
            }

            internal bool DeleteWheelScript(string id, IWin32Window ownerWindow)
            {
                id = Clean(id).ToLowerInvariant();
                if (!Regex.IsMatch(id, "^[a-z0-9][a-z0-9-]{1,48}$")) return false;
                if (MessageBox.Show(ownerWindow, "Delete this wheel script and its local backup?\r\n\r\nYou can add it again later from the original HTML file.", "Creator Cam Studio", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return false;
                string backup = Path.GetFullPath(Path.Combine(WheelScriptsFolder(), id));
                string backupRoot = Path.GetFullPath(WheelScriptsFolder()) + Path.DirectorySeparatorChar;
                if (backup.StartsWith(backupRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(backup)) Directory.Delete(backup, true);
                string external = Path.GetFullPath(Path.Combine(ModulesFolder(), id));
                string modulesRoot = Path.GetFullPath(ModulesFolder()) + Path.DirectorySeparatorChar;
                if (external.StartsWith(modulesRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(external)) Directory.Delete(external, true);
                var enabled = LoadEnabledModules();
                enabled.RemoveAll(delegate(string item) { return String.Equals(item, id, StringComparison.OrdinalIgnoreCase); });
                SaveEnabledModules(enabled);
                var deleted = File.Exists(DeletedWheelScriptsPath()) ? new List<string>(File.ReadAllLines(DeletedWheelScriptsPath())) : new List<string>();
                if (!deleted.Exists(delegate(string item) { return String.Equals(Clean(item), id, StringComparison.OrdinalIgnoreCase); })) deleted.Add(id);
                File.WriteAllLines(DeletedWheelScriptsPath(), deleted.ToArray());
                server.Publish("modules-reload", "{}");
                return true;
            }

            internal string ActionScriptsFolder()
            {
                string folder = StudioPath("backupscripts_action");
                Directory.CreateDirectory(folder);
                return folder;
            }

            private string DeletedActionScriptsPath()
            {
                return StudioPath("deleted-action-scripts-v3.txt");
            }

            private static bool IsBundledActionId(string id)
            {
                return Regex.IsMatch(id ?? "", "^action-[1-6]-(black-cat|halloween-sides|flying-witch|warlock|pumpkin|ghost)$", RegexOptions.IgnoreCase);
            }

            private void EnsureBundledActionBackups()
            {
                try
                {
                    var deleted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (File.Exists(DeletedActionScriptsPath())) foreach (string line in File.ReadAllLines(DeletedActionScriptsPath())) if (Clean(line).Length > 0) deleted.Add(Clean(line));
                    string[][] bundled = new string[][] {
                        new string[] { "action-1-black-cat", "Black Cat", "/Action/action1_blackcatoverlay.html", "12" },
                        new string[] { "action-2-halloween-sides", "Halloween Sides", "/Action/action2_bottomoverlay.html", "20" },
                        new string[] { "action-3-flying-witch", "Flying Witch", "/Action/action3_witchoverlay.html", "30" },
                        new string[] { "action-4-warlock", "Warlock", "/Action/action4_warlockoverlay.html", "25" },
                        new string[] { "action-5-pumpkin", "Pumpkin", "/Action/action5_pumpkinoverlay.html", "10" },
                        new string[] { "action-6-ghost", "Ghost", "/Action/action6_ghostoverlay.html", "20" }
                    };
                    string[] images = new string[] { "cat.png", "ghost.png", "pumpkin.png", "warlock.png", "witch.png" };
                    foreach (string[] action in bundled)
                    {
                        if (deleted.Contains(action[0])) continue;
                        string folder = Path.Combine(ActionScriptsFolder(), action[0]);
                        Directory.CreateDirectory(folder);
                        string overlay = Path.Combine(folder, "overlay.html");
                        if (!File.Exists(overlay)) server.ExportBundledAsset(action[2], overlay);
                        string imageFolder = Path.Combine(folder, "cardimages");
                        Directory.CreateDirectory(imageFolder);
                        foreach (string image in images)
                        {
                            string destination = Path.Combine(imageFolder, image);
                            if (!File.Exists(destination)) server.ExportBundledAsset("/Action/cardimages/" + image, destination);
                        }
                        WriteActionBackupManifest(folder, action[0], action[1]);
                    }
                }
                catch (Exception error) { Program.LogRuntimeError("Seed action script backups", error); }
            }

            private static void WriteActionBackupManifest(string folder, string id, string name)
            {
                string manifest = "{\r\n" +
                    "  \"id\": \"" + Json(id) + "\",\r\n" +
                    "  \"name\": \"" + Json(name) + "\",\r\n" +
                    "  \"version\": \"1.0.0\",\r\n" +
                    "  \"type\": \"ALERT\",\r\n" +
                    "  \"canvas\": { \"width\": 1920, \"height\": 1080 },\r\n" +
                    "  \"overlay\": \"overlay.html\",\r\n" +
                    "  \"defaultEnabled\": false,\r\n" +
                    "  \"restartOnTrigger\": true,\r\n" +
                    "  \"actions\": [\"run\"]\r\n" +
                    "}\r\n";
                File.WriteAllText(Path.Combine(folder, "module.json"), manifest, new UTF8Encoding(false));
            }

            private void BackupActionModule(string id)
            {
                string source = Path.Combine(ModulesFolder(), id);
                if (!Directory.Exists(source)) return;
                string destination = Path.Combine(ActionScriptsFolder(), id);
                string staging = destination + ".backup-" + Guid.NewGuid().ToString("N");
                try
                {
                    CopyHtmlModuleAssets(source, staging);
                    if (Directory.Exists(destination)) Directory.Delete(destination, true);
                    Directory.Move(staging, destination);
                    var deleted = File.Exists(DeletedActionScriptsPath()) ? new List<string>(File.ReadAllLines(DeletedActionScriptsPath())) : new List<string>();
                    deleted.RemoveAll(delegate(string item) { return String.Equals(Clean(item), id, StringComparison.OrdinalIgnoreCase); });
                    File.WriteAllLines(DeletedActionScriptsPath(), deleted.ToArray());
                }
                finally
                {
                    try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
                }
            }

            internal List<string[]> ActionScriptCatalog()
            {
                EnsureBundledActionBackups();
                var rows = new List<string[]>();
                foreach (string directory in Directory.GetDirectories(ActionScriptsFolder()))
                {
                    try
                    {
                        string manifestPath = Path.Combine(directory, "module.json");
                        string overlayPath = Path.Combine(directory, "overlay.html");
                        if (!File.Exists(manifestPath) || !File.Exists(overlayPath)) continue;
                        string manifest = File.ReadAllText(manifestPath, Encoding.UTF8);
                        string id = ManifestValue(manifest, "id").ToLowerInvariant();
                        string name = ManifestValue(manifest, "name");
                        if (!Regex.IsMatch(id, "^action-[a-z0-9][a-z0-9-]{1,41}$")) continue;
                        rows.Add(new string[] { id, name.Length == 0 ? id : name });
                    }
                    catch { }
                }
                rows.Sort(delegate(string[] a, string[] b) { return String.Compare(a[1], b[1], StringComparison.OrdinalIgnoreCase); });
                return rows;
            }

            internal bool IsActionScript(string id)
            {
                foreach (string[] row in ActionScriptCatalog()) if (String.Equals(row[0], id, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }

            internal string InstallHtmlActionModule(string displayName)
            {
                using (var dialog = new OpenFileDialog { Filter = "HTML Overlay (*.html;*.htm)|*.html;*.htm", CheckFileExists = true, Title = "Choose a full 1920 x 1080 action overlay" })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return "";
                    var sourceInfo = new FileInfo(dialog.FileName);
                    if (sourceInfo.Length > 32L * 1024L * 1024L) { MessageBox.Show("The HTML file exceeds the 32 MB safety limit."); return ""; }
                    string name = Clean(displayName);
                    if (name.Length == 0) name = Path.GetFileNameWithoutExtension(dialog.FileName);
                    name = Regex.Replace(name, "[\\r\\n\\t]+", " ").Trim();
                    if (name.Length > 80) name = name.Substring(0, 80);
                    string slug = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
                    if (slug.Length < 2) slug = "custom-overlay";
                    if (slug.Length > 38) slug = slug.Substring(0, 38).Trim('-');
                    string id = "action-" + slug;
                    string backup = Path.Combine(ActionScriptsFolder(), id);
                    if (!Directory.Exists(backup) && Directory.GetDirectories(ActionScriptsFolder()).Length >= 20)
                    {
                        MessageBox.Show("The Action script library is full. Delete a script before adding another one.\r\n\r\nMaximum: 20 action scripts.");
                        return "";
                    }
                    string destinationRoot = Path.Combine(ModulesFolder(), id);
                    if (Directory.Exists(destinationRoot) &&
                        MessageBox.Show("Replace the installed " + name + " action overlay?", "Creator Cam Studio", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return "";
                    string stagingRoot = Path.Combine(ModulesFolder(), ".installing-" + Guid.NewGuid().ToString("N"));
                    try
                    {
                        Directory.CreateDirectory(stagingRoot);
                        File.Copy(dialog.FileName, Path.Combine(stagingRoot, "overlay.html"), true);
                        string sourceFolder = Path.GetDirectoryName(dialog.FileName);
                        foreach (string assetFolder in new string[] { "assets", "cardimages", "images", "audio", "sounds" })
                        {
                            string candidate = Path.Combine(sourceFolder, assetFolder);
                            if (Directory.Exists(candidate)) CopyHtmlModuleAssets(candidate, Path.Combine(stagingRoot, assetFolder));
                        }
                        WriteActionBackupManifest(stagingRoot, id, name);
                        if (Directory.Exists(destinationRoot)) Directory.Delete(destinationRoot, true);
                        Directory.Move(stagingRoot, destinationRoot);
                        BackupActionModule(id);
                        var enabled = LoadEnabledModules();
                        if (!enabled.Contains(id)) enabled.Add(id);
                        SaveEnabledModules(enabled);
                        server.Publish("modules-reload", "{}");
                        MessageBox.Show(name + " was added as a manual full-overlay Action script.\r\n\r\nCanvas maximum: 1920 x 1080", "Creator Cam Studio");
                        return id;
                    }
                    catch (Exception error)
                    {
                        try { if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true); } catch { }
                        MessageBox.Show("The Action HTML could not be installed safely.\r\n\r\n" + error.Message, "Creator Cam Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return "";
                    }
                }
            }

            internal bool LoadActionScript(string id)
            {
                id = Clean(id).ToLowerInvariant();
                if (!Regex.IsMatch(id, "^action-[a-z0-9][a-z0-9-]{1,41}$")) return false;
                string backup = Path.Combine(ActionScriptsFolder(), id);
                if (!Directory.Exists(backup) || !File.Exists(Path.Combine(backup, "overlay.html"))) return false;
                if (!IsBundledActionId(id))
                {
                    string destination = Path.Combine(ModulesFolder(), id);
                    string staging = destination + ".loading-" + Guid.NewGuid().ToString("N");
                    try
                    {
                        CopyHtmlModuleAssets(backup, staging);
                        if (Directory.Exists(destination)) Directory.Delete(destination, true);
                        Directory.Move(staging, destination);
                    }
                    finally
                    {
                        try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
                    }
                }
                var enabled = LoadEnabledModules();
                if (!enabled.Contains(id)) enabled.Add(id);
                SaveEnabledModules(enabled);
                server.Publish("modules-reload", "{}");
                return true;
            }

            internal bool DeleteActionScript(string id, IWin32Window ownerWindow)
            {
                id = Clean(id).ToLowerInvariant();
                if (!Regex.IsMatch(id, "^action-[a-z0-9][a-z0-9-]{1,41}$")) return false;
                if (MessageBox.Show(ownerWindow, "Delete this Action HTML script and its local backup?\r\n\r\nAssigned slots will remain saved but cannot run until another script is assigned.", "Creator Cam Studio", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return false;
                string backup = Path.GetFullPath(Path.Combine(ActionScriptsFolder(), id));
                string backupRoot = Path.GetFullPath(ActionScriptsFolder()) + Path.DirectorySeparatorChar;
                if (backup.StartsWith(backupRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(backup)) Directory.Delete(backup, true);
                string external = Path.GetFullPath(Path.Combine(ModulesFolder(), id));
                string modulesRoot = Path.GetFullPath(ModulesFolder()) + Path.DirectorySeparatorChar;
                if (external.StartsWith(modulesRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(external)) Directory.Delete(external, true);
                var enabled = LoadEnabledModules();
                enabled.RemoveAll(delegate(string item) { return String.Equals(item, id, StringComparison.OrdinalIgnoreCase); });
                SaveEnabledModules(enabled);
                var deleted = File.Exists(DeletedActionScriptsPath()) ? new List<string>(File.ReadAllLines(DeletedActionScriptsPath())) : new List<string>();
                if (!deleted.Exists(delegate(string item) { return String.Equals(Clean(item), id, StringComparison.OrdinalIgnoreCase); })) deleted.Add(id);
                File.WriteAllLines(DeletedActionScriptsPath(), deleted.ToArray());
                server.Publish("modules-reload", "{}");
                return true;
            }

            internal string TickerHtmlScriptsFolder()
            {
                string folder = StudioPath("backupscripts_ticker");
                Directory.CreateDirectory(folder);
                return folder;
            }

            internal string DmcaHtmlScriptsFolder()
            {
                string folder = StudioPath("backupscripts_dmca");
                Directory.CreateDirectory(folder);
                return folder;
            }

            internal static string TickerHtmlId(int slot)
            {
                return "ticker-html-" + Math.Max(1, Math.Min(6, slot));
            }

            private static void WriteUserHtmlManifest(string folder, string id, string name, string type, bool restartOnTrigger)
            {
                string manifest = "{\r\n" +
                    "  \"id\": \"" + Json(id) + "\",\r\n" +
                    "  \"name\": \"" + Json(name) + "\",\r\n" +
                    "  \"version\": \"1.0.0\",\r\n" +
                    "  \"type\": \"" + Json(type) + "\",\r\n" +
                    "  \"canvas\": { \"width\": 1920, \"height\": 1080 },\r\n" +
                    "  \"overlay\": \"overlay.html\",\r\n" +
                    "  \"defaultEnabled\": false,\r\n" +
                    "  \"restartOnTrigger\": " + restartOnTrigger.ToString().ToLowerInvariant() + ",\r\n" +
                    "  \"duration\": 0,\r\n" +
                    "  \"actions\": [\"show\", \"run\"]\r\n" +
                    "}\r\n";
                File.WriteAllText(Path.Combine(folder, "module.json"), manifest, new UTF8Encoding(false));
            }

            private string UserHtmlName(string backupFolder)
            {
                try
                {
                    string manifestPath = Path.Combine(backupFolder, "module.json");
                    string overlayPath = Path.Combine(backupFolder, "overlay.html");
                    if (!File.Exists(manifestPath) || !File.Exists(overlayPath)) return "";
                    string name = ManifestValue(File.ReadAllText(manifestPath, Encoding.UTF8), "name");
                    return name.Length == 0 ? Path.GetFileName(backupFolder) : name;
                }
                catch { return ""; }
            }

            internal string TickerHtmlName(int slot)
            {
                return UserHtmlName(Path.Combine(TickerHtmlScriptsFolder(), "slot-" + Math.Max(1, Math.Min(6, slot))));
            }

            internal string DmcaHtmlName()
            {
                return UserHtmlName(Path.Combine(DmcaHtmlScriptsFolder(), "dmca-custom"));
            }

            private bool InstallUserHtmlOverlay(string id, string displayName, string type, bool restartOnTrigger, string backupFolder, string dialogTitle)
            {
                using (var dialog = new OpenFileDialog { Filter = "HTML Overlay (*.html;*.htm)|*.html;*.htm", CheckFileExists = true, Title = dialogTitle })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                    var sourceInfo = new FileInfo(dialog.FileName);
                    if (sourceInfo.Length > 32L * 1024L * 1024L) { MessageBox.Show("The HTML file exceeds the 32 MB safety limit."); return false; }

                    string name = Clean(displayName);
                    if (name.Length == 0) name = Path.GetFileNameWithoutExtension(dialog.FileName);
                    name = Regex.Replace(name, "[\\r\\n\\t]+", " ").Trim();
                    if (name.Length > 80) name = name.Substring(0, 80);

                    string destinationRoot = Path.Combine(ModulesFolder(), id);
                    string stagingRoot = Path.Combine(ModulesFolder(), ".installing-" + Guid.NewGuid().ToString("N"));
                    string backupStaging = backupFolder + ".backup-" + Guid.NewGuid().ToString("N");
                    try
                    {
                        Directory.CreateDirectory(stagingRoot);
                        File.Copy(dialog.FileName, Path.Combine(stagingRoot, "overlay.html"), true);
                        string sourceFolder = Path.GetDirectoryName(dialog.FileName);
                        foreach (string assetFolder in new string[] { "assets", "cardimages", "images", "audio", "sounds" })
                        {
                            string candidate = Path.Combine(sourceFolder, assetFolder);
                            if (Directory.Exists(candidate)) CopyHtmlModuleAssets(candidate, Path.Combine(stagingRoot, assetFolder));
                        }
                        WriteUserHtmlManifest(stagingRoot, id, name, type, restartOnTrigger);
                        CopyHtmlModuleAssets(stagingRoot, backupStaging);

                        if (Directory.Exists(destinationRoot)) Directory.Delete(destinationRoot, true);
                        Directory.Move(stagingRoot, destinationRoot);
                        if (Directory.Exists(backupFolder)) Directory.Delete(backupFolder, true);
                        Directory.Move(backupStaging, backupFolder);

                        var enabled = LoadEnabledModules();
                        enabled.RemoveAll(delegate(string item) { return String.Equals(item, id, StringComparison.OrdinalIgnoreCase); });
                        SaveEnabledModules(enabled);
                        server.Publish("modules-reload", "{}");
                        MessageBox.Show(name + " was uploaded safely.\r\n\r\nCanvas: 1920 x 1080", "Creator Cam Studio");
                        return true;
                    }
                    catch (Exception error)
                    {
                        try { if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true); } catch { }
                        try { if (Directory.Exists(backupStaging)) Directory.Delete(backupStaging, true); } catch { }
                        MessageBox.Show("The HTML overlay could not be installed safely.\r\n\r\n" + error.Message, "Creator Cam Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
            }

            internal bool InstallTickerHtml(int slot, string displayName)
            {
                slot = Math.Max(1, Math.Min(6, slot));
                return InstallUserHtmlOverlay(
                    TickerHtmlId(slot),
                    displayName,
                    "COMMUNITY",
                    true,
                    Path.Combine(TickerHtmlScriptsFolder(), "slot-" + slot),
                    "Choose floating Tip Ticker HTML for slot " + slot + " (1920 x 1080)");
            }

            internal bool InstallDmcaHtml(string displayName)
            {
                return InstallUserHtmlOverlay(
                    "dmca-custom",
                    displayName,
                    "DECORATION",
                    false,
                    Path.Combine(DmcaHtmlScriptsFolder(), "dmca-custom"),
                    "Choose one DMCA HTML overlay (1920 x 1080)");
            }

            internal bool RestoreUserHtmlOverlay(string id, string backupFolder)
            {
                string manifestPath = Path.Combine(backupFolder, "module.json");
                string overlayPath = Path.Combine(backupFolder, "overlay.html");
                if (!File.Exists(manifestPath) || !File.Exists(overlayPath)) return false;
                string destination = Path.Combine(ModulesFolder(), id);
                if (Directory.Exists(destination)) return true;
                string staging = destination + ".loading-" + Guid.NewGuid().ToString("N");
                try
                {
                    CopyHtmlModuleAssets(backupFolder, staging);
                    Directory.Move(staging, destination);
                    server.Publish("modules-reload", "{}");
                    return true;
                }
                finally
                {
                    try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
                }
            }

            internal bool DeleteUserHtmlOverlay(string id, string backupFolder, string label, IWin32Window ownerWindow)
            {
                if (MessageBox.Show(ownerWindow, "Delete " + label + " and its local HTML backup?", "Creator Cam Studio", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return false;
                string backup = Path.GetFullPath(backupFolder);
                string tickerRoot = Path.GetFullPath(TickerHtmlScriptsFolder()) + Path.DirectorySeparatorChar;
                string dmcaRoot = Path.GetFullPath(DmcaHtmlScriptsFolder()) + Path.DirectorySeparatorChar;
                if ((backup.StartsWith(tickerRoot, StringComparison.OrdinalIgnoreCase) || backup.StartsWith(dmcaRoot, StringComparison.OrdinalIgnoreCase)) && Directory.Exists(backup)) Directory.Delete(backup, true);
                string external = Path.GetFullPath(Path.Combine(ModulesFolder(), id));
                string modulesRoot = Path.GetFullPath(ModulesFolder()) + Path.DirectorySeparatorChar;
                if (external.StartsWith(modulesRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(external)) Directory.Delete(external, true);
                var enabled = LoadEnabledModules();
                enabled.RemoveAll(delegate(string item) { return String.Equals(item, id, StringComparison.OrdinalIgnoreCase); });
                SaveEnabledModules(enabled);
                server.Publish("modules-reload", "{}");
                return true;
            }

            internal void InstallModulePackage()
            {
                using (var dialog = new OpenFileDialog { Filter = "Creator Cam UI Skin (*.zip)|*.zip", CheckFileExists = true, Title = "Choose a Creator Cam UI skin package" })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    if (new FileInfo(dialog.FileName).Length > 150L * 1024L * 1024L) { MessageBox.Show("UI skin package is larger than the 150 MB safety limit."); return; }
                    using (var archive = ZipFile.OpenRead(dialog.FileName))
                    {
                        if (archive.Entries.Count > 600) { MessageBox.Show("Module package contains too many files."); return; }
                        ZipArchiveEntry manifestEntry = null;
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            string normalized = entry.FullName.Replace('\\', '/');
                            if (normalized.EndsWith("module.json", StringComparison.OrdinalIgnoreCase)) { manifestEntry = entry; break; }
                        }
                        if (manifestEntry == null || manifestEntry.Length > 256 * 1024) { MessageBox.Show("A valid module.json manifest was not found."); return; }
                        string manifest;
                        using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8)) manifest = reader.ReadToEnd();
                        string id = ManifestValue(manifest, "id").ToLowerInvariant();
                        string type = ManifestValue(manifest, "type").ToUpperInvariant();
                        if (!Regex.IsMatch(id, "^[a-z0-9][a-z0-9-]{1,48}$") || type != "THEME")
                        {
                            MessageBox.Show("Only UI skin packages with module type THEME are accepted in this library."); return;
                        }
                        string prefix = manifestEntry.FullName.Substring(0, manifestEntry.FullName.Length - "module.json".Length).Replace('\\', '/');
                        string destinationRoot = Path.Combine(ModulesFolder(), id);
                        if (Directory.Exists(destinationRoot))
                        {
                            if (MessageBox.Show("Replace the installed " + id + " UI skin?", "Creator Cam Studio", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                            Directory.Delete(destinationRoot, true);
                        }
                        Directory.CreateDirectory(destinationRoot);
                        string safeRoot = Path.GetFullPath(destinationRoot) + Path.DirectorySeparatorChar;
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            if (String.IsNullOrEmpty(entry.Name)) continue;
                            if (entry.Length > 32L * 1024L * 1024L) throw new InvalidDataException("A module file exceeds the 32 MB safety limit.");
                            string normalized = entry.FullName.Replace('\\', '/');
                            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                            string relative = normalized.Substring(prefix.Length).TrimStart('/');
                            string extension = Path.GetExtension(relative).ToLowerInvariant();
                            if (!Regex.IsMatch(extension, "^\\.(json|html|css|js|png|jpg|jpeg|gif|webp|svg|wav|mp3|ogg|txt)$")) continue;
                            string destination = Path.GetFullPath(Path.Combine(destinationRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
                            if (!destination.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsafe module path was blocked.");
                            Directory.CreateDirectory(Path.GetDirectoryName(destination));
                            entry.ExtractToFile(destination, true);
                        }
                        if (!File.Exists(Path.Combine(destinationRoot, "module.json"))) { Directory.Delete(destinationRoot, true); MessageBox.Show("The module manifest must be at the package root."); return; }
                        var enabled = LoadEnabledModules(); if (!enabled.Contains(id)) enabled.Add(id); SaveEnabledModules(enabled);
                        server.Publish("modules-reload", "{}");
                        MessageBox.Show("UI skin installed safely. Refreshing the skin library.", "Creator Cam Studio");
                    }
                }
            }

            internal string InstallHtmlGameModule(string gameKind, string displayName)
            {
                using (var dialog = new OpenFileDialog { Filter = "HTML Overlay (*.html;*.htm)|*.html;*.htm", CheckFileExists = true, Title = "Choose a 1920 x 1080 HTML overlay" })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return "";
                    var sourceInfo = new FileInfo(dialog.FileName);
                    if (sourceInfo.Length > 32L * 1024L * 1024L) { MessageBox.Show("The HTML file exceeds the 32 MB safety limit."); return ""; }

                    string kind = Clean(gameKind).ToUpperInvariant();
                    if (!Regex.IsMatch(kind, "^(WHEEL|DICE|CUSTOM)$")) kind = "CUSTOM";
                    string name = Clean(displayName);
                    if (name.Length == 0) name = Path.GetFileNameWithoutExtension(dialog.FileName);
                    name = Regex.Replace(name, "[\\r\\n\\t]+", " ").Trim();
                    if (name.Length > 80) name = name.Substring(0, 80);
                    string slug = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
                    if (slug.Length < 2) slug = "custom-game";
                    if (slug.Length > 38) slug = slug.Substring(0, 38).Trim('-');
                    string id = "game-" + slug;
                    string wheelBackup = Path.Combine(WheelScriptsFolder(), id);
                    if (kind == "WHEEL" && !Directory.Exists(wheelBackup) && Directory.GetDirectories(WheelScriptsFolder()).Length >= 20)
                    {
                        MessageBox.Show("The wheel script library is full. Delete a script before adding another one.\r\n\r\nMaximum: 20 wheel scripts.");
                        return "";
                    }

                    string destinationRoot = Path.Combine(ModulesFolder(), id);
                    if (Directory.Exists(destinationRoot) &&
                        MessageBox.Show("Replace the installed " + name + " HTML game?", "Creator Cam Studio", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return "";

                    string stagingRoot = Path.Combine(ModulesFolder(), ".installing-" + Guid.NewGuid().ToString("N"));
                    try
                    {
                        Directory.CreateDirectory(stagingRoot);
                        File.Copy(dialog.FileName, Path.Combine(stagingRoot, "overlay.html"), true);
                        string sourceFolder = Path.GetDirectoryName(dialog.FileName);
                        foreach (string assetFolder in new string[] { "assets", "cardimages", "images", "audio", "sounds" })
                        {
                            string candidate = Path.Combine(sourceFolder, assetFolder);
                            if (Directory.Exists(candidate)) CopyHtmlModuleAssets(candidate, Path.Combine(stagingRoot, assetFolder));
                        }

                        string manifest = "{\r\n" +
                            "  \"id\": \"" + Json(id) + "\",\r\n" +
                            "  \"name\": \"" + Json(name) + "\",\r\n" +
                            "  \"version\": \"1.0.0\",\r\n" +
                            "  \"type\": \"GAME\",\r\n" +
                            "  \"gameKind\": \"" + kind + "\",\r\n" +
                            "  \"canvas\": { \"width\": 1920, \"height\": 1080 },\r\n" +
                            "  \"overlay\": \"overlay.html\",\r\n" +
                            "  \"defaultEnabled\": false,\r\n" +
                            "  \"restartOnTrigger\": true,\r\n" +
                            "  \"duration\": 30,\r\n" +
                            "  \"actions\": [\"run\"]\r\n" +
                            "}\r\n";
                        File.WriteAllText(Path.Combine(stagingRoot, "module.json"), manifest, new UTF8Encoding(false));
                        if (Directory.Exists(destinationRoot)) Directory.Delete(destinationRoot, true);
                        Directory.Move(stagingRoot, destinationRoot);
                        if (kind == "WHEEL") BackupWheelModule(id);

                        var enabled = LoadEnabledModules();
                        if (!enabled.Contains(id)) enabled.Add(id);
                        SaveEnabledModules(enabled);
                        server.Publish("modules-reload", "{}");
                        MessageBox.Show(name + " was added to Backstage as a manual " + kind.ToLowerInvariant() + " module.\r\n\r\nCanvas maximum: 1920 x 1080", "Creator Cam Studio");
                        return id;
                    }
                    catch (Exception error)
                    {
                        try { if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true); } catch { }
                        MessageBox.Show("The HTML module could not be installed safely.\r\n\r\n" + error.Message, "Creator Cam Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return "";
                    }
                }
            }

            private static void CopyHtmlModuleAssets(string sourceRoot, string destinationRoot)
            {
                string[] files = Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories);
                if (files.Length > 500) throw new InvalidDataException("The HTML overlay contains too many asset files.");
                string safeSource = Path.GetFullPath(sourceRoot) + Path.DirectorySeparatorChar;
                string safeDestination = Path.GetFullPath(destinationRoot) + Path.DirectorySeparatorChar;
                long total = 0;
                foreach (string file in files)
                {
                    var info = new FileInfo(file);
                    if (info.Length > 32L * 1024L * 1024L) throw new InvalidDataException("An HTML overlay asset exceeds the 32 MB safety limit.");
                    total += info.Length;
                    if (total > 150L * 1024L * 1024L) throw new InvalidDataException("The HTML overlay assets exceed the 150 MB safety limit.");
                    string extension = info.Extension.ToLowerInvariant();
                    if (!Regex.IsMatch(extension, "^\\.(json|html|css|js|mjs|png|jpg|jpeg|gif|webp|svg|wav|mp3|ogg|txt|woff|woff2|ttf|otf|mp4|webm)$")) continue;
                    string fullSource = Path.GetFullPath(file);
                    if (!fullSource.StartsWith(safeSource, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("An unsafe source asset path was blocked.");
                    string relative = fullSource.Substring(safeSource.Length);
                    string destination = Path.GetFullPath(Path.Combine(destinationRoot, relative));
                    if (!destination.StartsWith(safeDestination, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("An unsafe destination asset path was blocked.");
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(file, destination, true);
                }
            }

            internal List<string[]> EvolutionModuleCatalog()
            {
                var enabled = LoadEnabledModules();
                var rows = new List<string[]>();
                rows.Add(new string[] { "top-tipper-card", "Top Tipper Card", "COMMUNITY", "Bundled", enabled.Contains("top-tipper-card") ? "Enabled" : "Disabled", "" });
                rows.Add(new string[] { "fan-card", "Saved Fan Card", "COMMUNITY", "Bundled", enabled.Contains("fan-card") ? "Enabled" : "Disabled", "" });
                rows.Add(new string[] { "recent-supporter", "Recent Supporter", "COMMUNITY", "Bundled", enabled.Contains("recent-supporter") ? "Enabled" : "Disabled", "" });
                rows.Add(new string[] { "tip-ticker", "Activity Tip Ticker", "COMMUNITY", "Bundled", enabled.Contains("tip-ticker") ? "Enabled" : "Disabled", "" });
                rows.Add(new string[] { "vip-badge", "VIP Badge", "COMMUNITY", "Bundled", enabled.Contains("vip-badge") ? "Enabled" : "Disabled", "" });
                rows.Add(new string[] { "halloween-pack", "Halloween Theme 2026", "THEME", "Bundled", enabled.Contains("halloween-pack") ? "Enabled" : "Disabled", "" });
                rows.Add(new string[] { "wheel-new-year-2025", "New Year 2025 Wheel", "GAME", "Bundled", enabled.Contains("wheel-new-year-2025") ? "Enabled" : "Disabled", "WHEEL" });
                rows.Add(new string[] { "wheel-christmas", "Christmas Wheel", "GAME", "Bundled", enabled.Contains("wheel-christmas") ? "Enabled" : "Disabled", "WHEEL" });
                rows.Add(new string[] { "wheel-halloween", "Halloween Wheel", "GAME", "Bundled", enabled.Contains("wheel-halloween") ? "Enabled" : "Disabled", "WHEEL" });
                rows.Add(new string[] { "action-1-black-cat", "Action 1 - Black Cat", "ALERT", "Bundled", enabled.Contains("action-1-black-cat") ? "Enabled" : "Disabled", "" });
                rows.Add(new string[] { "action-2-halloween-sides", "Action 2 - Halloween Sides", "DECORATION", "Bundled", enabled.Contains("action-2-halloween-sides") ? "Enabled" : "Disabled", "" });
                rows.Add(new string[] { "action-3-flying-witch", "Action 3 - Flying Witch", "ALERT", "Bundled", enabled.Contains("action-3-flying-witch") ? "Enabled" : "Disabled", "" });
                rows.Add(new string[] { "action-4-warlock", "Action 4 - Warlock", "ALERT", "Bundled", enabled.Contains("action-4-warlock") ? "Enabled" : "Disabled", "" });
                rows.Add(new string[] { "action-5-pumpkin", "Action 5 - Pumpkin", "ALERT", "Bundled", enabled.Contains("action-5-pumpkin") ? "Enabled" : "Disabled", "" });
                rows.Add(new string[] { "action-6-ghost", "Action 6 - Ghost", "ALERT", "Bundled", enabled.Contains("action-6-ghost") ? "Enabled" : "Disabled", "" });
                rows.Add(new string[] { "my-secret-show", "My Secret Show Camera Cover", "DECORATION", "Bundled", enabled.Contains("my-secret-show") ? "Enabled" : "Disabled", "" });
                foreach (string directory in Directory.GetDirectories(ModulesFolder()))
                {
                    string manifestPath = Path.Combine(directory, "module.json"); if (!File.Exists(manifestPath)) continue;
                    string manifest = File.ReadAllText(manifestPath);
                    string id = ManifestValue(manifest, "id").ToLowerInvariant(); if (!Regex.IsMatch(id, "^[a-z0-9][a-z0-9-]{1,48}$")) continue;
                    rows.Add(new string[] { id, ManifestValue(manifest, "name"), ManifestValue(manifest, "type").ToUpperInvariant(), "External", enabled.Contains(id) ? "Enabled" : "Disabled", ManifestValue(manifest, "gameKind").ToUpperInvariant() });
                }
                return rows;
            }

            internal void SetEvolutionModule(string id, bool enabledState)
            {
                id = Clean(id).ToLowerInvariant(); if (!Regex.IsMatch(id, "^[a-z0-9][a-z0-9-]{1,48}$")) return;
                var enabled = LoadEnabledModules();
                enabled.RemoveAll(delegate(string item) { return String.Equals(item, id, StringComparison.OrdinalIgnoreCase); });
                if (enabledState) enabled.Add(id);
                SaveEnabledModules(enabled);
                server.Publish("module-enable", "{\"id\":\"" + Json(id) + "\",\"enabled\":" + enabledState.ToString().ToLowerInvariant() + "}");
                if (id == "dmca") PublishDmcaSettings(enabledState);
            }

            private void PublishDmcaSettings(bool enabledState)
            {
                string[] values = MovingWatermarkSettingsValues();
                values[3] = enabledState.ToString();
                File.WriteAllText(StudioPath("moving-watermark-v4.txt"), String.Join("\t", values), new UTF8Encoding(false));
                server.Publish("dmca-settings", MovingWatermarkSettingsJson(values));
            }

            internal static string ManifestValue(string json, string key)
            {
                Match match = Regex.Match(json ?? "", "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase);
                return match.Success ? Clean(match.Groups[1].Value) : "";
            }

            internal static bool IsTemplateJson(string text)
            {
                string clean = (text ?? "").Trim();
                return clean.StartsWith("{") && clean.EndsWith("}") && clean.IndexOf("\"zones\"", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        private sealed partial class StudioDashboardForm
        {
            private readonly Button[] actionButtons = new Button[20];
            private readonly ToolTip actionToolTip = new ToolTip();
            private readonly HashSet<int> activeActionSlots = new HashSet<int>();
            private ComboBox actionSlot;
            private TextBox actionSlotName;
            private Label actionSlotStatus;
            // Retained only so older private library helpers continue to compile; the v8 UI never creates or preloads this library.
            private ComboBox actionScriptSelector;
            private TextBox actionScriptName;
            private Label actionScriptStatus;
            private DataGridView moduleGrid;
            private TextBox secretShowTitle, secretShowHeadline, secretShowSubtitle, secretShowWaiting, secretShowWelcome;
            private NumericUpDown secretShowPrice, secretShowDuration, secretShowTeaseDuration;
            private ComboBox secretShowTheme, secretShowIntensity;
            private CheckBox secretShowRotation;
            private Label secretShowStatus;
            private System.Windows.Forms.Timer secretShowStatusTimer;
            private ComboBox tickerHtmlSlot;
            private TextBox tickerHtmlName, dmcaHtmlName;
            private TextBox movingWatermarkTitle, movingWatermarkName, movingWatermarkTagline;
            private ComboBox movingWatermarkOpacity;
            private Label movingWatermarkStatus;
            private bool movingWatermarkVisible;
            private TextBox dmcaProtectedModel;
            private ComboBox dmcaProtectedOpacity;
            private Label dmcaProtectedStatus;
            private TextBox manualRecentUsername, manualRecentMessage;
            private NumericUpDown manualRecentAmount;
            private Label manualRecentStatus;
            private Button lastTipperVisibilityButton, lastSupporterVisibilityButton, vipBadgeVisibilityButton;
            private bool lastTipperOverlayVisible = true, lastSupporterOverlayVisible = true, vipBadgeOverlayVisible = true;
            private Label tickerHtmlStatus, dmcaHtmlStatus;
            private NumericUpDown resultDuration, resultFade;
            private ComboBox resultAnimation;
            private CheckBox resultUsername, resultPrize, resultValue;
            private TextBox templateName, templateEditor;
            private ComboBox templateSelector;
            private ComboBox connectorPlatform;
            private Label connectorStatus, connectorLastEvent, connectorCount;
            private Label chromeBridgeStatus, chromeBridgeRoom, chromeBridgeLastTip, chromeBridgeCount, chromeBridgeModelStatus;
            private int chromeBridgeEvents;
            private TextBox chromeBridgeModelAddress;
            private TextBox connectorUsername, connectorToken;
            private ComboBox connectorEnvironment;
            private CheckBox connectorAutoGameRequests;
            private NumericUpDown connectorDiceMinimum, connectorWheelMinimum;
            private Process connectorProcess;
            private int connectorEvents;

            private TabPage BuildActionDeckTab()
            {
                var page = Page("ACTION DECK");
                var deck = Group("CREATOR ACTION BUTTONS • 20 HTML OVERLAY SLOTS", 15, 15, 915, 200); page.Controls.Add(deck);
                deck.Controls.Add(LabelAt("All Actions start OFF. Click an assigned number once = ON. Click the same number again = OFF. Actions have NO Studio timer.", 18, 30));
                for (int i = 0; i < 20; i++)
                {
                    int slot = i + 1;
                    int column = i % 10;
                    int row = i / 10;
                    actionButtons[i] = ButtonAt(slot.ToString(), 18 + column * 86, 55 + row * 55, 72, delegate { TriggerAction(slot); });
                    actionButtons[i].Font = new Font("Segoe UI", 12, FontStyle.Bold);
                    deck.Controls.Add(actionButtons[i]);
                }

                var scripts = Group("ACTION DECK LOADER • RETRO SINGLE-SLOT + NEW 20-PACK MODE", 15, 235, 915, 405); page.Controls.Add(scripts);

                scripts.Controls.Add(LabelAt("RETRO MODE • replace one Action at a time", 18, 28));
                scripts.Controls.Add(LabelAt("Action slot", 18, 58));
                actionSlot = Combo(new string[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20" }, 18, 80, 75); scripts.Controls.Add(actionSlot);
                actionSlot.SelectedIndexChanged += delegate { RefreshActionSlotEditor(); };
                scripts.Controls.Add(LabelAt("Overlay name (optional)", 110, 58));
                actionSlotName = Box("", 110, 80, 300); scripts.Controls.Add(actionSlotName);
                scripts.Controls.Add(ButtonAt("BROWSE / REPLACE ONE HTML", 430, 77, 250, delegate { BrowseActionSlotHtml(); }));

                scripts.Controls.Add(LabelAt("20-PACK MODE • validate, back up, then replace all twenty slots together", 18, 132));
                scripts.Controls.Add(ButtonAt("IMPORT / REPLACE 20-PACK ZIP", 18, 158, 270, delegate
                {
                    StopAllActions();
                    if (owner.InstallActionPackZip())
                    {
                        activeActionSlots.Clear();
                        RefreshActionButtons();
                        RefreshActionSlotEditor();
                        SetEvent("20-Action Pack imported");
                    }
                }));
                scripts.Controls.Add(ButtonAt("RESTORE PREVIOUS 20-PACK", 298, 158, 245, delegate
                {
                    StopAllActions();
                    if (owner.RestorePreviousActionPack())
                    {
                        activeActionSlots.Clear();
                        RefreshActionButtons();
                        RefreshActionSlotEditor();
                        SetEvent("Previous Action Pack restored");
                    }
                }));

                scripts.Controls.Add(ButtonAt("STOP ALL", 18, 218, 125, delegate { StopAllActions(); }));
                scripts.Controls.Add(ButtonAt("CLEAR SLOT", 153, 218, 135, delegate { ClearSelectedActionSlot(); }));
                scripts.Controls.Add(ButtonAt("REFRESH", 298, 218, 125, delegate { RefreshActionSlotEditor(); RefreshActionButtons(); }));
                actionSlotStatus = LabelAt("Slot 1: EMPTY", 18, 270); actionSlotStatus.Size = new Size(855, 45); actionSlotStatus.AutoSize = false; scripts.Controls.Add(actionSlotStatus);
                var actionHelp = LabelAt("RETRO keeps the original one-slot-at-a-time workflow. 20-PACK imports a complete ZIP with action-01...action-20 (or slot-01...slot-20) folders. The entire pack is validated before replacement, and the old deck is saved for recovery.", 18, 325);
                actionHelp.Size = new Size(850, 64); actionHelp.AutoSize = false; scripts.Controls.Add(actionHelp);
                RefreshActionButtons();
                RefreshActionSlotEditor();
                return page;
            }

            private TabPage BuildModulesTab()
            {
                var page = Page("UI SKINS + HTML");
                var library = Group("UI SKIN LIBRARY • THEME PACKAGES ONLY", 15, 15, 915, 300); page.Controls.Add(library);
                moduleGrid = new DataGridView { Location = new Point(18, 38), Size = new Size(875, 165), ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.FromArgb(15, 17, 15), ForeColor = Color.Black };
                moduleGrid.Columns.Add("Id", "SKIN ID"); moduleGrid.Columns.Add("Name", "UI SKIN"); moduleGrid.Columns.Add("Type", "CLASS"); moduleGrid.Columns.Add("Source", "SOURCE"); moduleGrid.Columns.Add("Status", "STATUS");
                library.Controls.Add(moduleGrid);
                library.Controls.Add(ButtonAt("REFRESH", 18, 218, 125, delegate { RefreshModuleLibrary(); }));
                library.Controls.Add(ButtonAt("INSTALL SKIN ZIP", 153, 218, 185, delegate { owner.InstallModulePackage(); RefreshModuleLibrary(); }));
                library.Controls.Add(ButtonAt("ENABLE SELECTED", 348, 218, 170, delegate { SetSelectedModule(true); }));
                library.Controls.Add(ButtonAt("DISABLE SELECTED", 528, 218, 170, delegate { SetSelectedModule(false); }));
                library.Controls.Add(LabelAt("This library accepts UI skins only. Games, actions, tickers, and DMCA HTML are managed in their dedicated areas.", 18, 263));

                var ticker = Group("FLOATING TIP TICKER HTML • SIX SLOTS MAXIMUM", 15, 335, 915, 230); page.Controls.Add(ticker);
                ticker.Controls.Add(LabelAt("Slot", 18, 36)); tickerHtmlSlot = Combo(new string[] { "1", "2", "3", "4", "5", "6" }, 18, 58, 75); ticker.Controls.Add(tickerHtmlSlot);
                tickerHtmlSlot.SelectedIndexChanged += delegate { RefreshTickerHtmlStatus(); };
                ticker.Controls.Add(LabelAt("Overlay name", 110, 36)); tickerHtmlName = Box("My Floating Ticker", 110, 58, 300); ticker.Controls.Add(tickerHtmlName);
                ticker.Controls.Add(ButtonAt("UPLOAD / REPLACE HTML", 430, 55, 205, delegate { UploadTickerHtml(); }));
                ticker.Controls.Add(ButtonAt("SHOW SELECTED", 18, 105, 170, delegate { ShowTickerHtml(); }));
                ticker.Controls.Add(ButtonAt("HIDE SELECTED", 198, 105, 170, delegate { HideTickerHtml(); }));
                ticker.Controls.Add(ButtonAt("DELETE SELECTED", 378, 105, 180, delegate { DeleteTickerHtml(); }));
                tickerHtmlStatus = LabelAt("Slot 1: Empty", 18, 158); tickerHtmlStatus.Size = new Size(850, 44); tickerHtmlStatus.AutoSize = false; ticker.Controls.Add(tickerHtmlStatus);
                ticker.Controls.Add(LabelAt("Each uploaded HTML may float anywhere inside its own 1920 x 1080 canvas. No HTML is included by default.", 18, 195));

                var dmca = Group("PROTECTED LIVE BROADCAST WATERMARK + OPTIONAL CUSTOM HTML", 15, 585, 915, 355); page.Controls.Add(dmca);

                dmca.Controls.Add(LabelAt("Model / username", 18, 32));
                dmcaProtectedModel = Box("model_name", 18, 54, 285); dmca.Controls.Add(dmcaProtectedModel);
                dmca.Controls.Add(LabelAt("Opacity", 322, 32));
                dmcaProtectedOpacity = Combo(new string[] { "10%", "20%", "30%", "40%", "50%", "60%", "70%", "75%", "80%", "82%", "85%", "90%", "95%" }, 322, 54, 105); dmca.Controls.Add(dmcaProtectedOpacity);
                dmca.Controls.Add(ButtonAt("SAVE + SHOW", 447, 51, 145, delegate { SaveDmcaProtectedWatermark(true); }));
                dmca.Controls.Add(ButtonAt("SAVE", 602, 51, 105, delegate { SaveDmcaProtectedWatermark(movingWatermarkVisible); }));
                dmca.Controls.Add(ButtonAt("HIDE", 717, 51, 105, delegate { SaveDmcaProtectedWatermark(false); }));
                dmcaProtectedStatus = LabelAt("Protected watermark: loading settings...", 18, 98); dmcaProtectedStatus.Size = new Size(850, 36); dmcaProtectedStatus.AutoSize = false; dmca.Controls.Add(dmcaProtectedStatus);
                dmca.Controls.Add(LabelAt("Built-in watermark text: PROTECTED LIVE BROADCAST • @model • Unauthorized recording or redistribution is prohibited.", 18, 126));

                dmca.Controls.Add(LabelAt("Optional custom HTML overlay name", 18, 166)); dmcaHtmlName = Box("My DMCA Overlay", 18, 188, 300); dmca.Controls.Add(dmcaHtmlName);
                dmca.Controls.Add(ButtonAt("UPLOAD / REPLACE HTML", 338, 185, 205, delegate { UploadDmcaHtml(); }));
                dmca.Controls.Add(ButtonAt("TURN CUSTOM ON", 18, 235, 155, delegate { TurnDmcaHtmlOn(); }));
                dmca.Controls.Add(ButtonAt("TURN CUSTOM OFF", 183, 235, 165, delegate { TurnDmcaHtmlOff(); }));
                dmca.Controls.Add(ButtonAt("DELETE CUSTOM HTML", 358, 235, 185, delegate { DeleteDmcaHtml(); }));
                dmcaHtmlStatus = LabelAt("No DMCA HTML uploaded. Nothing is preloaded.", 565, 229); dmcaHtmlStatus.Size = new Size(320, 58); dmcaHtmlStatus.AutoSize = false; dmca.Controls.Add(dmcaHtmlStatus);
                dmca.Controls.Add(LabelAt("Custom HTML remains supported and separate. The built-in protected watermark above needs no uploaded HTML.", 18, 305));
                RefreshModuleLibrary();
                RefreshTickerHtmlStatus();
                RefreshDmcaHtmlStatus();
                LoadDmcaProtectedWatermarkSettings();
                return page;
            }

            private GroupBox BuildMySecretShowGroup()
            {
                var group = Group("MY SECRET SHOW • MANUAL OBS CAMERA COVER", 15, 535, 915, 465);
                secretShowStatus = LabelAt("Status: INACTIVE • Camera visible until you use these controls", 18, 33); secretShowStatus.ForeColor = Color.LightGreen; secretShowStatus.Font = new Font("Segoe UI", 10, FontStyle.Bold); group.Controls.Add(secretShowStatus);
                group.Controls.Add(LabelAt("Token price", 18, 70)); secretShowPrice = NumberAt(25, 1, 999999, 18, 92, 105, 0); group.Controls.Add(secretShowPrice);
                group.Controls.Add(LabelAt("Theme", 140, 70)); secretShowTheme = Combo(new string[] { "My Secret Show Blue", "Midnight Purple", "Obsidian Emerald", "Crimson Night" }, 140, 92, 190); group.Controls.Add(secretShowTheme);
                group.Controls.Add(LabelAt("Animation intensity", 350, 70)); secretShowIntensity = Combo(new string[] { "Off", "Low", "Medium", "High" }, 350, 92, 145); group.Controls.Add(secretShowIntensity);
                group.Controls.Add(LabelAt("Unlock seconds", 515, 70)); secretShowDuration = NumberAt(5, 4, 6, 515, 92, 105, 0); group.Controls.Add(secretShowDuration);
                secretShowRotation = new CheckBox { Text = "Rotate locked messages", Location = new Point(650, 94), AutoSize = true, ForeColor = TextColor, Checked = true }; group.Controls.Add(secretShowRotation);

                group.Controls.Add(LabelAt("Show title", 18, 133)); secretShowTitle = Box("MY SECRET SHOW", 18, 155, 270); group.Controls.Add(secretShowTitle);
                group.Controls.Add(LabelAt("Locked headline", 305, 133)); secretShowHeadline = Box("SECRET SHOW LOCKED", 305, 155, 300); group.Controls.Add(secretShowHeadline);
                group.Controls.Add(LabelAt("Subtitle (use {price})", 622, 133)); secretShowSubtitle = Box("Tip {price} Tokens to Unlock the Camera", 622, 155, 270); group.Controls.Add(secretShowSubtitle);
                group.Controls.Add(LabelAt("Waiting message", 18, 197)); secretShowWaiting = Box("Waiting for an approved tipper…", 18, 219, 420); group.Controls.Add(secretShowWaiting);
                group.Controls.Add(LabelAt("Unlock welcome message", 455, 197)); secretShowWelcome = Box("Welcome to My Secret Show", 455, 219, 437); group.Controls.Add(secretShowWelcome);

                group.Controls.Add(ButtonAt("LOCK", 18, 270, 130, delegate { PublishSecretShowCommand("lock"); }));
                group.Controls.Add(ButtonAt("UNLOCK", 158, 270, 130, delegate { PublishSecretShowCommand("unlock"); }));
                group.Controls.Add(ButtonAt("CANCEL", 298, 270, 130, delegate { PublishSecretShowCommand("cancel"); }));
                group.Controls.Add(ButtonAt("TEST", 438, 270, 130, delegate { PublishSecretShowCommand("test"); }));
                group.Controls.Add(ButtonAt("RESET", 578, 270, 130, delegate { PublishSecretShowCommand("resetOverlay"); }));
                group.Controls.Add(ButtonAt("TEASE VIEW", 718, 270, 174, delegate { PublishSecretShowCommand("tease"); }));
                group.Controls.Add(ButtonAt("SAVE + APPLY SETTINGS", 18, 337, 205, delegate { SaveSecretShowSettings(true); }));
                group.Controls.Add(LabelAt("Tease seconds", 245, 315)); secretShowTeaseDuration = NumberAt(3, 1, 180, 245, 337, 95, 0); group.Controls.Add(secretShowTeaseDuration);
                group.Controls.Add(LabelAt("No black layer: only the My Secret Show artwork. Set overall opacity in OBS Studio.", 365, 345));
                group.Controls.Add(LabelAt("Starts INACTIVE every app launch. Camera stays visible until you manually use LOCK / TEST / TEASE or apply this UI.", 18, 392));
                group.Controls.Add(LabelAt("Tease View temporarily hides the entire camera cover for the selected number of seconds, then automatically locks the camera again.", 18, 424));
                LoadSecretShowSettings();
                // IMPORTANT: loading this page must never activate the camera cover.
                // My Secret Show is enabled only after the user deliberately presses
                // one of its command buttons or SAVE + APPLY SETTINGS.
                return group;
            }

            private GroupBox BuildMovingWatermarkGroup()
            {
                var group = Group("PROTECTED LIVE BROADCAST • MOVING WATERMARK", 15, 1020, 915, 255);
                movingWatermarkStatus = LabelAt("Status: HIDDEN • Changes are saved in this app", 18, 33);
                movingWatermarkStatus.ForeColor = Color.LightGreen;
                movingWatermarkStatus.Font = new Font("Segoe UI", 10, FontStyle.Bold);
                group.Controls.Add(movingWatermarkStatus);

                group.Controls.Add(LabelAt("Title", 18, 70)); movingWatermarkTitle = Box("PROTECTED LIVE BROADCAST", 18, 92, 260); group.Controls.Add(movingWatermarkTitle);
                group.Controls.Add(LabelAt("Model / username", 298, 70)); movingWatermarkName = Box("@model_name", 298, 92, 260); group.Controls.Add(movingWatermarkName);
                group.Controls.Add(LabelAt("Tagline", 578, 70)); movingWatermarkTagline = Box("Unauthorized recording or redistribution is prohibited", 578, 92, 314); group.Controls.Add(movingWatermarkTagline);

                group.Controls.Add(LabelAt("Opacity", 18, 137));
                movingWatermarkOpacity = Combo(new string[] { "10%", "20%", "30%", "40%", "50%", "60%", "70%", "75%", "80%", "82%", "85%", "90%", "95%" }, 18, 159, 105); group.Controls.Add(movingWatermarkOpacity);
                group.Controls.Add(ButtonAt("SAVE + SHOW", 145, 156, 180, delegate { SaveMovingWatermark(true); }));
                group.Controls.Add(ButtonAt("SAVE CHANGES", 335, 156, 180, delegate { SaveMovingWatermark(movingWatermarkVisible); }));
                group.Controls.Add(ButtonAt("HIDE", 525, 156, 140, delegate { SaveMovingWatermark(false); }));
                group.Controls.Add(LabelAt("Editable per model. Opacity is saved and restored on the next launch.", 685, 162));
                LoadMovingWatermarkSettings();
                return group;
            }

            private GroupBox BuildManualLastSupporterGroup()
            {
                var group = Group("LAST TIPPER + LAST SUPPORTER + VIP • OVERLAY DISPLAY CONTROL", 15, 1275, 915, 360);
                manualRecentStatus = LabelAt("Status: No manual Last Supporter saved", 18, 33);
                manualRecentStatus.ForeColor = Color.LightGreen;
                manualRecentStatus.Font = new Font("Segoe UI", 10, FontStyle.Bold);
                group.Controls.Add(manualRecentStatus);

                group.Controls.Add(LabelAt("Supporter username", 18, 70)); manualRecentUsername = Box("", 18, 92, 270); group.Controls.Add(manualRecentUsername);
                group.Controls.Add(LabelAt("Token amount", 305, 70)); manualRecentAmount = NumberAt(0, 0, 999999, 305, 92, 140, 0); group.Controls.Add(manualRecentAmount);
                group.Controls.Add(LabelAt("Optional subtitle / message", 465, 70)); manualRecentMessage = Box("MOST RECENT", 465, 92, 427); group.Controls.Add(manualRecentMessage);
                group.Controls.Add(ButtonAt("SET LAST SUPPORTER", 18, 145, 205, delegate { SetManualLastSupporter(); }));
                group.Controls.Add(ButtonAt("CLEAR LAST SUPPORTER", 238, 145, 205, delegate { ClearManualLastSupporter(); }));
                var note = LabelAt("Last Tipper follows the newest tip. Last Supporter is the manual saved card above. They are now separate overlays.", 465, 145);
                note.AutoSize = false; note.Size = new Size(425, 55); group.Controls.Add(note);

                lastTipperVisibilityButton = ButtonAt("LAST TIPPER OVERLAY: ON", 18, 213, 265, delegate { SetSupporterOverlayVisibility(!lastTipperOverlayVisible, lastSupporterOverlayVisible, vipBadgeOverlayVisible); });
                group.Controls.Add(lastTipperVisibilityButton);
                lastSupporterVisibilityButton = ButtonAt("LAST SUPPORTER OVERLAY: ON", 298, 213, 285, delegate { SetSupporterOverlayVisibility(lastTipperOverlayVisible, !lastSupporterOverlayVisible, vipBadgeOverlayVisible); });
                group.Controls.Add(lastSupporterVisibilityButton);
                group.Controls.Add(LabelAt("OFF hides the card only; it does not erase names, totals, or saved session data.", 605, 220));

                vipBadgeVisibilityButton = ButtonAt("WAITING FOR VIP: ON", 18, 263, 265, delegate { SetSupporterOverlayVisibility(lastTipperOverlayVisible, lastSupporterOverlayVisible, !vipBadgeOverlayVisible); });
                group.Controls.Add(vipBadgeVisibilityButton);
                group.Controls.Add(LabelAt("OFF hides the VIP badge only. Supporter and VIP data are preserved.", 305, 270));
                LoadSupporterOverlayVisibility();
                LoadManualLastSupporter();
                return group;
            }

            private void LoadSupporterOverlayVisibility()
            {
                lastTipperOverlayVisible = true;
                lastSupporterOverlayVisible = true;
                vipBadgeOverlayVisible = true;
                try
                {
                    string path = owner.StudioPath("supporter-overlay-visibility-v1.txt");
                    if (File.Exists(path))
                    {
                        string[] values = File.ReadAllText(path).Split('\t');
                        if (values.Length > 0) Boolean.TryParse(values[0], out lastTipperOverlayVisible);
                        if (values.Length > 1) Boolean.TryParse(values[1], out lastSupporterOverlayVisible);
                        if (values.Length > 2) Boolean.TryParse(values[2], out vipBadgeOverlayVisible);
                    }
                }
                catch { }
                RefreshSupporterOverlayButtons();
            }

            private void SetSupporterOverlayVisibility(bool lastTipperVisible, bool lastSupporterVisible, bool vipBadgeVisible)
            {
                lastTipperOverlayVisible = lastTipperVisible;
                lastSupporterOverlayVisible = lastSupporterVisible;
                vipBadgeOverlayVisible = vipBadgeVisible;
                File.WriteAllText(owner.StudioPath("supporter-overlay-visibility-v1.txt"), lastTipperOverlayVisible + "\t" + lastSupporterOverlayVisible + "\t" + vipBadgeOverlayVisible);
                owner.server.Publish("supporter-overlay-visibility", "{\"lastTipper\":" + lastTipperOverlayVisible.ToString().ToLowerInvariant() + ",\"lastSupporter\":" + lastSupporterOverlayVisible.ToString().ToLowerInvariant() + ",\"vipBadge\":" + vipBadgeOverlayVisible.ToString().ToLowerInvariant() + "}");
                RefreshSupporterOverlayButtons();
                SetEvent("Supporter overlay visibility changed");
            }

            private void RefreshSupporterOverlayButtons()
            {
                if (lastTipperVisibilityButton != null)
                {
                    lastTipperVisibilityButton.Text = "LAST TIPPER OVERLAY: " + (lastTipperOverlayVisible ? "ON" : "OFF");
                    lastTipperVisibilityButton.BackColor = lastTipperOverlayVisible ? Color.SeaGreen : Color.FromArgb(58, 61, 57);
                    lastTipperVisibilityButton.ForeColor = Color.White;
                }
                if (lastSupporterVisibilityButton != null)
                {
                    lastSupporterVisibilityButton.Text = "LAST SUPPORTER OVERLAY: " + (lastSupporterOverlayVisible ? "ON" : "OFF");
                    lastSupporterVisibilityButton.BackColor = lastSupporterOverlayVisible ? Color.SeaGreen : Color.FromArgb(58, 61, 57);
                    lastSupporterVisibilityButton.ForeColor = Color.White;
                }
                if (vipBadgeVisibilityButton != null)
                {
                    vipBadgeVisibilityButton.Text = "WAITING FOR VIP: " + (vipBadgeOverlayVisible ? "ON" : "OFF");
                    vipBadgeVisibilityButton.BackColor = vipBadgeOverlayVisible ? Color.SeaGreen : Color.FromArgb(58, 61, 57);
                    vipBadgeVisibilityButton.ForeColor = Color.White;
                }
            }

            private void LoadManualLastSupporter()
            {
                string[] values = owner.ManualRecentSupporterValues();
                manualRecentUsername.Text = values[0];
                decimal amount;
                if (!Decimal.TryParse(values[1], out amount)) amount = 0;
                manualRecentAmount.Value = Math.Max(manualRecentAmount.Minimum, Math.Min(manualRecentAmount.Maximum, amount));
                manualRecentMessage.Text = values[2];
                manualRecentStatus.Text = values[0].Length > 0 ? "Status: SHOWING • " + values[0] : "Status: No manual Last Supporter saved";
                manualRecentStatus.ForeColor = values[0].Length > 0 ? Gold : Color.LightGreen;
            }

            private void SetManualLastSupporter()
            {
                string username = (manualRecentUsername.Text ?? "").Trim();
                if (username.Length == 0) { MessageBox.Show("Enter a supporter username before setting Last Supporter.", "Creator Cam Studio"); manualRecentUsername.Focus(); return; }
                if (!owner.SaveManualRecentSupporterSettings(username, (int)manualRecentAmount.Value, manualRecentMessage.Text)) { MessageBox.Show("The Last Supporter could not be saved.", "Creator Cam Studio"); return; }
                manualRecentStatus.Text = "Status: SHOWING • " + username;
                manualRecentStatus.ForeColor = Gold;
                SetEvent("Manual Last Supporter updated");
            }

            private void ClearManualLastSupporter()
            {
                owner.ClearManualRecentSupporterSettings();
                manualRecentUsername.Text = "";
                manualRecentAmount.Value = 0;
                manualRecentMessage.Text = "MOST RECENT";
                manualRecentStatus.Text = "Status: No manual Last Supporter saved";
                manualRecentStatus.ForeColor = Color.LightGreen;
                SetEvent("Manual Last Supporter cleared");
            }

            private static int WatermarkOpacityPercent(ComboBox selector, int fallback)
            {
                string text = selector == null ? "" : (selector.SelectedItem == null ? selector.Text : selector.SelectedItem.ToString());
                int value;
                if (!Int32.TryParse((text ?? "").Replace("%", "").Trim(), out value)) value = fallback;
                return Math.Max(10, Math.Min(95, value));
            }

            private static void SelectWatermarkOpacity(ComboBox selector, string value)
            {
                if (selector == null) return;
                int percent;
                if (!Int32.TryParse(value, out percent)) percent = 82;
                string target = Math.Max(10, Math.Min(95, percent)).ToString() + "%";
                int index = selector.Items.IndexOf(target);
                if (index >= 0) selector.SelectedIndex = index; else selector.Text = target;
            }

            private void LoadMovingWatermarkSettings()
            {
                string[] values = owner.MovingWatermarkSettingsValues();
                movingWatermarkTitle.Text = values[0];
                movingWatermarkName.Text = values[1];
                movingWatermarkTagline.Text = values[2];
                SelectWatermarkOpacity(movingWatermarkOpacity, values.Length > 4 ? values[4] : "82");
                Boolean.TryParse(values[3], out movingWatermarkVisible);
                movingWatermarkStatus.Text = movingWatermarkVisible ? "Status: SHOWING • Saved" : "Status: HIDDEN • Saved";
                movingWatermarkStatus.ForeColor = movingWatermarkVisible ? Gold : Color.LightGreen;
            }

            private void LoadDmcaProtectedWatermarkSettings()
            {
                if (dmcaProtectedModel == null || dmcaProtectedStatus == null) return;
                string[] values = owner.MovingWatermarkSettingsValues();
                string model = values[1] ?? "";
                dmcaProtectedModel.Text = model.TrimStart('@');
                SelectWatermarkOpacity(dmcaProtectedOpacity, values.Length > 4 ? values[4] : "82");
                Boolean.TryParse(values[3], out movingWatermarkVisible);
                dmcaProtectedStatus.Text = "Protected watermark: " + (movingWatermarkVisible ? "SHOWING" : "HIDDEN") + " • @" + dmcaProtectedModel.Text + " • " + (values.Length > 4 ? values[4] : "82") + "%";
                dmcaProtectedStatus.ForeColor = movingWatermarkVisible ? Gold : Color.LightGreen;
            }

            private void SaveDmcaProtectedWatermark(bool enabled)
            {
                string model = SecretShowField(dmcaProtectedModel == null ? "" : dmcaProtectedModel.Text, "model_name").TrimStart('@');
                int opacity = WatermarkOpacityPercent(dmcaProtectedOpacity, 82);
                owner.SaveMovingWatermarkSettings(
                    "PROTECTED LIVE BROADCAST",
                    "@" + model,
                    "Unauthorized recording or redistribution is prohibited",
                    enabled,
                    opacity);
                movingWatermarkVisible = enabled;
                if (movingWatermarkTitle != null) movingWatermarkTitle.Text = "PROTECTED LIVE BROADCAST";
                if (movingWatermarkName != null) movingWatermarkName.Text = "@" + model;
                if (movingWatermarkTagline != null) movingWatermarkTagline.Text = "Unauthorized recording or redistribution is prohibited";
                SelectWatermarkOpacity(movingWatermarkOpacity, opacity.ToString());
                if (movingWatermarkStatus != null)
                {
                    movingWatermarkStatus.Text = enabled ? "Status: SHOWING • Saved" : "Status: HIDDEN • Saved";
                    movingWatermarkStatus.ForeColor = enabled ? Gold : Color.LightGreen;
                }
                LoadDmcaProtectedWatermarkSettings();
                SetEvent(enabled ? "Protected live broadcast watermark saved and shown" : "Protected live broadcast watermark saved and hidden");
            }

            private void SaveMovingWatermark(bool enabled)
            {
                int opacity = WatermarkOpacityPercent(movingWatermarkOpacity, 82);
                owner.SaveMovingWatermarkSettings(
                    SecretShowField(movingWatermarkTitle.Text, "PROTECTED LIVE BROADCAST"),
                    SecretShowField(movingWatermarkName.Text, "@model_name"),
                    SecretShowField(movingWatermarkTagline.Text, "Unauthorized recording or redistribution is prohibited"),
                    enabled,
                    opacity);
                movingWatermarkVisible = enabled;
                movingWatermarkStatus.Text = enabled ? "Status: SHOWING • Saved" : "Status: HIDDEN • Saved";
                movingWatermarkStatus.ForeColor = enabled ? Gold : Color.LightGreen;
                if (dmcaProtectedModel != null)
                {
                    dmcaProtectedModel.Text = movingWatermarkName.Text.TrimStart('@');
                    SelectWatermarkOpacity(dmcaProtectedOpacity, opacity.ToString());
                    LoadDmcaProtectedWatermarkSettings();
                }
                SetEvent(enabled ? "Moving watermark saved and shown" : "Moving watermark saved and hidden");
            }

            private static string SecretShowField(string value, string fallback)
            {
                string clean = (value ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ").Trim();
                if (clean.Length == 0) clean = fallback;
                return clean.Length > 180 ? clean.Substring(0, 180) : clean;
            }

            private void LoadSecretShowSettings()
            {
                secretShowTheme.SelectedIndex = 0; secretShowIntensity.SelectedItem = "Medium"; secretShowRotation.Checked = true;
                try
                {
                    string path = owner.StudioPath("my-secret-show-v4.txt"); if (!File.Exists(path)) return;
                    string[] values = File.ReadAllText(path).Split('\t'); decimal number;
                    if (values.Length > 0) secretShowTitle.Text = values[0]; if (values.Length > 1) secretShowHeadline.Text = values[1]; if (values.Length > 2) secretShowSubtitle.Text = values[2];
                    if (values.Length > 3 && Decimal.TryParse(values[3], out number)) secretShowPrice.Value = Math.Max(secretShowPrice.Minimum, Math.Min(secretShowPrice.Maximum, number));
                    if (values.Length > 4) secretShowWaiting.Text = values[4]; if (values.Length > 5) secretShowWelcome.Text = values[5];
                    if (values.Length > 6 && Decimal.TryParse(values[6], out number)) secretShowDuration.Value = Math.Max(secretShowDuration.Minimum, Math.Min(secretShowDuration.Maximum, number));
                    if (values.Length > 7 && secretShowTheme.Items.Contains(values[7])) secretShowTheme.SelectedItem = values[7];
                    if (values.Length > 8 && secretShowIntensity.Items.Contains(values[8])) secretShowIntensity.SelectedItem = values[8];
                    bool rotation; if (values.Length > 9 && Boolean.TryParse(values[9], out rotation)) secretShowRotation.Checked = rotation;
                    if (values.Length > 12 && Decimal.TryParse(values[12], out number)) secretShowTeaseDuration.Value = Math.Max(secretShowTeaseDuration.Minimum, Math.Min(secretShowTeaseDuration.Maximum, number));
                }
                catch { }
            }

            private void SaveSecretShowSettings(bool announce)
            {
                string[] values = new string[] {
                    SecretShowField(secretShowTitle.Text, "MY SECRET SHOW"), SecretShowField(secretShowHeadline.Text, "SECRET SHOW LOCKED"),
                    SecretShowField(secretShowSubtitle.Text, "Tip {price} Tokens to Unlock the Camera"), ((int)secretShowPrice.Value).ToString(),
                    SecretShowField(secretShowWaiting.Text, "Waiting for an approved tipper…"), SecretShowField(secretShowWelcome.Text, "Welcome to My Secret Show"),
                    ((int)secretShowDuration.Value).ToString(), secretShowTheme.Text, secretShowIntensity.Text, secretShowRotation.Checked.ToString(),
                    "100", "0", ((int)secretShowTeaseDuration.Value).ToString()
                };
                File.WriteAllText(owner.StudioPath("my-secret-show-v4.txt"), String.Join("\t", values), new UTF8Encoding(false));
                owner.SetEvolutionModule("my-secret-show", true);
                owner.server.Publish("module-action", "{\"id\":\"my-secret-show\",\"action\":\"settings\",\"settings\":" + owner.SecretShowSettingsJson() + "}");
                if (announce) { secretShowStatus.Text = "Status: SETTINGS APPLIED • Camera state unchanged"; secretShowStatus.ForeColor = Color.LightGreen; SetEvent("My Secret Show settings applied"); }
            }

            private void PublishSecretShowCommand(string action)
            {
                SaveSecretShowSettings(false);
                if (secretShowStatusTimer != null) { secretShowStatusTimer.Stop(); secretShowStatusTimer.Dispose(); secretShowStatusTimer = null; }
                owner.server.Publish("module-action", "{\"id\":\"my-secret-show\",\"action\":\"" + ControlDeckForm.Json(action) + "\"}");
                if (action == "tease")
                {
                    secretShowStatus.Text = "Status: TEASE VIEW • Auto-locking"; secretShowStatus.ForeColor = Gold;
                    secretShowStatusTimer = new System.Windows.Forms.Timer { Interval = ((int)secretShowTeaseDuration.Value * 1000) + 400 };
                    secretShowStatusTimer.Tick += delegate { secretShowStatusTimer.Stop(); secretShowStatus.Text = "Status: LOCKED • Tease complete"; secretShowStatus.ForeColor = Color.LightGreen; };
                    secretShowStatusTimer.Start();
                }
                else if (action == "unlock" || action == "test")
                {
                    secretShowStatus.Text = action == "test" ? "Status: TESTING • Camera remains covered" : "Status: UNLOCKING"; secretShowStatus.ForeColor = Gold;
                    secretShowStatusTimer = new System.Windows.Forms.Timer { Interval = ((int)secretShowDuration.Value * 1000) + 700 };
                    bool testing = action == "test";
                    secretShowStatusTimer.Tick += delegate { secretShowStatusTimer.Stop(); secretShowStatus.Text = testing ? "Status: LOCKED • Test complete" : "Status: UNLOCKED • Camera visible"; secretShowStatus.ForeColor = testing ? Color.LightGreen : Color.Gold; };
                    secretShowStatusTimer.Start();
                }
                else { secretShowStatus.Text = "Status: LOCKED • Camera cover active"; secretShowStatus.ForeColor = Color.LightGreen; }
                SetEvent("My Secret Show: " + action);
            }

            private GroupBox BuildGameResultSettingsGroup()
            {
                var group = Group("GAME RESULT DISPLAY MANAGER", 15, 605, 915, 205);
                group.Controls.Add(LabelAt("Result display (seconds)", 18, 36)); resultDuration = NumberAt(20, 2, 120, 18, 58, 145, 0); group.Controls.Add(resultDuration);
                group.Controls.Add(LabelAt("Fade duration", 180, 36)); resultFade = NumberAt(2, 1, 10, 180, 58, 120, 0); group.Controls.Add(resultFade);
                group.Controls.Add(LabelAt("Animation", 320, 36)); resultAnimation = Combo(new string[] { "Slide", "Fade", "Zoom" }, 320, 58, 145); group.Controls.Add(resultAnimation);
                resultUsername = new CheckBox { Text = "Show entered viewer", Location = new Point(490, 58), AutoSize = true, ForeColor = TextColor, Checked = true }; group.Controls.Add(resultUsername);
                resultPrize = new CheckBox { Text = "Show prize", Location = new Point(620, 58), AutoSize = true, ForeColor = TextColor, Checked = true }; group.Controls.Add(resultPrize);
                resultValue = new CheckBox { Text = "Show result", Location = new Point(730, 58), AutoSize = true, ForeColor = TextColor, Checked = true }; group.Controls.Add(resultValue);
                group.Controls.Add(ButtonAt("SAVE RESULT DISPLAY", 690, 103, 190, delegate { SaveGameResultSettings(); }));
                group.Controls.Add(LabelAt("Exact: a specific number is required.  Range: any total inside the selected range succeeds.", 18, 113));
                group.Controls.Add(LabelAt("A viewer name appears only when the optional Viewer field contains one. Prize starts ON; Result can be changed.", 18, 150));
                return group;
            }

            private sealed class GameModuleChoice
            {
                internal string Id;
                internal string Name;
                internal GameModuleChoice(string id, string name) { Id = id; Name = name; }
                public override string ToString() { return Name + "  [" + Id + "]"; }
            }

            private GroupBox BuildHtmlGameModulesGroup()
            {
                var group = Group("CUSTOM HTML DICE + GAME MODULES • 1920 x 1080 MAXIMUM", 15, 830, 915, 235);
                group.Controls.Add(LabelAt("Import a self-contained HTML dice or custom game. Wheel scripts are managed above in the Multiple Wheel Engine.", 18, 32));
                group.Controls.Add(LabelAt("Available game modules", 18, 62));
                htmlGameSelector = Combo(new string[] { }, 18, 84, 500); group.Controls.Add(htmlGameSelector);
                group.Controls.Add(ButtonAt("RUN SELECTED", 535, 81, 170, delegate { RunSelectedHtmlGame(); }));
                group.Controls.Add(ButtonAt("REFRESH", 715, 81, 150, delegate { RefreshHtmlGameModules(); }));
                group.Controls.Add(LabelAt("New module name", 18, 130));
                htmlGameName = Box("My Custom Dice Game", 18, 152, 300); group.Controls.Add(htmlGameName);
                group.Controls.Add(LabelAt("Module kind", 338, 130));
                htmlGameKind = Combo(new string[] { "Dice", "Custom" }, 338, 152, 140); group.Controls.Add(htmlGameKind);
                group.Controls.Add(ButtonAt("IMPORT HTML OVERLAY", 500, 149, 205, delegate { ImportHtmlGame(); }));
                group.Controls.Add(LabelAt("No platform API is required. The selected HTML reloads from the beginning each time you click Run.", 18, 198));
                RefreshHtmlGameModules();
                return group;
            }

            private void RefreshHtmlGameModules()
            {
                if (htmlGameSelector == null) return;
                string selectedId = "";
                var selected = htmlGameSelector.SelectedItem as GameModuleChoice;
                if (selected != null) selectedId = selected.Id;
                htmlGameSelector.Items.Clear();
                foreach (string[] row in owner.EvolutionModuleCatalog())
                {
                    if (row.Length < 3 || !String.Equals(row[2], "GAME", StringComparison.OrdinalIgnoreCase)) continue;
                    if (row.Length > 5 && String.Equals(row[5], "WHEEL", StringComparison.OrdinalIgnoreCase)) continue;
                    var choice = new GameModuleChoice(row[0], row[1]);
                    htmlGameSelector.Items.Add(choice);
                    if (String.Equals(choice.Id, selectedId, StringComparison.OrdinalIgnoreCase)) htmlGameSelector.SelectedItem = choice;
                }
                if (htmlGameSelector.SelectedIndex < 0 && htmlGameSelector.Items.Count > 0) htmlGameSelector.SelectedIndex = 0;
            }

            private void ImportHtmlGame()
            {
                string installedId = owner.InstallHtmlGameModule(htmlGameKind.Text, htmlGameName.Text);
                if (installedId.Length == 0) return;
                RefreshHtmlGameModules();
                foreach (object item in htmlGameSelector.Items)
                {
                    var choice = item as GameModuleChoice;
                    if (choice != null && String.Equals(choice.Id, installedId, StringComparison.OrdinalIgnoreCase)) { htmlGameSelector.SelectedItem = choice; break; }
                }
                SetEvent("HTML game module imported");
            }

            private void RunSelectedHtmlGame()
            {
                var choice = htmlGameSelector == null ? null : htmlGameSelector.SelectedItem as GameModuleChoice;
                if (choice == null) { MessageBox.Show("Choose a game module first."); return; }
                owner.SetEvolutionModule(choice.Id, true);
                owner.server.Publish("module-action", "{\"id\":\"" + ControlDeckForm.Json(choice.Id) + "\",\"action\":\"run\",\"duration\":30,\"name\":\"" + ControlDeckForm.Json(choice.Name) + "\"}");
                currentGame.Text = "Current Game: " + choice.Name;
                SetEvent("Manual HTML game started");
                wheelCount++;
                RefreshStats();
            }

            private GroupBox BuildTemplateGroup(int top = 505)
            {
                var group = Group("LAYOUT LIBRARY • JSON TEMPLATES + FULL PACKS", 15, top, 915, 570);

                // Header / identity
                group.Controls.Add(LabelAt("Template / package name", 18, 30));
                templateName = Box("My Creator Layout", 18, 52, 260); group.Controls.Add(templateName);

                group.Controls.Add(LabelAt("Saved JSON templates", 295, 30));
                templateSelector = Combo(new string[] { }, 295, 52, 270); group.Controls.Add(templateSelector);
                templateSelector.SelectedIndexChanged += delegate { LoadTemplateEditor(); };

                // Legacy JSON section
                var jsonBox = Group("JSON TEMPLATE • SINGLE CREATOR-CAM ZONE LAYOUT", 18, 95, 860, 245);
                group.Controls.Add(jsonBox);

                templateEditor = MultiBox(DefaultTemplateJson(), 15, 28, 825, 135);
                jsonBox.Controls.Add(templateEditor);

                jsonBox.Controls.Add(ButtonAt("SAVE JSON", 15, 175, 135, delegate { SaveTemplate(); }));
                jsonBox.Controls.Add(ButtonAt("LOAD JSON", 160, 175, 135, delegate { PublishTemplate(); }));
                jsonBox.Controls.Add(ButtonAt("DUPLICATE", 305, 175, 125, delegate { DuplicateTemplate(); }));
                jsonBox.Controls.Add(ButtonAt("IMPORT JSON TEMPLATE", 440, 175, 175, delegate { ImportTemplate(); }));
                jsonBox.Controls.Add(ButtonAt("EXPORT JSON TEMPLATE", 625, 175, 175, delegate { ExportTemplate(); }));

                // Full pack section
                var packBox = Group("FULL LAYOUT PACK • ALL 12 MOVABLE 1920 × 1080 ELEMENTS", 18, 355, 860, 145);
                group.Controls.Add(packBox);

                packBox.Controls.Add(ButtonAt("IMPORT LAYOUT PACK ZIP", 15, 42, 225, delegate { ImportLayoutPackZip(); }));
                packBox.Controls.Add(ButtonAt("EXPORT CURRENT LAYOUT PACK", 250, 42, 235, delegate { ExportCurrentLayoutPackZip(); }));
                packBox.Controls.Add(ButtonAt("RESTORE LAST LAYOUT", 495, 42, 190, delegate { RestorePreviousLayoutPack(); }));

                var packHelp = LabelAt(
                    "Use this for Football, Halloween, Couple Show, or any complete studio position pack. " +
                    "It swaps Brand, Camera, Goal, Tippers, Last Tipper, Recent, Ticker, Alert, Game Zone, VIP, DMCA, and Background together.",
                    15, 88);
                packHelp.AutoSize = false; packHelp.Size = new Size(815, 42); packBox.Controls.Add(packHelp);

                var footer = LabelAt(
                    "Tip: JSON buttons only handle old Creator Cam zone templates. Layout Pack buttons only handle StimTake full-layout ZIP packages.",
                    18, 520);
                footer.AutoSize = false; footer.Size = new Size(850, 32); footer.ForeColor = Color.LightGreen; group.Controls.Add(footer);

                RefreshTemplateList();
                return group;
            }

            private TabPage BuildConnectorTab()
            {
                var page = Page("CONNECTORS");

                var model = Group("MY CHATURBATE MODEL", 15, 15, 915, 285);
                page.Controls.Add(model);

                model.Controls.Add(LabelAt("Model address", 18, 35));
                chromeBridgeModelAddress = Box("", 18, 57, 620);
                model.Controls.Add(chromeBridgeModelAddress);

                model.Controls.Add(ButtonAt("SAVE MODEL", 655, 54, 120, delegate { SaveChromeBridgeModel(); }));
                model.Controls.Add(ButtonAt("DELETE MODEL", 785, 54, 110, delegate { DeleteChromeBridgeModel(); }));

                var example = LabelAt("Example: https://chaturbate.com/obsidian_stallion/", 18, 90);
                example.ForeColor = Color.Silver;
                example.Size = new Size(845, 24);
                model.Controls.Add(example);

                chromeBridgeModelStatus = LabelAt("Model: NOT SAVED", 18, 125);
                chromeBridgeModelStatus.ForeColor = Color.LightGreen;
                chromeBridgeModelStatus.Size = new Size(845, 26);
                model.Controls.Add(chromeBridgeModelStatus);

                chromeBridgeStatus = LabelAt("Bridge: WAITING FOR TIP", 18, 158);
                chromeBridgeStatus.ForeColor = Color.LightGreen;
                chromeBridgeStatus.Size = new Size(845, 26);
                model.Controls.Add(chromeBridgeStatus);

                chromeBridgeRoom = LabelAt("Room: Waiting", 18, 190);
                chromeBridgeRoom.Size = new Size(410, 26);
                model.Controls.Add(chromeBridgeRoom);

                chromeBridgeCount = LabelAt("Tips received: 0", 450, 190);
                chromeBridgeCount.Size = new Size(395, 26);
                model.Controls.Add(chromeBridgeCount);

                chromeBridgeLastTip = LabelAt("Last tip: None", 18, 222);
                chromeBridgeLastTip.Size = new Size(827, 26);
                model.Controls.Add(chromeBridgeLastTip);

                var note = LabelAt(
                    "Save your Chaturbate room once. To use a different model later, delete this model and save the new room.",
                    18, 252);
                note.AutoSize = false;
                note.Size = new Size(845, 28);
                model.Controls.Add(note);

                LoadChromeBridgeModel();

                owner.server.EventPublished += ConnectorEventPublished;
                Disposed += delegate {
                    owner.server.EventPublished -= ConnectorEventPublished;
                    StopChaturbateConnector();
                };
                return page;
            }

            private string ChromeBridgeModelPath()
            {
                return owner.StudioPath("chaturbate-model-address-v1.txt");
            }

            private string ChromeBridgeModelName(string address)
            {
                if (String.IsNullOrWhiteSpace(address)) return "";

                Uri uri;
                if (!Uri.TryCreate(address.Trim(), UriKind.Absolute, out uri)) return "";
                if (!String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return "";

                string host = (uri.Host ?? "").Trim().ToLowerInvariant();
                if (host != "chaturbate.com" && host != "www.chaturbate.com") return "";

                string path = (uri.AbsolutePath ?? "").Trim('/');
                if (path.Length == 0 || path.Contains("/")) return "";
                if (!Regex.IsMatch(path, "^[A-Za-z0-9_]+$")) return "";

                return path;
            }

            private string NormalizeChromeBridgeModelAddress(string address)
            {
                string model = ChromeBridgeModelName(address);
                if (model.Length == 0) return "";
                return "https://chaturbate.com/" + model + "/";
            }

            private void LoadChromeBridgeModel()
            {
                if (chromeBridgeModelAddress == null || chromeBridgeModelStatus == null) return;

                try
                {
                    string path = ChromeBridgeModelPath();
                    if (!File.Exists(path))
                    {
                        chromeBridgeModelAddress.Text = "";
                        chromeBridgeModelAddress.ReadOnly = false;
                        chromeBridgeModelStatus.Text = "Model: NOT SAVED";
                        return;
                    }

                    string saved = NormalizeChromeBridgeModelAddress(File.ReadAllText(path, Encoding.UTF8).Trim());
                    if (saved.Length == 0)
                    {
                        chromeBridgeModelAddress.Text = "";
                        chromeBridgeModelAddress.ReadOnly = false;
                        chromeBridgeModelStatus.Text = "Model: SAVED ADDRESS NEEDS ATTENTION";
                        return;
                    }

                    chromeBridgeModelAddress.Text = saved;
                    chromeBridgeModelAddress.ReadOnly = true;
                    chromeBridgeModelStatus.Text = "Model: " + ChromeBridgeModelName(saved) + " • SAVED";
                }
                catch
                {
                    chromeBridgeModelAddress.ReadOnly = false;
                    chromeBridgeModelStatus.Text = "Model: COULD NOT LOAD SAVED ADDRESS";
                }
            }

            private void SaveChromeBridgeModel()
            {
                if (chromeBridgeModelAddress == null) return;

                string normalized = NormalizeChromeBridgeModelAddress(chromeBridgeModelAddress.Text);
                if (normalized.Length == 0)
                {
                    MessageBox.Show(
                        "Enter a Chaturbate model address like:\r\nhttps://chaturbate.com/obsidian_stallion/",
                        "StimTake Studio",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    File.WriteAllText(ChromeBridgeModelPath(), normalized, new UTF8Encoding(false));
                    chromeBridgeModelAddress.Text = normalized;
                    chromeBridgeModelAddress.ReadOnly = true;
                    chromeBridgeModelStatus.Text = "Model: " + ChromeBridgeModelName(normalized) + " • SAVED";
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not save the model address.\r\n\r\n" + ex.Message, "StimTake Studio");
                }
            }

            private void DeleteChromeBridgeModel()
            {
                if (chromeBridgeModelAddress == null) return;

                if (MessageBox.Show(
                    "Delete the saved Chaturbate model connection?\r\n\r\nYou can enter a different model after it is deleted.",
                    "StimTake Studio",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                try
                {
                    string path = ChromeBridgeModelPath();
                    if (File.Exists(path)) File.Delete(path);
                    chromeBridgeModelAddress.ReadOnly = false;
                    chromeBridgeModelAddress.Text = "";
                    chromeBridgeModelStatus.Text = "Model: NOT SAVED";
                    chromeBridgeModelAddress.Focus();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not delete the saved model address.\r\n\r\n" + ex.Message, "StimTake Studio");
                }
            }


            private string ChaturbateBridgeScript()
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Connectors", "StimTakeChaturbateBridge", "stimtake_chaturbate_bridge.py");
            }

            private void LoadChaturbateConnectorSettings()
            {
                try
                {
                    string usernamePath = owner.StudioPath("chaturbate-username-v1.txt");
                    if (File.Exists(usernamePath) && connectorUsername != null)
                        connectorUsername.Text = File.ReadAllText(usernamePath, Encoding.UTF8).Trim();

                    string settingsPath = owner.StudioPath("chaturbate-bridge-settings-v1.txt");
                    if (File.Exists(settingsPath))
                    {
                        string[] values = File.ReadAllText(settingsPath, Encoding.UTF8).Split('\t');
                        decimal number;
                        bool flag;
                        if (values.Length > 0 && Decimal.TryParse(values[0], out number))
                            connectorDiceMinimum.Value = Math.Max(connectorDiceMinimum.Minimum, Math.Min(connectorDiceMinimum.Maximum, number));
                        if (values.Length > 1 && Decimal.TryParse(values[1], out number))
                            connectorWheelMinimum.Value = Math.Max(connectorWheelMinimum.Minimum, Math.Min(connectorWheelMinimum.Maximum, number));
                        if (values.Length > 2 && Boolean.TryParse(values[2], out flag))
                            connectorAutoGameRequests.Checked = flag;
                        if (values.Length > 3 && connectorEnvironment != null)
                            connectorEnvironment.SelectedIndex = String.Equals(values[3], "TESTBED", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                    }
                }
                catch { }
            }

            private void SaveChaturbateConnectorSettings()
            {
                try
                {
                    string username = (connectorUsername.Text ?? "").Trim();
                    File.WriteAllText(owner.StudioPath("chaturbate-username-v1.txt"), username, new UTF8Encoding(false));
                    File.WriteAllText(
                        owner.StudioPath("chaturbate-bridge-settings-v1.txt"),
                        connectorDiceMinimum.Value + "\t" + connectorWheelMinimum.Value + "\t" + connectorAutoGameRequests.Checked + "\t" +
                        (connectorEnvironment != null && connectorEnvironment.SelectedIndex == 1 ? "TESTBED" : "LIVE"),
                        new UTF8Encoding(false));
                }
                catch { }
            }

            private void StartChaturbateConnector()
            {
                string username = (connectorUsername.Text ?? "").Trim();
                string token = connectorToken.Text ?? "";
                string script = ChaturbateBridgeScript();

                if (username.Length == 0)
                {
                    MessageBox.Show("Enter this model's Chaturbate username.");
                    return;
                }
                if (token.Trim().Length == 0)
                {
                    MessageBox.Show("Enter this model's Chaturbate Events API token. StimTake will not save it.");
                    return;
                }
                if (!File.Exists(script))
                {
                    MessageBox.Show("StimTake Chaturbate Bridge was not found:\r\n\r\n" + script);
                    return;
                }

                StopChaturbateConnector();
                SaveChaturbateConnectorSettings();

                try
                {
                    var info = new ProcessStartInfo();
                    info.FileName = "py";
                    info.Arguments =
                        "-3 \"" + script + "\"" +
                        " --username \"" + username.Replace("\"", "") + "\"" +
                        " --endpoint \"http://127.0.0.1:8787/api/platform-event\"" +
                        " --request-mode " + (connectorAutoGameRequests.Checked ? "trigger" : "detect") +
                        " --dice-min " + ((int)connectorDiceMinimum.Value) +
                        " --wheel-min " + ((int)connectorWheelMinimum.Value) +
                        (connectorEnvironment != null && connectorEnvironment.SelectedIndex == 1 ? " --testbed" : "");
                    info.WorkingDirectory = Path.GetDirectoryName(script);
                    info.UseShellExecute = false;
                    info.CreateNoWindow = true;
                    info.EnvironmentVariables["STIMTAKE_CB_USERNAME"] = username;
                    info.EnvironmentVariables["STIMTAKE_CB_TOKEN"] = token;

                    connectorProcess = Process.Start(info);
                    connectorToken.Clear();

                    if (connectorProcess == null) throw new InvalidOperationException("Python connector process did not start.");
                    connectorProcess.EnableRaisingEvents = true;
                    connectorProcess.Exited += delegate {
                        if (IsDisposed || !IsHandleCreated) return;
                        try
                        {
                            BeginInvoke((MethodInvoker)delegate {
                                if (connectorStatus != null) connectorStatus.Text = "Connection Status: STOPPED";
                            });
                        }
                        catch { }
                    };

                    connectorStatus.Text = "Connection Status: RUNNING • " +
                        (connectorEnvironment != null && connectorEnvironment.SelectedIndex == 1 ? "TESTBED" : "LIVE") +
                        " • waiting for Chaturbate tips";
                }
                catch (Exception error)
                {
                    connectorToken.Clear();
                    connectorProcess = null;
                    MessageBox.Show(
                        "Chaturbate connector could not start.\r\n\r\n" +
                        error.Message +
                        "\r\n\r\nPython must be available through the Windows 'py' launcher.",
                        "StimTake Chaturbate Bridge",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    connectorStatus.Text = "Connection Status: START FAILED";
                }
            }

            private void StopChaturbateConnector()
            {
                try
                {
                    if (connectorProcess != null && !connectorProcess.HasExited)
                    {
                        connectorProcess.Kill();
                        connectorProcess.WaitForExit(2000);
                    }
                }
                catch { }
                finally
                {
                    if (connectorProcess != null)
                    {
                        try { connectorProcess.Dispose(); } catch { }
                    }
                    connectorProcess = null;
                    if (connectorStatus != null) connectorStatus.Text = "Connection Status: STOPPED";
                }
            }

            private static string ConnectorJsonString(string json, string field)
            {
                if (String.IsNullOrWhiteSpace(json) || String.IsNullOrWhiteSpace(field)) return "";
                Match match = Regex.Match(
                    json,
                    "\"" + Regex.Escape(field) + "\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"",
                    RegexOptions.IgnoreCase);
                if (!match.Success) return "";
                string value = match.Groups["value"].Value;
                try { value = Regex.Unescape(value); } catch { }
                return (value ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            }

            private static long ConnectorJsonLong(string json, string field)
            {
                if (String.IsNullOrWhiteSpace(json) || String.IsNullOrWhiteSpace(field)) return 0;
                Match match = Regex.Match(
                    json,
                    "\"" + Regex.Escape(field) + "\"\\s*:\\s*(?<value>[0-9]+)",
                    RegexOptions.IgnoreCase);
                long value;
                return match.Success && Int64.TryParse(match.Groups["value"].Value, out value) ? value : 0;
            }

            private void ConnectorEventPublished(string type, string payload)
            {
                if (!String.Equals(type, "platform-event", StringComparison.OrdinalIgnoreCase)) return;
                if (IsDisposed || !IsHandleCreated) return;

                string source = ConnectorJsonString(payload, "source");
                string eventType = ConnectorJsonString(payload, "type");
                string username = ConnectorJsonString(payload, "username");
                string room = ConnectorJsonString(payload, "room");
                long amount = ConnectorJsonLong(payload, "amount");

                BeginInvoke((MethodInvoker)delegate
                {
                    connectorEvents++;
                    if (connectorStatus != null) connectorStatus.Text = "Connection Status: EVENT RECEIVED";
                    if (connectorLastEvent != null) connectorLastEvent.Text = "Last Event: " + (payload.Length > 100 ? payload.Substring(0, 100) + "..." : payload);
                    if (connectorCount != null) connectorCount.Text = "Events Received: " + connectorEvents;

                    if (String.Equals(source, "chaturbate-browser", StringComparison.OrdinalIgnoreCase) &&
                        String.Equals(eventType, "tip", StringComparison.OrdinalIgnoreCase) &&
                        username.Length > 0 && amount > 0)
                    {
                        chromeBridgeEvents++;
                        chromeBridgeStatus.Text = "Bridge: RECEIVING";
                        chromeBridgeRoom.Text = "Room: " + (room.Length > 0 ? room : "Unknown");
                        chromeBridgeCount.Text = "Tips received: " + chromeBridgeEvents;
                        chromeBridgeLastTip.Text = "Last tip: " + username + " • " + amount + (amount == 1 ? " token" : " tokens");
                    }
                });
            }

            private void SelectTab(string name)
            {
                foreach (TabPage page in mainTabs.TabPages) if (page.Text == name) { mainTabs.SelectedTab = page; break; }
            }

            private string ModuleKey(string display)
            {
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                    { "Brand Panel", "brand" }, { "Camera Frame", "camera" }, { "Token Goal", "goal" },
                    { "Top Tippers / Fans", "supporters" }, { "Last Tipper", "last-tipper" }, { "Recent Supporter", "recent" }, { "Tip Ticker", "ticker" },
                    { "Alert Display", "alert" }, { "Game Overlay Zone", "game-zone" }, { "VIP Badge", "vip" },
                    { "DMCA Watermark", "dmca" }, { "Background", "background" }
                };
                return map.ContainsKey(display) ? map[display] : display.ToLowerInvariant();
            }

            private static string[] IncludedActionLines()
            {
                return new string[] {
                    "1|Black Cat|action-1-black-cat|run|zoom|12",
                    "2|Halloween Sides|action-2-halloween-sides|run|fade",
                    "3|Flying Witch|action-3-flying-witch|run|slide",
                    "4|Warlock|action-4-warlock|run|zoom",
                    "5|Pumpkin|action-5-pumpkin|run|zoom",
                    "6|Ghost|action-6-ghost|run|fade",
                    "7|Empty||run|fade", "8|Empty||run|fade", "9|Empty||run|fade",
                    "10|Empty||run|fade", "11|Empty||run|fade", "12|Empty||run|fade"
                };
            }

            private int SelectedActionSlot()
            {
                int slot;
                if (actionSlot == null || !Int32.TryParse(actionSlot.Text, out slot)) slot = 1;
                return Math.Max(1, Math.Min(20, slot));
            }

            private string[] SelectedActionSlotValues()
            {
                int selectedSlot = SelectedActionSlot();
                foreach (string line in owner.ActionSlotLines())
                {
                    string[] values = line.Split('|'); int slot;
                    if (values.Length >= 5 && Int32.TryParse(values[0], out slot) && slot == selectedSlot) return values;
                }
                return new string[] { selectedSlot.ToString(), "Empty", "", "run", "fade" };
            }

            private void RefreshActionSlotEditor()
            {
                if (actionSlotStatus == null) return;
                string[] values = SelectedActionSlotValues();
                if (values[2].Length == 0)
                {
                    actionSlotName.Text = "";
                    actionSlotStatus.Text = "Slot " + values[0] + ": EMPTY • Click BROWSE / REPLACE HTML to choose an overlay file.";
                }
                else
                {
                    actionSlotName.Text = values[1];
                    actionSlotStatus.Text = "Slot " + values[0] + ": " + values[1] + " • OFF until you click its Action button";
                }
            }

            private void BrowseActionSlotHtml()
            {
                int slot = SelectedActionSlot();
                if (!owner.InstallActionSlotHtml(slot, actionSlotName.Text)) return;
                RefreshActionButtons();
                RefreshActionSlotEditor();
                SetEvent("Action " + slot + " HTML imported and assigned");
            }

            private GameModuleChoice SelectedActionScript()
            {
                return actionScriptSelector == null ? null : actionScriptSelector.SelectedItem as GameModuleChoice;
            }

            private void RefreshActionScripts()
            {
                if (actionScriptSelector == null) return;
                string selectedLibraryId = "";
                var selected = SelectedActionScript();
                if (selected != null) selectedLibraryId = selected.Id;
                List<string[]> scripts = owner.ActionScriptCatalog();
                actionScriptSelector.Items.Clear();
                foreach (string[] row in scripts)
                {
                    var choice = new GameModuleChoice(row[0], row[1]);
                    actionScriptSelector.Items.Add(choice);
                    if (String.Equals(choice.Id, selectedLibraryId, StringComparison.OrdinalIgnoreCase)) actionScriptSelector.SelectedItem = choice;
                }
                if (actionScriptSelector.SelectedIndex < 0 && actionScriptSelector.Items.Count > 0) actionScriptSelector.SelectedIndex = 0;
                if (actionScriptStatus != null) actionScriptStatus.Text = "Backups: backupscripts_action  •  " + scripts.Count + "/20  •  Full canvas: 1920 x 1080";
            }

            private void UploadActionScript()
            {
                string id = owner.InstallHtmlActionModule(actionScriptName.Text);
                if (id.Length == 0) return;
                RefreshActionScripts();
                foreach (object item in actionScriptSelector.Items)
                {
                    var choice = item as GameModuleChoice;
                    if (choice != null && String.Equals(choice.Id, id, StringComparison.OrdinalIgnoreCase)) { actionScriptSelector.SelectedItem = choice; break; }
                }
                actionScriptStatus.Text = "Uploaded and backed up: " + id;
                SetEvent("Action HTML uploaded");
            }

            private void DeleteSelectedActionScript()
            {
                var choice = SelectedActionScript();
                if (choice == null) { MessageBox.Show("Choose an Action HTML script first."); return; }
                if (!owner.DeleteActionScript(choice.Id, this)) return;
                RefreshActionScripts();
                actionScriptStatus.Text = "Action script deleted. Reassign any slot that used it.";
                SetEvent("Action HTML deleted");
            }

            private void RunSelectedActionScript()
            {
                var choice = SelectedActionScript();
                if (choice == null) { MessageBox.Show("Choose an Action HTML script first."); return; }
                if (!owner.LoadActionScript(choice.Id)) { MessageBox.Show("The selected Action backup could not be loaded."); return; }
                actionScriptStatus.Text = "Starting: " + choice.Name;
                var launchTimer = new Timer { Interval = 900 };
                launchTimer.Tick += delegate
                {
                    launchTimer.Stop();
                    launchTimer.Dispose();
                    owner.server.Publish("module-action", "{\"id\":\"" + ControlDeckForm.Json(choice.Id) + "\",\"action\":\"run\",\"duration\":30,\"name\":\"" + ControlDeckForm.Json(choice.Name) + "\"}");
                    actionScriptStatus.Text = "Running: " + choice.Name;
                };
                launchTimer.Start();
                SetEvent("Manual Action HTML started");
            }

            private void AssignSelectedActionScript()
            {
                var choice = SelectedActionScript();
                if (choice == null) { MessageBox.Show("Choose an Action HTML script first."); return; }
                int slot;
                if (actionSlot == null || !Int32.TryParse(actionSlot.Text, out slot)) slot = 1;
                if (!owner.LoadActionScript(choice.Id)) { MessageBox.Show("The selected Action backup could not be loaded."); return; }
                var lines = new List<string>(ActionLines());
                lines.RemoveAll(delegate(string line) { return line.StartsWith(slot + "|", StringComparison.Ordinal); });
                lines.Add(slot + "|" + CleanField(choice.Name) + "|" + CleanField(choice.Id) + "|run|fade");
                lines.Sort(delegate(string a, string b)
                {
                    int aSlot = 0, bSlot = 0;
                    Int32.TryParse(a.Split('|')[0], out aSlot);
                    Int32.TryParse(b.Split('|')[0], out bSlot);
                    return aSlot.CompareTo(bSlot);
                });
                File.WriteAllLines(owner.StudioPath("actions-v3.txt"), lines.ToArray());
                owner.server.Publish("action-config", "{\"actions\":[" + owner.ActionDefinitionsJson() + "]}");
                RefreshActionButtons();
                actionScriptStatus.Text = "Assigned " + choice.Name + " to Action " + slot + ".";
                SetEvent("Action " + slot + " assignment updated");
            }

            private string[] ActionLines()
            {
                return owner.ActionSlotLines();
            }

            private void LoadIncludedActions()
            {
                string path = owner.StudioPath("actions-v3.txt");
                if (File.Exists(path))
                {
                    if (MessageBox.Show("Load the included Action 1-6 set and leave 7-12 empty? Your current assignments will be backed up first.", "Creator Cam Studio", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    string backup = owner.StudioPath("actions-v3-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                    File.Copy(path, backup, true);
                }
                File.WriteAllLines(path, IncludedActionLines());
                owner.server.Publish("action-config", "{\"actions\":[" + owner.ActionDefinitionsJson() + "]}");
                RefreshActionButtons();
                SetEvent("Included Action 1-6 set loaded");
            }

            private void ClearSelectedActionSlot()
            {
                int slot = SelectedActionSlot();
                string[] values = SelectedActionSlotValues();
                if (values[2].Length == 0) { actionSlotStatus.Text = "Slot " + slot + " is already empty."; return; }
                if (MessageBox.Show("Clear Action " + slot + " and delete its managed HTML backup?\r\n\r\nYour original HTML file is not changed.", "Creator Cam Studio", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                owner.DeleteActionSlotHtml(slot);
                RefreshActionButtons();
                RefreshActionSlotEditor();
                SetEvent("Action " + slot + " cleared");
            }

            private void RefreshActionButtons()
            {
                for (int index = 0; index < actionButtons.Length; index++)
                {
                    if (actionButtons[index] == null) continue;
                    actionButtons[index].BackColor = Color.FromArgb(44, 47, 52);
                    actionButtons[index].ForeColor = Color.Gainsboro;
                    actionToolTip.SetToolTip(actionButtons[index], "Action " + (index + 1) + " • Empty HTML slot");
                }
                foreach (string line in ActionLines())
                {
                    string[] value = line.Split('|'); int slot;
                    if (value.Length < 5 || !Int32.TryParse(value[0], out slot) || slot < 1 || slot > 20 || actionButtons[slot - 1] == null) continue;
                    bool assigned = value[2].Length > 0;
                    bool running = assigned && activeActionSlots.Contains(slot);
                    actionButtons[slot - 1].BackColor = running ? Color.FromArgb(28, 118, 92) : Color.FromArgb(44, 47, 52);
                    actionButtons[slot - 1].ForeColor = Color.White;
                    actionToolTip.SetToolTip(actionButtons[slot - 1], assigned ? value[1] + (running ? " • ON • Click to turn OFF" : " • OFF • Click to turn ON") : "Action " + slot + " • Empty HTML slot");
                }
            }

            private void TriggerAction(int slot)
            {
                if (activeActionSlots.Contains(slot))
                {
                    StopAction(slot);
                    return;
                }
                foreach (string line in ActionLines())
                {
                    string[] value = line.Split('|'); int current;
                    if (value.Length < 5 || !Int32.TryParse(value[0], out current) || current != slot) continue;
                    if (value[2].Length == 0) { MessageBox.Show("Action " + slot + " is empty. Select the slot and click BROWSE / REPLACE HTML first."); return; }
                    if (!String.Equals(value[2], ControlDeckForm.ActionSlotId(slot), StringComparison.OrdinalIgnoreCase)) { MessageBox.Show("Action " + slot + " has an invalid assignment. Browse to the HTML again to repair this slot."); return; }
                    if (!owner.LoadActionSlotHtml(slot)) { MessageBox.Show("The managed HTML backup for Action " + slot + " could not be loaded. Browse to the original HTML again to repair it."); return; }
                    string definition = "{\"slot\":" + slot + ",\"name\":\"" + ControlDeckForm.Json(value[1]) + "\",\"module\":\"" + ControlDeckForm.Json(value[2]) + "\",\"action\":\"" + ControlDeckForm.Json(value[3]) + "\",\"animation\":\"" + ControlDeckForm.Json(value[4]) + "\"}";
                    var launchTimer = new Timer { Interval = 250 };
                    launchTimer.Tick += delegate
                    {
                        launchTimer.Stop();
                        launchTimer.Dispose();
                        owner.server.Publish("action-trigger", "{\"slot\":" + slot + ",\"definition\":" + definition + "}");
                        activeActionSlots.Add(slot);
                        RefreshActionButtons();
                        SetEvent("Action " + slot + " ON: " + value[1]);
                    };
                    launchTimer.Start();
                    return;
                }
            }

            private void StopAction(int slot)
            {
                string id = ControlDeckForm.ActionSlotId(slot);
                owner.server.Publish("module-action", "{\"id\":\"" + ControlDeckForm.Json(id) + "\",\"action\":\"stop\",\"name\":\"Action " + slot + "\"}");
                activeActionSlots.Remove(slot);
                RefreshActionButtons();
                SetEvent("Action " + slot + " OFF");
            }

            private void StopAllActions()
            {
                for (int slot = 1; slot <= 20; slot++)
                {
                    string id = ControlDeckForm.ActionSlotId(slot);
                    owner.server.Publish("module-action", "{\"id\":\"" + ControlDeckForm.Json(id) + "\",\"action\":\"stop\",\"name\":\"Action " + slot + "\"}");
                }
                activeActionSlots.Clear();
                RefreshActionButtons();
                SetEvent("All Action overlays OFF");
            }

            private static string CleanField(string value) { return (value ?? "").Replace("|", " ").Replace("\r", " ").Replace("\n", " ").Trim(); }

            private void RefreshModuleLibrary()
            {
                if (moduleGrid == null) return;
                moduleGrid.Rows.Clear();
                foreach (string[] row in owner.EvolutionModuleCatalog())
                {
                    if (row.Length < 5 || !String.Equals(row[2], "THEME", StringComparison.OrdinalIgnoreCase)) continue;
                    moduleGrid.Rows.Add(row[0], row[1], "UI SKIN", row[3], row[4]);
                }
            }

            private string SelectedModuleId()
            {
                if (moduleGrid.SelectedRows.Count == 0) return "";
                return Convert.ToString(moduleGrid.SelectedRows[0].Cells[0].Value);
            }

            private void SetSelectedModule(bool enabled)
            {
                string id = SelectedModuleId(); if (id.Length == 0) { MessageBox.Show("Select a module first."); return; }
                owner.SetEvolutionModule(id, enabled); RefreshModuleLibrary();
            }

            private void RunSelectedModule()
            {
                string id = SelectedModuleId(); if (id.Length == 0) { MessageBox.Show("Select a module first."); return; }
                string action = id == "halloween-pack" ? "pumpkin" : "show";
                owner.server.Publish("module-action", "{\"id\":\"" + ControlDeckForm.Json(id) + "\",\"action\":\"" + action + "\",\"duration\":12,\"name\":\"" + ControlDeckForm.Json(id) + "\"}");
            }

            private int SelectedTickerHtmlSlot()
            {
                int slot;
                if (tickerHtmlSlot == null || !Int32.TryParse(tickerHtmlSlot.Text, out slot)) slot = 1;
                return Math.Max(1, Math.Min(6, slot));
            }

            private void RefreshTickerHtmlStatus()
            {
                if (tickerHtmlStatus == null) return;
                int slot = SelectedTickerHtmlSlot();
                string name = owner.TickerHtmlName(slot);
                tickerHtmlStatus.Text = name.Length == 0
                    ? "Slot " + slot + ": Empty • " + (6 - CountTickerHtmlSlots()) + " slot(s) available"
                    : "Slot " + slot + ": " + name + " • Stored in backupscripts_ticker";
            }

            private int CountTickerHtmlSlots()
            {
                int count = 0;
                for (int slot = 1; slot <= 6; slot++) if (owner.TickerHtmlName(slot).Length > 0) count++;
                return count;
            }

            private void UploadTickerHtml()
            {
                int slot = SelectedTickerHtmlSlot();
                if (!owner.InstallTickerHtml(slot, tickerHtmlName.Text)) return;
                RefreshTickerHtmlStatus();
                SetEvent("Floating ticker HTML uploaded to slot " + slot);
            }

            private void ShowTickerHtml()
            {
                int slot = SelectedTickerHtmlSlot();
                string name = owner.TickerHtmlName(slot);
                if (name.Length == 0) { MessageBox.Show("Ticker slot " + slot + " is empty. Upload an HTML overlay first."); return; }
                string id = ControlDeckForm.TickerHtmlId(slot);
                string backup = Path.Combine(owner.TickerHtmlScriptsFolder(), "slot-" + slot);
                if (!owner.RestoreUserHtmlOverlay(id, backup)) { MessageBox.Show("The ticker HTML backup could not be loaded."); return; }
                owner.SetEvolutionModule(id, true);
                var launchTimer = new Timer { Interval = 900 };
                launchTimer.Tick += delegate
                {
                    launchTimer.Stop();
                    launchTimer.Dispose();
                    owner.server.Publish("module-action", "{\"id\":\"" + ControlDeckForm.Json(id) + "\",\"action\":\"show\",\"duration\":0,\"name\":\"" + ControlDeckForm.Json(name) + "\"}");
                };
                launchTimer.Start();
                tickerHtmlStatus.Text = "Showing slot " + slot + ": " + name;
                SetEvent("Floating ticker HTML shown");
            }

            private void HideTickerHtml()
            {
                int slot = SelectedTickerHtmlSlot();
                string id = ControlDeckForm.TickerHtmlId(slot);
                owner.SetEvolutionModule(id, false);
                RefreshTickerHtmlStatus();
                SetEvent("Floating ticker slot " + slot + " hidden");
            }

            private void DeleteTickerHtml()
            {
                int slot = SelectedTickerHtmlSlot();
                string name = owner.TickerHtmlName(slot);
                if (name.Length == 0) { MessageBox.Show("Ticker slot " + slot + " is already empty."); return; }
                string id = ControlDeckForm.TickerHtmlId(slot);
                string backup = Path.Combine(owner.TickerHtmlScriptsFolder(), "slot-" + slot);
                if (!owner.DeleteUserHtmlOverlay(id, backup, "ticker slot " + slot + " (" + name + ")", this)) return;
                RefreshTickerHtmlStatus();
                SetEvent("Floating ticker HTML deleted");
            }

            private void RefreshDmcaHtmlStatus()
            {
                if (dmcaHtmlStatus == null) return;
                string name = owner.DmcaHtmlName();
                bool enabled = owner.LoadEnabledModules().Contains("dmca-custom");
                dmcaHtmlStatus.Text = name.Length == 0
                    ? "No DMCA HTML uploaded. Nothing is preloaded."
                    : name + "\r\nStatus: " + (enabled ? "ON" : "OFF") + " • One upload maximum";
            }

            private void UploadDmcaHtml()
            {
                if (!owner.InstallDmcaHtml(dmcaHtmlName.Text)) return;
                RefreshDmcaHtmlStatus();
                SetEvent("DMCA HTML uploaded");
            }

            private void TurnDmcaHtmlOn()
            {
                string name = owner.DmcaHtmlName();
                if (name.Length == 0) { MessageBox.Show("Upload one 1920 x 1080 DMCA HTML overlay first."); return; }
                string backup = Path.Combine(owner.DmcaHtmlScriptsFolder(), "dmca-custom");
                if (!owner.RestoreUserHtmlOverlay("dmca-custom", backup)) { MessageBox.Show("The DMCA HTML backup could not be loaded."); return; }
                var launchTimer = new Timer { Interval = 900 };
                launchTimer.Tick += delegate
                {
                    launchTimer.Stop();
                    launchTimer.Dispose();
                    owner.SetEvolutionModule("dmca-custom", true);
                    RefreshDmcaHtmlStatus();
                };
                launchTimer.Start();
                SetEvent("DMCA HTML turned on");
            }

            private void TurnDmcaHtmlOff()
            {
                owner.SetEvolutionModule("dmca-custom", false);
                RefreshDmcaHtmlStatus();
                SetEvent("DMCA HTML turned off");
            }

            private void DeleteDmcaHtml()
            {
                string name = owner.DmcaHtmlName();
                if (name.Length == 0) { MessageBox.Show("There is no DMCA HTML to delete."); return; }
                string backup = Path.Combine(owner.DmcaHtmlScriptsFolder(), "dmca-custom");
                if (!owner.DeleteUserHtmlOverlay("dmca-custom", backup, "the DMCA HTML overlay (" + name + ")", this)) return;
                RefreshDmcaHtmlStatus();
                SetEvent("DMCA HTML deleted");
            }

            private void SaveGameResultSettings()
            {
                string animation = resultAnimation.Text.ToLowerInvariant();
                File.WriteAllText(owner.StudioPath("game-result-v3.txt"), resultDuration.Value + "\t" + resultFade.Value + "\t" + animation + "\t" + resultUsername.Checked + "\t" + resultPrize.Checked + "\t" + resultValue.Checked);
                owner.server.Publish("game-result-settings", "{\"duration\":" + ((int)resultDuration.Value * 1000) + ",\"fadeDuration\":" + ((int)resultFade.Value * 1000) + ",\"animation\":\"" + animation + "\",\"showUsername\":" + resultUsername.Checked.ToString().ToLowerInvariant() + ",\"showPrize\":" + resultPrize.Checked.ToString().ToLowerInvariant() + ",\"showResult\":" + resultValue.Checked.ToString().ToLowerInvariant() + "}");
            }

            private string TemplatesFolder()
            {
                string folder = owner.StudioPath("Templates");
                Directory.CreateDirectory(folder);
                return folder;
            }

            private static string DefaultTemplateJson()
            {
                return "{\r\n  \"template\": \"My Creator Layout\",\r\n  \"canvas\": { \"width\": 1920, \"height\": 1080 },\r\n  \"zones\": {\r\n    \"cameraZone\": { \"module\": \"camera\", \"x\": 278, \"y\": 130, \"width\": 1364, \"height\": 790 },\r\n    \"gameZone\": { \"module\": \"game-zone\", \"x\": 1655, \"y\": 400, \"width\": 240, \"height\": 460 },\r\n    \"alertZone\": { \"module\": \"alert\", \"x\": 1655, \"y\": 875, \"width\": 240, \"height\": 150 }\r\n  }\r\n}";
            }

            private string SafeTemplateName(string value)
            {
                string safe = Regex.Replace(CleanField(value), "[^A-Za-z0-9 _-]", "").Trim();
                return safe.Length == 0 ? "Custom Template" : safe.Substring(0, Math.Min(60, safe.Length));
            }

            private void RefreshTemplateList()
            {
                if (templateSelector == null) return;
                templateSelector.Items.Clear();
                foreach (string file in Directory.GetFiles(TemplatesFolder(), "*.json")) templateSelector.Items.Add(Path.GetFileNameWithoutExtension(file));
                if (templateSelector.Items.Count > 0 && templateSelector.SelectedIndex < 0) templateSelector.SelectedIndex = 0;
            }

            
            private static string[] FullLayoutPackModules()
            {
                return new string[] {
                    "brand", "camera", "goal", "supporters", "last-tipper", "recent",
                    "ticker", "alert", "game-zone", "vip", "dmca", "background"
                };
            }

            private string LayoutPackHistoryFolder()
            {
                string folder = owner.StudioPath("layout-pack-history");
                Directory.CreateDirectory(folder);
                return folder;
            }

            private string CreateLayoutPackRecoverySnapshot(string label)
            {
                string safe = Regex.Replace(label ?? "layout", "[^A-Za-z0-9._ -]+", "-").Trim();
                if (safe.Length == 0) safe = "layout";
                if (safe.Length > 48) safe = safe.Substring(0, 48);

                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string snapshot = Path.Combine(LayoutPackHistoryFolder(), stamp + "-" + safe + ".txt");
                string current = owner.StudioPath("module-styles-v3.txt");

                if (File.Exists(current))
                {
                    File.Copy(current, snapshot, true);
                }
                else
                {
                    File.WriteAllText(snapshot, "", new UTF8Encoding(false));
                }

                return snapshot;
            }

            private bool TryNormalizeFullLayoutStyles(string text, out string normalized, out string summary, out string error)
            {
                normalized = "";
                summary = "";
                error = "";

                var allowed = new HashSet<string>(FullLayoutPackModules(), StringComparer.OrdinalIgnoreCase);
                var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                string[] lines = (text ?? "").Replace("\r", "").Split('\n');
                for (int index = 0; index < lines.Length; index++)
                {
                    string line = lines[index].Trim();
                    if (line.Length == 0) continue;

                    string[] parts = line.Split('|');
                    if (parts.Length < 6)
                    {
                        error = "Line " + (index + 1) + " does not contain module|x|y|scale|opacity|width.";
                        return false;
                    }

                    string module = parts[0].Trim().ToLowerInvariant();
                    if (!allowed.Contains(module))
                    {
                        error = "Unsupported layout module: " + module;
                        return false;
                    }
                    if (found.ContainsKey(module))
                    {
                        error = "Duplicate layout module: " + module;
                        return false;
                    }

                    decimal x, y, scale, opacity, width;
                    if (!Decimal.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out x) &&
                        !Decimal.TryParse(parts[1], out x))
                    {
                        error = module + ": invalid X value.";
                        return false;
                    }
                    if (!Decimal.TryParse(parts[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out y) &&
                        !Decimal.TryParse(parts[2], out y))
                    {
                        error = module + ": invalid Y value.";
                        return false;
                    }
                    if (!Decimal.TryParse(parts[3], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out scale))
                    {
                        error = module + ": invalid scale value.";
                        return false;
                    }
                    if (!Decimal.TryParse(parts[4], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out opacity))
                    {
                        error = module + ": invalid opacity value.";
                        return false;
                    }
                    if (!Decimal.TryParse(parts[5], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out width) &&
                        !Decimal.TryParse(parts[5], out width))
                    {
                        error = module + ": invalid width value.";
                        return false;
                    }

                    if (x < -1500 || x > 1500) { error = module + ": X must be between -1500 and 1500."; return false; }
                    if (y < -900 || y > 900) { error = module + ": Y must be between -900 and 900."; return false; }
                    if (scale < .25m || scale > 3m) { error = module + ": scale must be between 0.25 and 3.00."; return false; }
                    if (opacity < 0m || opacity > 1m) { error = module + ": opacity must be between 0 and 1."; return false; }
                    if (width < 0m || width > 100m) { error = module + ": width must be between 0 and 100."; return false; }

                    found[module] =
                        module + "|" +
                        x.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" +
                        y.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" +
                        scale.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" +
                        opacity.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" +
                        width.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                string[] required = FullLayoutPackModules();
                var missing = new List<string>();
                foreach (string module in required)
                {
                    if (!found.ContainsKey(module)) missing.Add(module);
                }

                if (missing.Count > 0)
                {
                    error = "This is not a complete layout pack. Missing: " + String.Join(", ", missing.ToArray());
                    return false;
                }

                var output = new List<string>();
                var preview = new List<string>();
                foreach (string module in required)
                {
                    output.Add(found[module]);
                    string[] p = found[module].Split('|');
                    preview.Add(module.PadRight(14) + " X " + p[1].PadLeft(5) + "  Y " + p[2].PadLeft(5) +
                        "  Size " + (Decimal.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture) * 100m).ToString("0") + "%" +
                        "  Opacity " + (Decimal.Parse(p[4], System.Globalization.CultureInfo.InvariantCulture) * 100m).ToString("0") + "%");
                }

                normalized = String.Join(Environment.NewLine, output.ToArray()) + Environment.NewLine;
                summary = String.Join(Environment.NewLine, preview.ToArray());
                return true;
            }

            private string ReadLayoutPackEntry(ZipArchive archive, string fileName, long maximumBytes)
            {
                ZipArchiveEntry found = null;
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string clean = (entry.FullName ?? "").Replace('\\', '/');
                    if (clean.StartsWith("/") || clean.Contains("../") || clean.Contains("/.."))
                        throw new InvalidDataException("Unsafe ZIP path: " + clean);

                    if (String.Equals(Path.GetFileName(clean), fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (found != null) throw new InvalidDataException("Layout pack contains more than one " + fileName + ".");
                        found = entry;
                    }
                }

                if (found == null) throw new InvalidDataException("Layout pack is missing " + fileName + ".");
                if (found.Length > maximumBytes) throw new InvalidDataException(fileName + " exceeds the allowed size.");

                using (Stream stream = found.Open())
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    string text = reader.ReadToEnd();
                    if (Encoding.UTF8.GetByteCount(text) > maximumBytes)
                        throw new InvalidDataException(fileName + " exceeds the allowed size.");
                    return text;
                }
            }

            private void ImportLayoutPackZip()
            {
                using (var dialog = new OpenFileDialog {
                    Filter = "StimTake Layout Pack (*.zip)|*.zip",
                    CheckFileExists = true,
                    Title = "Import Full StimTake Layout Pack"
                })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;

                    var info = new FileInfo(dialog.FileName);
                    if (info.Length > 16L * 1024L * 1024L)
                    {
                        MessageBox.Show("Layout Pack ZIP exceeds the 16 MB safety limit.");
                        return;
                    }

                    try
                    {
                        string manifestText;
                        string stylesText;

                        using (var file = new FileStream(dialog.FileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var archive = new ZipArchive(file, ZipArchiveMode.Read, false))
                        {
                            if (archive.Entries.Count > 100)
                                throw new InvalidDataException("Layout Pack contains too many archive entries.");

                            manifestText = ReadLayoutPackEntry(archive, "layout-pack.json", 256 * 1024);
                            stylesText = ReadLayoutPackEntry(archive, "module-styles-v3.txt", 256 * 1024);
                        }

                        if (!Regex.IsMatch(manifestText, "\"type\"\\s*:\\s*\"stimtake-layout-pack\"", RegexOptions.IgnoreCase))
                        {
                            MessageBox.Show("That ZIP is not a StimTake Layout Pack.\r\n\r\nUse IMPORT JSON for old Creator Cam zone templates.");
                            return;
                        }

                        string normalized, summary, error;
                        if (!TryNormalizeFullLayoutStyles(stylesText, out normalized, out summary, out error))
                        {
                            MessageBox.Show("Layout Pack validation failed.\r\n\r\n" + error, "StimTake Layout Pack", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        DialogResult approval = MessageBox.Show(
                            "Full Layout Pack validated successfully.\r\n\r\n" + summary +
                            "\r\n\r\nThis will replace all 12 saved overlay position records.\r\nThe current layout will be backed up first.\r\n\r\nContinue?",
                            "Import Full Layout Pack",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);
                        if (approval != DialogResult.Yes) return;

                        string recovery = CreateLayoutPackRecoverySnapshot(Path.GetFileNameWithoutExtension(dialog.FileName));
                        string target = owner.StudioPath("module-styles-v3.txt");
                        string staging = target + ".layout-stage-" + Guid.NewGuid().ToString("N");

                        File.WriteAllText(staging, normalized, new UTF8Encoding(false));
                        string stagedText = File.ReadAllText(staging, Encoding.UTF8);
                        string stagedNormalized, stagedSummary, stagedError;
                        if (!TryNormalizeFullLayoutStyles(stagedText, out stagedNormalized, out stagedSummary, out stagedError))
                            throw new InvalidDataException("Staged layout failed validation: " + stagedError);

                        try
                        {
                            File.Copy(staging, target, true);
                            owner.PublishStudioBootstrap();
                            LoadSelectedModuleStyle();
                            MessageBox.Show(
                                "Full Layout Pack applied.\r\n\r\nPrevious positions were preserved at:\r\n" + recovery,
                                "StimTake Layout Pack");
                        }
                        catch
                        {
                            try
                            {
                                if (File.Exists(recovery)) File.Copy(recovery, target, true);
                                owner.PublishStudioBootstrap();
                            }
                            catch { }
                            throw;
                        }
                        finally
                        {
                            try { if (File.Exists(staging)) File.Delete(staging); } catch { }
                        }
                    }
                    catch (Exception error)
                    {
                        MessageBox.Show(
                            "Layout Pack was not applied.\r\n\r\n" + error.Message,
                            "StimTake Layout Pack",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            }

            private void ExportCurrentLayoutPackZip()
            {
                string stylesPath = owner.StudioPath("module-styles-v3.txt");
                if (!File.Exists(stylesPath))
                {
                    MessageBox.Show("No saved module position file exists yet. Move/save the overlay elements first.");
                    return;
                }

                string normalized, summary, error;
                if (!TryNormalizeFullLayoutStyles(File.ReadAllText(stylesPath, Encoding.UTF8), out normalized, out summary, out error))
                {
                    MessageBox.Show(
                        "Current layout cannot be exported as a FULL pack yet.\r\n\r\n" + error +
                        "\r\n\r\nOpen the Overlay Position Designer and save/reset each missing element once.",
                        "StimTake Layout Pack");
                    return;
                }

                string defaultName = SafeTemplateName(templateName != null ? templateName.Text : "My Layout") + "_Layout_Pack.zip";
                using (var dialog = new SaveFileDialog {
                    Filter = "StimTake Layout Pack (*.zip)|*.zip",
                    FileName = defaultName,
                    OverwritePrompt = true
                })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;

                    string manifest =
                        "{\r\n" +
                        "  \"schemaVersion\": 1,\r\n" +
                        "  \"type\": \"stimtake-layout-pack\",\r\n" +
                        "  \"name\": \"" + ControlDeckForm.Json(Path.GetFileNameWithoutExtension(dialog.FileName)) + "\",\r\n" +
                        "  \"canvas\": { \"width\": 1920, \"height\": 1080 },\r\n" +
                        "  \"sourceFormat\": \"module-styles-v3.txt\",\r\n" +
                        "  \"moduleCount\": 12\r\n" +
                        "}\r\n";

                    using (var file = new FileStream(dialog.FileName, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                    using (var archive = new ZipArchive(file, ZipArchiveMode.Create, false))
                    {
                        ZipArchiveEntry manifestEntry = archive.CreateEntry("layout-pack.json", CompressionLevel.Optimal);
                        using (var writer = new StreamWriter(manifestEntry.Open(), new UTF8Encoding(false))) writer.Write(manifest);

                        ZipArchiveEntry stylesEntry = archive.CreateEntry("module-styles-v3.txt", CompressionLevel.Optimal);
                        using (var writer = new StreamWriter(stylesEntry.Open(), new UTF8Encoding(false))) writer.Write(normalized);

                        ZipArchiveEntry readmeEntry = archive.CreateEntry("README.txt", CompressionLevel.Optimal);
                        using (var writer = new StreamWriter(readmeEntry.Open(), new UTF8Encoding(false)))
                        {
                            writer.Write(
                                "StimTake Full Layout Pack\r\n" +
                                "Canvas: 1920 x 1080\r\n" +
                                "Modules: 12\r\n" +
                                "Import from LAYOUT + THEMES > JSON LAYOUT TEMPLATE + FULL LAYOUT PACK SYSTEM.\r\n");
                        }
                    }

                    MessageBox.Show("Full Layout Pack exported successfully.", "StimTake Layout Pack");
                }
            }

            private void RestorePreviousLayoutPack()
            {
                string history = LayoutPackHistoryFolder();
                string[] backups = Directory.GetFiles(history, "*.txt");
                if (backups.Length == 0)
                {
                    MessageBox.Show("No previous Layout Pack recovery backup exists yet.");
                    return;
                }

                Array.Sort(backups, StringComparer.OrdinalIgnoreCase);
                string previous = backups[backups.Length - 1];

                if (MessageBox.Show(
                    "Restore the most recent saved layout positions?\r\n\r\n" + Path.GetFileName(previous) +
                    "\r\n\r\nThe current position set will be backed up first.",
                    "Restore Previous Layout",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes) return;

                string currentRecovery = CreateLayoutPackRecoverySnapshot("before-layout-restore");
                string target = owner.StudioPath("module-styles-v3.txt");

                try
                {
                    string normalized, summary, error;
                    if (!TryNormalizeFullLayoutStyles(File.ReadAllText(previous, Encoding.UTF8), out normalized, out summary, out error))
                        throw new InvalidDataException("Backup validation failed: " + error);

                    File.WriteAllText(target, normalized, new UTF8Encoding(false));
                    owner.PublishStudioBootstrap();
                    LoadSelectedModuleStyle();
                    MessageBox.Show("Previous full layout restored successfully.", "StimTake Layout Pack");
                }
                catch (Exception error)
                {
                    try
                    {
                        if (File.Exists(currentRecovery)) File.Copy(currentRecovery, target, true);
                        owner.PublishStudioBootstrap();
                    }
                    catch { }

                    MessageBox.Show(
                        "Previous layout could not be restored.\r\n\r\n" + error.Message,
                        "StimTake Layout Pack",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }

private void SaveTemplate()
            {
                if (!ControlDeckForm.IsTemplateJson(templateEditor.Text)) { MessageBox.Show("Template JSON must contain a valid zones object."); return; }
                string name = SafeTemplateName(templateName.Text);
                File.WriteAllText(Path.Combine(TemplatesFolder(), name + ".json"), templateEditor.Text, Encoding.UTF8);
                File.WriteAllText(owner.StudioPath("active-template-v3.json"), templateEditor.Text, Encoding.UTF8);
                RefreshTemplateList(); templateSelector.SelectedItem = name; PublishTemplate();
            }

            private void LoadTemplateEditor()
            {
                if (templateSelector.SelectedItem == null) return;
                string path = Path.Combine(TemplatesFolder(), templateSelector.Text + ".json");
                if (File.Exists(path)) { templateName.Text = templateSelector.Text; templateEditor.Text = File.ReadAllText(path, Encoding.UTF8); }
            }

            private void PublishTemplate()
            {
                if (!ControlDeckForm.IsTemplateJson(templateEditor.Text)) { MessageBox.Show("Template JSON must contain a valid zones object."); return; }
                File.WriteAllText(owner.StudioPath("active-template-v3.json"), templateEditor.Text, Encoding.UTF8);
                owner.server.Publish("layout-template", templateEditor.Text);
            }

            private void DuplicateTemplate()
            {
                templateName.Text = SafeTemplateName(templateName.Text) + " Copy"; SaveTemplate();
            }

            private void ImportTemplate()
            {
                using (var dialog = new OpenFileDialog { Filter = "Creator Cam Template (*.json)|*.json", CheckFileExists = true })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    if (new FileInfo(dialog.FileName).Length > 1024 * 1024) { MessageBox.Show("Template exceeds the 1 MB limit."); return; }
                    string text = File.ReadAllText(dialog.FileName, Encoding.UTF8);
                    if (!ControlDeckForm.IsTemplateJson(text)) { MessageBox.Show("That file is not a Creator Cam zone template."); return; }
                    templateName.Text = SafeTemplateName(Path.GetFileNameWithoutExtension(dialog.FileName)); templateEditor.Text = text;
                }
            }

            private void ExportTemplate()
            {
                if (!ControlDeckForm.IsTemplateJson(templateEditor.Text)) { MessageBox.Show("Template JSON must contain a valid zones object."); return; }
                using (var dialog = new SaveFileDialog { Filter = "Creator Cam Template (*.json)|*.json", FileName = SafeTemplateName(templateName.Text) + ".json", OverwritePrompt = true })
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK) File.WriteAllText(dialog.FileName, templateEditor.Text, Encoding.UTF8);
                }
            }

            private void LoadEvolutionEditors()
            {
                RefreshActionButtons();
                RefreshActionSlotEditor();
                try
                {
                    string path = owner.StudioPath("game-result-v3.txt"); if (File.Exists(path))
                    {
                        string[] values = File.ReadAllText(path).Split('\t'); decimal number; bool flag;
                        if (values.Length > 0 && Decimal.TryParse(values[0], out number)) resultDuration.Value = Math.Max(resultDuration.Minimum, Math.Min(resultDuration.Maximum, number));
                        if (values.Length > 1 && Decimal.TryParse(values[1], out number)) resultFade.Value = Math.Max(resultFade.Minimum, Math.Min(resultFade.Maximum, number));
                        if (values.Length > 2 && values[2].Length > 0) resultAnimation.SelectedItem = Char.ToUpper(values[2][0]) + values[2].Substring(1);
                        if (values.Length > 3 && Boolean.TryParse(values[3], out flag)) resultUsername.Checked = flag;
                        if (values.Length > 4 && Boolean.TryParse(values[4], out flag)) resultPrize.Checked = flag;
                        if (values.Length > 5 && Boolean.TryParse(values[5], out flag)) resultValue.Checked = flag;
                    }
                }
                catch { }
                RefreshTickerHtmlStatus();
                RefreshDmcaHtmlStatus();
                try { string path = owner.StudioPath("connector-v3.txt"); if (File.Exists(path)) connectorPlatform.SelectedItem = File.ReadAllText(path); } catch { }
                try { string path = owner.StudioPath("active-template-v3.json"); if (File.Exists(path)) templateEditor.Text = File.ReadAllText(path, Encoding.UTF8); } catch { }
            }
        }

        internal sealed partial class StaticServer
        {
            internal event Action<string, string> EventPublished;

            internal bool ExportBundledAsset(string assetPath, string destination)
            {
                try
                {
                    Asset asset;
                    string key = "/" + (assetPath ?? "").Replace('\\', '/').TrimStart('/');
                    if (!assets.TryGetValue(key, out asset)) return false;
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.WriteAllBytes(destination, asset.Bytes);
                    return true;
                }
                catch (Exception error)
                {
                    Program.LogRuntimeError("Export bundled wheel script", error);
                    return false;
                }
            }

            private void NotifyEvolutionEvent(string type, string payload)
            {
                Action<string, string> handler = EventPublished;
                if (handler != null) try { handler(type, payload); } catch { }
            }

            private string ExternalModulesFolder()
            {
                string folder = Path.Combine(LocalDataRoot(), "CreatorCamOverlayKit", "Modules");
                Directory.CreateDirectory(folder);
                return folder;
            }

            private bool TryServeEvolution(string path, string target, string requestLine, Stream network)
            {
                if (path == "/api/recent-supporter")
                {
                    string recent;
                    try { recent = BuildManualRecentSupporterState(); }
                    catch (Exception error)
                    {
                        Program.LogRuntimeError("Read manual Last Supporter settings", error);
                        recent = "null";
                    }
                    byte[] recentBody = Encoding.UTF8.GetBytes(recent);
                    WriteResponse(network, "200 OK", "application/json; charset=utf-8", recentBody, requestLine.StartsWith("HEAD "));
                    return true;
                }
                if (path == "/api/moving-watermark")
                {
                    string settings;
                    try { settings = BuildMovingWatermarkState(); }
                    catch (Exception error)
                    {
                        Program.LogRuntimeError("Read moving watermark settings", error);
                        settings = "{\"title\":\"OBSIDIAN\",\"username\":\"STALLION\",\"tagline\":\"LIVE • VERIFIED • HD\",\"enabled\":false,\"opacity\":0.82,\"speed\":36}";
                    }
                    byte[] body = Encoding.UTF8.GetBytes(settings);
                    WriteResponse(network, "200 OK", "application/json; charset=utf-8", body, requestLine.StartsWith("HEAD "));
                    return true;
                }
                if (path == "/api/modules")
                {
                    string catalog;
                    try { catalog = BuildExternalModuleCatalog(); }
                    catch (Exception error)
                    {
                        Program.LogRuntimeError("Load external module catalog", error);
                        catalog = "{\"modules\":[]}";
                    }
                    byte[] body = Encoding.UTF8.GetBytes(catalog);
                    WriteResponse(network, "200 OK", "application/json; charset=utf-8", body, requestLine.StartsWith("HEAD "));
                    return true;
                }
                const string prefix = "/external-modules/";
                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
                string relative = path.Substring(prefix.Length).Replace('/', Path.DirectorySeparatorChar);
                string root = Path.GetFullPath(ExternalModulesFolder()) + Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(Path.Combine(root, relative));
                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                {
                    WriteResponse(network, "404 Not Found", "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Module asset not found."), requestLine.StartsWith("HEAD "));
                    return true;
                }
                string extension = Path.GetExtension(full).ToLowerInvariant();
                if (!Regex.IsMatch(extension, "^\\.(json|html|css|js|mjs|png|jpg|jpeg|gif|webp|svg|wav|mp3|ogg|txt|woff|woff2|ttf|otf|mp4|webm)$") || new FileInfo(full).Length > 32L * 1024L * 1024L)
                {
                    WriteResponse(network, "403 Forbidden", "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Module asset was blocked."), requestLine.StartsWith("HEAD "));
                    return true;
                }
                byte[] content = File.ReadAllBytes(full);
                WriteResponse(network, "200 OK", Mime(full), content, requestLine.StartsWith("HEAD "));
                return true;
            }

            private void ClearPersistedManualRecentSupporterForTip(string type, string payloadJson)
            {
                string eventType = (type ?? "").Trim().ToLowerInvariant();
                bool isTip = eventType == "tip" || ((eventType == "platform-event" || eventType == "viewer-event") && Regex.IsMatch(payloadJson ?? "", "\"type\"\\s*:\\s*\"tip\"", RegexOptions.IgnoreCase));
                if (!isTip) return;
                try
                {
                    string path = Path.Combine(LocalDataRoot(), "CreatorCamOverlayKit", "recent-supporter-v3.txt");
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (Exception error) { Program.LogRuntimeError("Replace manual Last Supporter", error); }
            }

            private string BuildManualRecentSupporterState()
            {
                string path = Path.Combine(LocalDataRoot(), "CreatorCamOverlayKit", "recent-supporter-v3.txt");
                if (!File.Exists(path)) return "null";
                string[] values = File.ReadAllText(path, Encoding.UTF8).Split('\t');
                string username = values.Length > 0 ? values[0].Replace("\r", " ").Replace("\n", " ").Trim() : "";
                if (username.Length == 0) return "null";
                if (username.Length > 64) username = username.Substring(0, 64);
                int amount;
                if (values.Length < 2 || !Int32.TryParse(values[1], out amount)) amount = 0;
                amount = Math.Max(0, Math.Min(999999, amount));
                string message = values.Length > 2 ? values[2].Replace("\r", " ").Replace("\n", " ").Trim() : "";
                if (message.Length > 80) message = message.Substring(0, 80);
                return "{\"username\":\"" + ControlDeckForm.Json(username) + "\",\"amount\":" + amount + ",\"message\":\"" + ControlDeckForm.Json(message) + "\",\"manual\":true,\"at\":0}";
            }

            private string BuildMovingWatermarkState()
            {
                string[] values = new string[] { "OBSIDIAN", "STALLION", "LIVE • VERIFIED • HD", "False", "82" };
                string settingsPath = Path.Combine(LocalDataRoot(), "CreatorCamOverlayKit", "moving-watermark-v4.txt");
                if (File.Exists(settingsPath))
                {
                    string[] saved = File.ReadAllText(settingsPath, Encoding.UTF8).Split('\t');
                    for (int index = 0; index < values.Length && index < saved.Length; index++)
                        if (!String.IsNullOrWhiteSpace(saved[index])) values[index] = saved[index];
                }
                values[0] = values[0].Trim(); if (values[0].Length == 0) values[0] = "OBSIDIAN"; if (values[0].Length > 32) values[0] = values[0].Substring(0, 32);
                values[1] = values[1].Trim(); if (values[1].Length == 0) values[1] = "STALLION"; if (values[1].Length > 40) values[1] = values[1].Substring(0, 40);
                values[2] = values[2].Trim(); if (values[2].Length == 0) values[2] = "LIVE • VERIFIED • HD"; if (values[2].Length > 60) values[2] = values[2].Substring(0, 60);
                bool savedEnabled;
                bool enabled = Boolean.TryParse(values[3], out savedEnabled) && savedEnabled;
                int opacityPercent;
                if (!Int32.TryParse(values[4], out opacityPercent)) opacityPercent = 82;
                opacityPercent = Math.Max(10, Math.Min(95, opacityPercent));
                string opacity = (opacityPercent / 100.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                return "{\"title\":\"" + ControlDeckForm.Json(values[0]) + "\",\"username\":\"" + ControlDeckForm.Json(values[1]) + "\",\"tagline\":\"" + ControlDeckForm.Json(values[2]) +
                    "\",\"enabled\":" + enabled.ToString().ToLowerInvariant() + ",\"opacity\":" + opacity + ",\"speed\":36}";
            }

            private string BuildExternalModuleCatalog()
            {
                var items = new List<string>();
                foreach (string directory in Directory.GetDirectories(ExternalModulesFolder()))
                {
                    try
                    {
                        string manifestPath = Path.Combine(directory, "module.json"); if (!File.Exists(manifestPath)) continue;
                        string raw = File.ReadAllText(manifestPath, Encoding.UTF8).Trim();
                        string id = ControlDeckForm.ManifestValue(raw, "id").ToLowerInvariant();
                        string type = ControlDeckForm.ManifestValue(raw, "type").ToUpperInvariant();
                        if (!Regex.IsMatch(id, "^[a-z0-9][a-z0-9-]{1,48}$") || !Regex.IsMatch(type, "^(GAME|COMMUNITY|THEME|ALERT|DECORATION)$") || !raw.StartsWith("{") || !raw.EndsWith("}")) continue;
                        items.Add("{\"id\":\"" + ControlDeckForm.Json(id) + "\",\"baseUrl\":\"/external-modules/" + ControlDeckForm.Json(id) + "/\",\"manifest\":" + raw + "}");
                    }
                    catch (Exception error) { Program.LogRuntimeError("Read external module", error); }
                }
                return "{\"modules\":[" + String.Join(",", items.ToArray()) + "]}";
            }
        }
    }
}
