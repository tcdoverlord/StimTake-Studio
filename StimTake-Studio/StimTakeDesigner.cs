using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using StimTakeShared;

[assembly: System.Reflection.AssemblyTitle("StimTake Designer")]
[assembly: System.Reflection.AssemblyProduct("StimTake Designer")]
[assembly: System.Reflection.AssemblyCompany("Talented Creative Design and TCDOVERLORD")]
[assembly: System.Reflection.AssemblyCopyright("Copyright 2026 Talented Creative Design and TCDOVERLORD")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

namespace StimTakeDesigner
{
    internal static class DesignerProgram
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args != null && args.Length == 3 && String.Equals(args[0], "--build-pack", StringComparison.OrdinalIgnoreCase))
            {
                Environment.ExitCode = BuildPackCommand(args[1], args[2]);
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new DesignerForm());
        }

        private static int BuildPackCommand(string workspace, string outputZip)
        {
            try
            {
                ShowPackValidation validation = ShowPackValidator.ValidateDirectory(workspace);
                if (!validation.IsValid) return 2;
                string destination = Path.GetFullPath(outputZip);
                if (File.Exists(destination)) return 3;
                string parent = Path.GetDirectoryName(destination);
                if (String.IsNullOrWhiteSpace(parent)) return 4;
                Directory.CreateDirectory(parent);
                ZipFile.CreateFromDirectory(Path.GetFullPath(workspace), destination, CompressionLevel.Optimal, false);
                ShowPackValidation zipValidation = ShowPackValidator.ValidateZip(destination);
                if (zipValidation.IsValid) return 0;
                try { File.Delete(destination); } catch { }
                return 5;
            }
            catch { return 1; }
        }
    }

    internal sealed class DesignerForm : Form
    {
        private static readonly Color Bg = Color.FromArgb(13, 10, 24);
        private static readonly Color Side = Color.FromArgb(21, 14, 37);
        private static readonly Color Card = Color.FromArgb(30, 21, 51);
        private static readonly Color Card2 = Color.FromArgb(38, 27, 64);
        private static readonly Color Purple = Color.FromArgb(142, 72, 255);
        private static readonly Color TextMain = Color.FromArgb(245, 242, 252);
        private static readonly Color TextMuted = Color.FromArgb(177, 169, 197);
        private static readonly Color Green = Color.FromArgb(64, 220, 151);

        private readonly string designerRoot;
        private readonly string workspaceRoot;

        private TextBox packName;
        private TextBox packId;
        private TextBox packVersion;
        private TextBox themeName;
        private ComboBox actionSlot;
        private TextBox actionName;
        private TextBox actionOverlay;
        private NumericUpDown actionDuration;
        private CheckBox actionEnabledByDefault;
        private Label workspaceStatus;
        private ListView actionList;
        private TextBox themeJson;

        internal DesignerForm()
        {
            string isolated = (Environment.GetEnvironmentVariable("STIMTAKE_RUNTIME_ROOT") ?? "").Trim();
            string local = isolated.Length > 0 && Path.IsPathRooted(isolated)
                ? Path.GetFullPath(isolated)
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            designerRoot = Path.Combine(local, "StimTakeDesigner");
            workspaceRoot = Path.Combine(designerRoot, "workspace");
            Directory.CreateDirectory(workspaceRoot);

            Text = "StimTake Designer 1.0";
            Icon = SystemIcons.Application;
            BackColor = Bg;
            ForeColor = TextMain;
            Font = new Font("Segoe UI", 9.5f);
            MinimumSize = new Size(1100, 720);
            ClientSize = new Size(1260, 790);
            StartPosition = FormStartPosition.CenterScreen;

            BuildUi();
            LoadDefaults();
            RefreshActionList();
        }

        private void BuildUi()
        {
            var side = new Panel { Dock = DockStyle.Left, Width = 220, BackColor = Side, Padding = new Padding(16) };
            Controls.Add(side);
            side.Controls.Add(new Label
            {
                Text = "STIMTAKE\nDESIGNER",
                Dock = DockStyle.Top,
                Height = 78,
                ForeColor = Color.FromArgb(185, 150, 255),
                Font = new Font("Segoe UI", 19, FontStyle.Bold)
            });

            var purpose = new Label
            {
                Text = "Developer / Content App\nBuild the show. Studio runs it.",
                Location = new Point(16, 100),
                Size = new Size(185, 72),
                ForeColor = TextMuted
            };
            side.Controls.Add(purpose);

            side.Controls.Add(ButtonAt("NEW PACK", 16, 195, 185, delegate { NewPack(); }, true));
            side.Controls.Add(ButtonAt("OPEN WORKSPACE", 16, 242, 185, delegate { OpenWorkspace(); }, false));
            side.Controls.Add(ButtonAt("SAVE PACK", 16, 289, 185, delegate { SaveAll(); }, false));
            side.Controls.Add(ButtonAt("VALIDATE PACK", 16, 336, 185, delegate { ValidatePack(true); }, false));
            side.Controls.Add(ButtonAt("BUILD SHOW PACK ZIP", 16, 383, 185, delegate { BuildZip(); }, true));

            workspaceStatus = new Label
            {
                Text = "Workspace: not saved",
                Location = new Point(16, 455),
                Size = new Size(185, 110),
                ForeColor = TextMuted
            };
            side.Controls.Add(workspaceStatus);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildPackTab());
            tabs.TabPages.Add(BuildActionTab());
            tabs.TabPages.Add(BuildThemeTab());
            tabs.TabPages.Add(BuildSpecTab());
            Controls.Add(tabs);
            tabs.BringToFront();
        }

        private TabPage Page(string title)
        {
            return new TabPage(title) { BackColor = Bg, ForeColor = TextMain, Padding = new Padding(18), AutoScroll = true };
        }

        private TabPage BuildPackTab()
        {
            var page = Page("PACK");
            var card = Group("SHOW PACK IDENTITY", 15, 15, 930, 315);
            page.Controls.Add(card);

            LabelBox(card, "Pack name", 20, 45);
            packName = Box(card, "My StimTake Show Pack", 20, 70, 520);

            LabelBox(card, "Pack ID", 20, 115);
            packId = Box(card, "my-show-pack", 20, 140, 520);

            LabelBox(card, "Version", 565, 45);
            packVersion = Box(card, "1.0.0", 565, 70, 220);

            LabelBox(card, "Theme", 565, 115);
            themeName = Box(card, "custom", 565, 140, 220);

            card.Controls.Add(new Label
            {
                Text = "Designer creates portable creative content. Studio owns the model's token prices, live connection, supporter state, and show execution.",
                Location = new Point(20, 205),
                Size = new Size(850, 55),
                ForeColor = TextMuted
            });
            card.Controls.Add(ButtonAt("SAVE PACK MANIFEST", 20, 260, 210, delegate { SavePackManifest(); RefreshActionList(); }, true));

            var flow = Group("PRODUCT CONTRACT", 15, 350, 930, 220);
            flow.Controls.Add(new Label
            {
                Text = "StimTake Designer  →  Show Pack ZIP  →  StimTake Studio 6.0\n\n" +
                       "Designer: action identity, overlay, assets, theme, preview\n" +
                       "Studio: token price, enabled state, live session, model lock, tip processing",
                Location = new Point(20, 50),
                Size = new Size(850, 120),
                ForeColor = TextMain,
                Font = new Font("Segoe UI", 11)
            });
            page.Controls.Add(flow);
            return page;
        }

        private TabPage BuildActionTab()
        {
            var page = Page("ACTIONS");

            var left = Group("ACTION SLOTS 01–20", 15, 15, 440, 610);
            page.Controls.Add(left);

            actionList = new ListView
            {
                Location = new Point(18, 48),
                Size = new Size(404, 485),
                View = View.Details,
                FullRowSelect = true,
                BackColor = Card2,
                ForeColor = TextMain
            };
            actionList.Columns.Add("SLOT", 70);
            actionList.Columns.Add("ACTION", 245);
            actionList.Columns.Add("READY", 80);
            actionList.SelectedIndexChanged += delegate
            {
                if (actionList.SelectedItems.Count > 0)
                {
                    int slot;
                    if (Int32.TryParse(actionList.SelectedItems[0].Text, out slot))
                    {
                        actionSlot.SelectedIndex = Math.Max(0, Math.Min(19, slot - 1));
                        LoadActionSlot(slot);
                    }
                }
            };
            left.Controls.Add(actionList);
            left.Controls.Add(ButtonAt("REFRESH", 18, 548, 120, delegate { RefreshActionList(); }, false));

            var editor = Group("ACTION EDITOR", 475, 15, 520, 610);
            page.Controls.Add(editor);

            LabelBox(editor, "Slot", 20, 46);
            actionSlot = new ComboBox
            {
                Location = new Point(20, 70),
                Size = new Size(120, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Card2,
                ForeColor = TextMain
            };
            for (int i = 1; i <= 20; i++) actionSlot.Items.Add(i.ToString("00"));
            actionSlot.SelectedIndex = 0;
            actionSlot.SelectedIndexChanged += delegate { LoadActionSlot(actionSlot.SelectedIndex + 1); };
            editor.Controls.Add(actionSlot);

            LabelBox(editor, "Action name", 20, 112);
            actionName = Box(editor, "Action 01", 20, 136, 450);

            LabelBox(editor, "Overlay HTML", 20, 180);
            actionOverlay = Box(editor, "overlay.html", 20, 204, 330);
            editor.Controls.Add(ButtonAt("CHOOSE", 365, 201, 105, delegate { ChooseOverlay(); }, false));

            LabelBox(editor, "Duration (seconds)", 20, 250);
            actionDuration = new NumericUpDown
            {
                Location = new Point(20, 274),
                Size = new Size(150, 28),
                Minimum = 1,
                Maximum = 3600,
                Value = 12,
                BackColor = Card2,
                ForeColor = TextMain
            };
            editor.Controls.Add(actionDuration);

            actionEnabledByDefault = new CheckBox
            {
                Text = "Enabled by default",
                Location = new Point(20, 325),
                Size = new Size(240, 28),
                ForeColor = TextMain
            };
            editor.Controls.Add(actionEnabledByDefault);

            editor.Controls.Add(ButtonAt("SAVE ACTION", 20, 380, 160, delegate { SaveAction(); }, true));
            editor.Controls.Add(ButtonAt("OPEN HTML", 195, 380, 130, delegate { OpenActionOverlay(false); }, false));
            editor.Controls.Add(ButtonAt("PREVIEW", 340, 380, 130, delegate { OpenActionOverlay(true); }, false));
            editor.Controls.Add(ButtonAt("IMPORT ACTION FOLDER", 20, 435, 200, delegate { ImportActionFolder(); }, false));

            editor.Controls.Add(new Label
            {
                Text = "Token pricing is intentionally not authored here. Studio 6.0 lets each model assign the tip amount for every action.",
                Location = new Point(20, 500),
                Size = new Size(455, 64),
                ForeColor = TextMuted
            });

            return page;
        }

        private TabPage BuildThemeTab()
        {
            var page = Page("THEME");
            var card = Group("THEME METADATA / DESIGN TOKENS", 15, 15, 930, 560);
            page.Controls.Add(card);

            themeJson = new TextBox
            {
                Location = new Point(20, 48),
                Size = new Size(890, 430),
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                AcceptsTab = true,
                WordWrap = false,
                BackColor = Card2,
                ForeColor = TextMain,
                Font = new Font("Consolas", 10)
            };
            card.Controls.Add(themeJson);
            card.Controls.Add(ButtonAt("SAVE THEME.JSON", 20, 495, 180, delegate { SaveTheme(); }, true));
            return page;
        }

        private TabPage BuildSpecTab()
        {
            var page = Page("PACK SPEC");
            var card = Group("VERSION 6 SHOW PACK CONTRACT", 15, 15, 930, 560);
            page.Controls.Add(card);
            card.Controls.Add(new Label
            {
                Text =
                    "A Show Pack is portable creative content, not a trusted Windows extension.\r\n\r\n" +
                    "Designer owns:\r\n" +
                    "  • action identity and metadata\r\n" +
                    "  • overlay HTML/CSS/JS content\r\n" +
                    "  • images, sounds, animations and theme assets\r\n" +
                    "  • preview and packaging\r\n\r\n" +
                    "Studio owns:\r\n" +
                    "  • model identity and room lock\r\n" +
                    "  • token prices for actions\r\n" +
                    "  • enabled/disabled show assignments\r\n" +
                    "  • live tip processing, supporter state and OBS runtime\r\n\r\n" +
                    "Safety boundary:\r\n" +
                    "  Imported packs do not receive arbitrary Windows command execution, registry access,\r\n" +
                    "  browser credentials, cookies, API tokens, payment access, or administrator privileges.",
                Location = new Point(22, 52),
                Size = new Size(860, 450),
                ForeColor = TextMain,
                Font = new Font("Segoe UI", 10.5f)
            });
            return page;
        }

        private GroupBox Group(string title, int x, int y, int w, int h)
        {
            return new GroupBox
            {
                Text = title,
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = Card,
                ForeColor = TextMain,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
        }

        private void LabelBox(Control parent, string text, int x, int y)
        {
            parent.Controls.Add(new Label { Text = text, Location = new Point(x, y), Size = new Size(250, 22), ForeColor = TextMuted });
        }

        private TextBox Box(Control parent, string text, int x, int y, int w)
        {
            var box = new TextBox
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, 28),
                BackColor = Card2,
                ForeColor = TextMain,
                BorderStyle = BorderStyle.FixedSingle
            };
            parent.Controls.Add(box);
            return box;
        }

        private Button ButtonAt(string text, int x, int y, int w, EventHandler click, bool primary)
        {
            var button = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, 38),
                BackColor = primary ? Purple : Card2,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = primary ? 0 : 1, BorderColor = Purple },
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.Click += click;
            return button;
        }

        private void LoadDefaults()
        {
            packName.Text = "My StimTake Show Pack";
            packId.Text = "my-show-pack";
            packVersion.Text = "1.0.0";
            themeName.Text = "custom";
            themeJson.Text = "{\r\n  \"name\": \"Custom\",\r\n  \"background\": \"#0E0A1A\",\r\n  \"accent\": \"#8E48FF\",\r\n  \"text\": \"#F5F2FC\"\r\n}\r\n";
            actionSlot.SelectedIndex = 0;
            LoadActionSlot(1);
            UpdateWorkspaceStatus();
        }

        private string CleanPackId()
        {
            string id = (packId.Text ?? "").Trim().ToLowerInvariant();
            id = Regex.Replace(id, "[^a-z0-9_-]+", "-").Trim('-');
            return id.Length > 0 ? id : "my-show-pack";
        }

        private string CurrentWorkspace()
        {
            return Path.Combine(workspaceRoot, CleanPackId());
        }

        private void UpdateWorkspaceStatus()
        {
            if (workspaceStatus == null) return;
            workspaceStatus.Text = "Workspace:\r\n" + CurrentWorkspace();
        }

        private void NewPack()
        {
            if (MessageBox.Show("Start a new pack workspace? Existing files are not deleted.", "StimTake Designer",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            LoadDefaults();
        }

        private void OpenWorkspace()
        {
            SavePackManifest();
            string path = CurrentWorkspace();
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show("Could not open the workspace.\r\n\r\n" + ex.Message); }
        }

        private void SaveAll()
        {
            SavePackManifest();
            SaveTheme();
            SaveAction();
            RefreshActionList();
            MessageBox.Show("Workspace saved.", "StimTake Designer");
        }

        private void SavePackManifest()
        {
            try
            {
                string workspace = CurrentWorkspace();
                Directory.CreateDirectory(workspace);
                Directory.CreateDirectory(Path.Combine(workspace, "actions"));
                Directory.CreateDirectory(Path.Combine(workspace, "theme"));

                string json =
                    "{\r\n" +
                    "  \"schema_version\": 1,\r\n" +
                    "  \"product\": \"StimTake Show Pack\",\r\n" +
                    "  \"name\": \"" + Json(packName.Text) + "\",\r\n" +
                    "  \"id\": \"" + Json(CleanPackId()) + "\",\r\n" +
                    "  \"version\": \"" + Json(packVersion.Text) + "\",\r\n" +
                    "  \"theme\": \"" + Json(themeName.Text) + "\",\r\n" +
                    "  \"max_actions\": 20\r\n" +
                    "}\r\n";
                File.WriteAllText(Path.Combine(workspace, "pack.json"), json, new UTF8Encoding(false));
                UpdateWorkspaceStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Pack manifest could not be saved.\r\n\r\n" + ex.Message, "StimTake Designer");
            }
        }

        private string ActionFolder(int slot)
        {
            return Path.Combine(CurrentWorkspace(), "actions", "action-" + slot.ToString("00"));
        }

        private void SaveAction()
        {
            try
            {
                SavePackManifest();
                int slot = actionSlot.SelectedIndex + 1;
                string folder = ActionFolder(slot);
                Directory.CreateDirectory(folder);

                string overlayName = Path.GetFileName((actionOverlay.Text ?? "").Trim());
                if (overlayName.Length == 0) overlayName = "overlay.html";

                string sourceOverlay = (actionOverlay.Text ?? "").Trim();
                if (File.Exists(sourceOverlay))
                {
                    string destination = Path.Combine(folder, overlayName);
                    if (!String.Equals(Path.GetFullPath(sourceOverlay), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
                        File.Copy(sourceOverlay, destination, true);
                }
                else
                {
                    string destination = Path.Combine(folder, overlayName);
                    if (!File.Exists(destination))
                    {
                        File.WriteAllText(destination, StarterOverlay(actionName.Text), new UTF8Encoding(false));
                    }
                }

                actionOverlay.Text = overlayName;

                string json =
                    "{\r\n" +
                    "  \"schema_version\": 1,\r\n" +
                    "  \"slot\": " + slot + ",\r\n" +
                    "  \"id\": \"" + Json(CleanPackId() + "-action-" + slot.ToString("00")) + "\",\r\n" +
                    "  \"name\": \"" + Json(actionName.Text) + "\",\r\n" +
                    "  \"type\": \"overlay\",\r\n" +
                    "  \"overlay\": \"" + Json(overlayName) + "\",\r\n" +
                    "  \"duration\": " + ((int)actionDuration.Value) + ",\r\n" +
                    "  \"default_enabled\": " + actionEnabledByDefault.Checked.ToString().ToLowerInvariant() + "\r\n" +
                    "}\r\n";
                File.WriteAllText(Path.Combine(folder, "action.json"), json, new UTF8Encoding(false));
                RefreshActionList();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Action could not be saved.\r\n\r\n" + ex.Message, "StimTake Designer");
            }
        }

        private void LoadActionSlot(int slot)
        {
            if (slot < 1 || slot > 20) return;
            string folder = ActionFolder(slot);
            string manifest = Path.Combine(folder, "action.json");

            actionName.Text = "Action " + slot.ToString("00");
            actionOverlay.Text = "overlay.html";
            actionDuration.Value = 12;
            actionEnabledByDefault.Checked = false;

            if (!File.Exists(manifest)) return;
            try
            {
                string json = File.ReadAllText(manifest, Encoding.UTF8);
                string name = JsonString(json, "name");
                string overlay = JsonString(json, "overlay");
                long duration = JsonLong(json, "duration");
                if (name.Length > 0) actionName.Text = name;
                if (overlay.Length > 0) actionOverlay.Text = overlay;
                if (duration >= actionDuration.Minimum && duration <= actionDuration.Maximum) actionDuration.Value = duration;
                actionEnabledByDefault.Checked = Regex.IsMatch(json, "\"default_enabled\"\\s*:\\s*true", RegexOptions.IgnoreCase);
            }
            catch { }
        }

        private void ChooseOverlay()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Choose action overlay HTML";
                dialog.Filter = "HTML overlay (*.html;*.htm)|*.html;*.htm";
                if (dialog.ShowDialog(this) == DialogResult.OK) actionOverlay.Text = dialog.FileName;
            }
        }

        private void OpenActionOverlay(bool preview)
        {
            try
            {
                SaveAction();
                int slot = actionSlot.SelectedIndex + 1;
                string path = Path.Combine(ActionFolder(slot), Path.GetFileName(actionOverlay.Text));
                if (!File.Exists(path))
                {
                    MessageBox.Show("Save the action overlay first.", "StimTake Designer");
                    return;
                }
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show((preview ? "Preview" : "Open HTML") + " failed.\r\n\r\n" + ex.Message, "StimTake Designer");
            }
        }

        private void ImportActionFolder()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose an existing action folder to copy into this slot.";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    int slot = actionSlot.SelectedIndex + 1;
                    string destination = ActionFolder(slot);
                    Directory.CreateDirectory(destination);
                    CopyDirectory(dialog.SelectedPath, destination);
                    LoadActionSlot(slot);
                    RefreshActionList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("The action folder could not be imported.\r\n\r\n" + ex.Message, "StimTake Designer");
                }
            }
        }

        private void SaveTheme()
        {
            try
            {
                SavePackManifest();
                string folder = Path.Combine(CurrentWorkspace(), "theme");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "theme.json"), themeJson.Text ?? "{}", new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Theme could not be saved.\r\n\r\n" + ex.Message, "StimTake Designer");
            }
        }

        private void RefreshActionList()
        {
            if (actionList == null) return;
            actionList.Items.Clear();
            for (int slot = 1; slot <= 20; slot++)
            {
                string manifest = Path.Combine(ActionFolder(slot), "action.json");
                string name = "Empty";
                string ready = "No";
                if (File.Exists(manifest))
                {
                    try
                    {
                        string json = File.ReadAllText(manifest, Encoding.UTF8);
                        string parsed = JsonString(json, "name");
                        if (parsed.Length > 0) name = parsed;
                        string overlay = JsonString(json, "overlay");
                        ready = overlay.Length > 0 && File.Exists(Path.Combine(ActionFolder(slot), overlay)) ? "Yes" : "Needs file";
                    }
                    catch { ready = "Invalid"; }
                }
                var item = new ListViewItem(slot.ToString("00"));
                item.SubItems.Add(name);
                item.SubItems.Add(ready);
                actionList.Items.Add(item);
            }
        }

        private List<string> ValidatePack(bool showResult)
        {
            ShowPackValidation validation = ShowPackValidator.ValidateDirectory(CurrentWorkspace());
            var errors = new List<string>(validation.Errors);

            if (showResult)
            {
                if (errors.Count == 0)
                    MessageBox.Show("Pack validation passed.\r\n\r\nThe same bounded validator will run again when Studio imports the ZIP.",
                        "StimTake Designer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    MessageBox.Show("Pack validation found:\r\n\r\n• " + String.Join("\r\n• ", errors.ToArray()),
                        "StimTake Designer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return errors;
        }

        private void BuildZip()
        {
            SavePackManifest();
            SaveTheme();

            List<string> errors = ValidatePack(false);
            if (errors.Count > 0)
            {
                MessageBox.Show("The pack is not ready to build:\r\n\r\n• " + String.Join("\r\n• ", errors.ToArray()),
                    "StimTake Designer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Build StimTake Show Pack";
                dialog.Filter = "StimTake Show Pack (*.zip)|*.zip";
                dialog.FileName = CleanPackId() + "-" + (packVersion.Text ?? "1.0.0").Trim() + ".zip";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    if (File.Exists(dialog.FileName)) File.Delete(dialog.FileName);
                    ZipFile.CreateFromDirectory(CurrentWorkspace(), dialog.FileName, CompressionLevel.Optimal, false);
                    ShowPackValidation zipValidation = ShowPackValidator.ValidateZip(dialog.FileName);
                    if (!zipValidation.IsValid)
                    {
                        try { File.Delete(dialog.FileName); } catch { }
                        throw new InvalidDataException("The generated ZIP failed final validation: " + String.Join("; ", zipValidation.Errors.ToArray()));
                    }
                    MessageBox.Show(
                        "Validated Show Pack ZIP created:\r\n\r\n" + dialog.FileName + "\r\n\r\nStudio will validate it again before activation.",
                        "StimTake Designer",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("The Show Pack ZIP could not be created.\r\n\r\n" + ex.Message, "StimTake Designer");
                }
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
            {
                string name = Path.GetFileName(file);
                File.Copy(file, Path.Combine(destination, name), true);
            }
            foreach (string folder in Directory.GetDirectories(source))
            {
                string name = Path.GetFileName(folder);
                CopyDirectory(folder, Path.Combine(destination, name));
            }
        }

        private static string StarterOverlay(string name)
        {
            string safe = System.Net.WebUtility.HtmlEncode(String.IsNullOrWhiteSpace(name) ? "StimTake Action" : name.Trim());
            return "<!doctype html>\r\n" +
                   "<html><head><meta charset=\"utf-8\"><title>" + safe + "</title>\r\n" +
                   "<style>html,body{margin:0;width:100%;height:100%;background:transparent;overflow:hidden}" +
                   ".action{font:700 64px Segoe UI,sans-serif;color:white;text-shadow:0 4px 20px #000;" +
                   "display:flex;align-items:center;justify-content:center;width:100vw;height:100vh}</style></head>\r\n" +
                   "<body><div class=\"action\">" + safe + "</div></body></html>\r\n";
        }

        private static string Json(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static string JsonString(string json, string field)
        {
            if (String.IsNullOrWhiteSpace(json) || String.IsNullOrWhiteSpace(field)) return "";
            Match match = Regex.Match(json, "\"" + Regex.Escape(field) + "\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"", RegexOptions.IgnoreCase);
            if (!match.Success) return "";
            string value = match.Groups["value"].Value;
            try { value = Regex.Unescape(value); } catch { }
            return value.Replace("\\\"", "\"").Replace("\\\\", "\\").Trim();
        }

        private static long JsonLong(string json, string field)
        {
            if (String.IsNullOrWhiteSpace(json) || String.IsNullOrWhiteSpace(field)) return 0;
            Match match = Regex.Match(json, "\"" + Regex.Escape(field) + "\"\\s*:\\s*(?<value>-?[0-9]+)", RegexOptions.IgnoreCase);
            long value;
            return match.Success && Int64.TryParse(match.Groups["value"].Value, out value) ? value : 0;
        }
    }
}
