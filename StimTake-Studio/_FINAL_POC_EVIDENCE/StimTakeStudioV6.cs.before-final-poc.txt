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

namespace CreatorCamOverlayKit
{
    internal static partial class Program
    {
        /// <summary>
        /// StimTake Studio 6.0 front-of-house shell.
        ///
        /// This form intentionally shares the existing StaticServer instance.
        /// It does not create a second backend and does not replace the proven
        /// Creator Cam / Backstage implementation.  The old Control Deck remains
        /// available through the Backstage button.
        /// </summary>
        private sealed class StimTakeStudioV6Form : Form
        {
            private static readonly Color Bg = Color.FromArgb(14, 10, 26);
            private static readonly Color Sidebar = Color.FromArgb(20, 14, 36);
            private static readonly Color Card = Color.FromArgb(29, 21, 50);
            private static readonly Color Card2 = Color.FromArgb(35, 25, 59);
            private static readonly Color Purple = Color.FromArgb(142, 72, 255);
            private static readonly Color PurpleSoft = Color.FromArgb(181, 145, 255);
            private static readonly Color Green = Color.FromArgb(64, 220, 151);
            private static readonly Color Red = Color.FromArgb(255, 104, 120);
            private static readonly Color TextMain = Color.FromArgb(245, 242, 252);
            private static readonly Color TextMuted = Color.FromArgb(175, 166, 196);

            private readonly StaticServer server;
            private readonly Action openBackstage;
            private readonly Action exitApplication;
            private readonly string dataFolder;
            private readonly string v6Folder;
            private readonly string modelFile;
            private readonly string pricingFile;
            private readonly string activePackFile;
            private readonly Func<ShowPackValidation, string> activateShowPack;
            private readonly Func<ShowPackAction, bool> triggerShowPackAction;
            private ShowPackValidation activePack;

            private readonly Dictionary<string, long> sessionSupport = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            private int sessionTips;
            private long sessionTokens;

            private Panel content;
            private Label pageTitle;
            private Label modelHeader;
            private Label backendStatus;
            private Label bridgeStatus;
            private Label obsStatus;
            private Label overlayStatus;
            private Label sessionTipsLabel;
            private Label sessionTokensLabel;
            private Label lastTipLabel;
            private Label activePackLabel;
            private ListView topTippers;
            private ListView actionsList;
            private TextBox modelAddress;
            private Label modelStatus;
            private Button saveModelButton;
            private Button deleteModelButton;
            private Label actionHint;
            private ListView historyList;
            private System.Windows.Forms.Timer healthTimer;
            private bool closingFromContext;

            internal bool AllowClose;

            internal StimTakeStudioV6Form(
                StaticServer eventServer,
                Action backstageAction,
                Action exitAction,
                Func<ShowPackValidation, string> packActivation,
                Func<ShowPackAction, bool> showActionTrigger)
            {
                server = eventServer;
                openBackstage = backstageAction;
                exitApplication = exitAction;
                activateShowPack = packActivation;
                triggerShowPackAction = showActionTrigger;

                dataFolder = Path.Combine(LocalDataRoot(), "CreatorCamOverlayKit");
                v6Folder = Path.Combine(LocalDataRoot(), "StimTakeStudioV6");
                modelFile = Path.Combine(dataFolder, "chaturbate-model-address-v1.txt");
                pricingFile = Path.Combine(v6Folder, "action-prices-v6.tsv");
                activePackFile = Path.Combine(v6Folder, "active-show-pack-v6.txt");
                Directory.CreateDirectory(v6Folder);

                Text = "StimTake Studio 6.0";
                Icon = SystemIcons.Application;
                BackColor = Bg;
                ForeColor = TextMain;
                Font = new Font("Segoe UI", 9.5f);
                MinimumSize = new Size(1050, 720);
                ClientSize = new Size(1240, 790);
                StartPosition = FormStartPosition.CenterScreen;

                BuildShell();
                LoadModelState();
                LoadActivePack();
                LoadActionPricing();
                LoadRuntimeState();
                ShowDashboard();

                server.EventPublished += ServerEventPublished;

                healthTimer = new System.Windows.Forms.Timer();
                healthTimer.Interval = 1500;
                healthTimer.Tick += delegate { RefreshHealth(); };
                healthTimer.Start();
                RefreshHealth();

                FormClosing += delegate(object sender, FormClosingEventArgs e)
                {
                    if (!AllowClose && e.CloseReason == CloseReason.UserClosing)
                    {
                        e.Cancel = true;
                        if (!closingFromContext)
                        {
                            closingFromContext = true;
                            BeginInvoke((MethodInvoker)delegate
                            {
                                try { exitApplication(); }
                                finally { closingFromContext = false; }
                            });
                        }
                    }
                };

                Disposed += delegate
                {
                    try { server.EventPublished -= ServerEventPublished; } catch { }
                    if (healthTimer != null) healthTimer.Dispose();
                };
            }

            private void BuildShell()
            {
                var side = new Panel { Dock = DockStyle.Left, Width = 205, BackColor = Sidebar, Padding = new Padding(14) };
                Controls.Add(side);

                var brand = new Label
                {
                    Text = "STIMTAKE",
                    ForeColor = PurpleSoft,
                    Font = new Font("Segoe UI", 19, FontStyle.Bold),
                    AutoSize = false,
                    Height = 42,
                    Dock = DockStyle.Top,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                side.Controls.Add(brand);

                var sub = new Label
                {
                    Text = "STUDIO 6.0\nMODEL APP",
                    ForeColor = TextMuted,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    AutoSize = false,
                    Height = 52,
                    Dock = DockStyle.Top
                };
                side.Controls.Add(sub);

                AddNavButton(side, "Dashboard", 118, delegate { ShowDashboard(); });
                AddNavButton(side, "Show Actions", 164, delegate { ShowActions(); });
                AddNavButton(side, "Top Tippers", 210, delegate { ShowTopTippersPage(); });
                AddNavButton(side, "My Model", 256, delegate { ShowModelPage(); });
                AddNavButton(side, "History", 302, delegate { ShowHistory(); });

                var backstage = NavButton("Backstage", delegate { openBackstage(); });
                backstage.Location = new Point(14, 585);
                backstage.Size = new Size(176, 38);
                side.Controls.Add(backstage);

                var note = new Label
                {
                    Text = "Advanced/manual tools",
                    ForeColor = TextMuted,
                    Location = new Point(18, 628),
                    Size = new Size(170, 32),
                    Font = new Font("Segoe UI", 8)
                };
                side.Controls.Add(note);

                var top = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = Bg, Padding = new Padding(26, 12, 25, 0) };
                Controls.Add(top);

                pageTitle = new Label
                {
                    Text = "Dashboard",
                    Dock = DockStyle.Left,
                    Width = 430,
                    ForeColor = TextMain,
                    Font = new Font("Segoe UI", 20, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                top.Controls.Add(pageTitle);

                modelHeader = new Label
                {
                    Text = "No model saved",
                    Dock = DockStyle.Right,
                    Width = 390,
                    ForeColor = PurpleSoft,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleRight
                };
                top.Controls.Add(modelHeader);

                content = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Bg, Padding = new Padding(22, 12, 22, 22) };
                Controls.Add(content);
                content.BringToFront();
            }

            private void AddNavButton(Control parent, string text, int y, Action action)
            {
                var button = NavButton(text, action);
                button.Location = new Point(14, y);
                button.Size = new Size(176, 38);
                parent.Controls.Add(button);
            }

            private Button NavButton(string text, Action action)
            {
                var b = new Button
                {
                    Text = text,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Card,
                    ForeColor = TextMain,
                    FlatAppearance = { BorderSize = 0 },
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                b.Click += delegate { action(); };
                return b;
            }

            private Panel CardPanel(string title, int x, int y, int w, int h)
            {
                var panel = new Panel
                {
                    Location = new Point(x, y),
                    Size = new Size(w, h),
                    BackColor = Card,
                    Padding = new Padding(18)
                };
                var label = new Label
                {
                    Text = title.ToUpperInvariant(),
                    Location = new Point(18, 14),
                    Size = new Size(w - 36, 25),
                    ForeColor = TextMuted,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold)
                };
                panel.Controls.Add(label);
                return panel;
            }

            private Label BigValue(string text, int x, int y, int w)
            {
                return new Label
                {
                    Text = text,
                    Location = new Point(x, y),
                    Size = new Size(w, 42),
                    ForeColor = TextMain,
                    Font = new Font("Segoe UI", 22, FontStyle.Bold)
                };
            }

            private Label StatusValue(string text, int x, int y, int w)
            {
                return new Label
                {
                    Text = text,
                    Location = new Point(x, y),
                    Size = new Size(w, 26),
                    ForeColor = Green,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold)
                };
            }

            private Button PrimaryButton(string text, int x, int y, int w, EventHandler click)
            {
                var b = new Button
                {
                    Text = text,
                    Location = new Point(x, y),
                    Size = new Size(w, 38),
                    BackColor = Purple,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    FlatAppearance = { BorderSize = 0 },
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                b.Click += click;
                return b;
            }

            private Button SecondaryButton(string text, int x, int y, int w, EventHandler click)
            {
                var b = PrimaryButton(text, x, y, w, click);
                b.BackColor = Card2;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = Purple;
                return b;
            }

            private void ClearContent(string title)
            {
                pageTitle.Text = title;
                content.SuspendLayout();
                content.Controls.Clear();
                content.ResumeLayout();
            }

            private void ShowDashboard()
            {
                ClearContent("Dashboard");

                var health = CardPanel("System Status", 0, 0, 500, 196);
                backendStatus = StatusValue("● Backend  RUNNING", 18, 52, 450);
                bridgeStatus = StatusValue("● Chrome Bridge  WAITING FOR TIP", 18, 82, 450);
                overlayStatus = StatusValue("● Overlay Server  RUNNING", 18, 112, 450);
                obsStatus = StatusValue("● OBS  NOT CHECKED", 18, 142, 450);
                health.Controls.Add(backendStatus);
                health.Controls.Add(bridgeStatus);
                health.Controls.Add(overlayStatus);
                health.Controls.Add(obsStatus);
                content.Controls.Add(health);

                var model = CardPanel("My Model", 520, 0, 470, 196);
                var modelName = new Label
                {
                    Name = "dashboardModelName",
                    Text = CurrentModelName().Length > 0 ? CurrentModelName() : "Not configured",
                    Location = new Point(18, 52),
                    Size = new Size(420, 36),
                    ForeColor = TextMain,
                    Font = new Font("Segoe UI", 18, FontStyle.Bold)
                };
                model.Controls.Add(modelName);
                var modelUrl = new Label
                {
                    Text = CurrentModelAddress().Length > 0 ? CurrentModelAddress() : "Save a Chaturbate model address to lock the show.",
                    Location = new Point(18, 94),
                    Size = new Size(420, 42),
                    ForeColor = TextMuted
                };
                model.Controls.Add(modelUrl);
                model.Controls.Add(SecondaryButton("MODEL SETTINGS", 18, 140, 150, delegate { ShowModelPage(); }));
                content.Controls.Add(model);

                var live = CardPanel("Live Session", 0, 216, 500, 222);
                sessionTipsLabel = BigValue(sessionTips.ToString(), 18, 52, 120);
                sessionTokensLabel = BigValue(sessionTokens.ToString(), 160, 52, 160);
                live.Controls.Add(sessionTipsLabel);
                live.Controls.Add(sessionTokensLabel);
                live.Controls.Add(new Label { Text = "TIPS", Location = new Point(20, 93), Size = new Size(100, 22), ForeColor = TextMuted });
                live.Controls.Add(new Label { Text = "TOKENS", Location = new Point(162, 93), Size = new Size(100, 22), ForeColor = TextMuted });
                lastTipLabel = new Label
                {
                    Text = "Last tip: Waiting for first tip...",
                    Location = new Point(18, 123),
                    Size = new Size(450, 30),
                    ForeColor = PurpleSoft,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold)
                };
                live.Controls.Add(lastTipLabel);
                live.Controls.Add(PrimaryButton("START NEW SESSION", 18, 164, 180, delegate { StartNewSession(); }));
                live.Controls.Add(SecondaryButton("END SESSION", 210, 164, 135, delegate { EndSession(); }));
                content.Controls.Add(live);

                var pack = CardPanel("Active Show Pack", 520, 216, 470, 222);
                activePackLabel = new Label
                {
                    Text = ActivePackDisplay(),
                    Location = new Point(18, 54),
                    Size = new Size(420, 35),
                    ForeColor = TextMain,
                    Font = new Font("Segoe UI", 15, FontStyle.Bold)
                };
                pack.Controls.Add(activePackLabel);
                pack.Controls.Add(new Label
                {
                    Text = "Show Packs define creative actions. You choose the token amount for each action.",
                    Location = new Point(18, 96),
                    Size = new Size(420, 46),
                    ForeColor = TextMuted
                });
                pack.Controls.Add(PrimaryButton("IMPORT SHOW PACK", 18, 158, 180, delegate { ImportShowPack(); }));
                pack.Controls.Add(SecondaryButton("ACTION PRICES", 212, 158, 150, delegate { ShowActions(); }));
                content.Controls.Add(pack);

                var top = CardPanel("Top Tippers / Fans", 0, 458, 990, 245);
                topTippers = BuildSupporterList(18, 48, 954, 180);
                top.Controls.Add(topTippers);
                content.Controls.Add(top);

                RefreshTopTippers();
                RefreshHealth();
            }

            private void ShowActions()
            {
                ClearContent("Show Actions");

                var intro = CardPanel("20 Action Slots", 0, 0, 990, 100);
                intro.Controls.Add(new Label
                {
                    Text = "The Show Pack defines the creative action. The model controls the token price. Pricing is local to this Studio profile.",
                    Location = new Point(18, 48),
                    Size = new Size(940, 38),
                    ForeColor = TextMuted
                });
                content.Controls.Add(intro);

                var panel = CardPanel("Action Pricing", 0, 120, 990, 525);
                actionsList = new ListView
                {
                    Location = new Point(18, 48),
                    Size = new Size(954, 390),
                    View = View.Details,
                    FullRowSelect = true,
                    GridLines = false,
                    BackColor = Card2,
                    ForeColor = TextMain
                };
                actionsList.Columns.Add("SLOT", 70);
                actionsList.Columns.Add("ACTION", 520);
                actionsList.Columns.Add("TOKENS", 150);
                actionsList.Columns.Add("STATE", 150);
                panel.Controls.Add(actionsList);

                panel.Controls.Add(PrimaryButton("EDIT SELECTED PRICE", 18, 452, 190, delegate { EditSelectedActionPrice(); }));
                panel.Controls.Add(SecondaryButton("IMPORT SHOW PACK", 220, 452, 180, delegate { ImportShowPack(); }));
                actionHint = new Label
                {
                    Text = "Pricing is stored by Show Pack ID + action ID. A matching accepted tip triggers the installed action once; duration comes from the pack.",
                    Location = new Point(420, 452),
                    Size = new Size(530, 52),
                    ForeColor = TextMuted
                };
                panel.Controls.Add(actionHint);
                content.Controls.Add(panel);
                RenderActionPricing();
            }

            private void ShowTopTippersPage()
            {
                ClearContent("Top Tippers");

                var mode = CardPanel("Automatic Live Tracking", 0, 0, 990, 100);
                mode.Controls.Add(StatusValue("● LIVE MODE  •  Studio supporter history is the source of truth", 18, 50, 930));
                content.Controls.Add(mode);

                var panel = CardPanel("Lifetime Supporters", 0, 120, 990, 505);
                topTippers = BuildSupporterList(18, 48, 954, 400);
                panel.Controls.Add(topTippers);
                panel.Controls.Add(SecondaryButton("OPEN BACKSTAGE / MANUAL TOOLS", 18, 458, 260, delegate { openBackstage(); }));
                content.Controls.Add(panel);
                RefreshTopTippers();
            }

            private void ShowModelPage()
            {
                ClearContent("My Model");

                var panel = CardPanel("Chaturbate Model Connection", 0, 0, 990, 300);
                panel.Controls.Add(new Label
                {
                    Text = "Model address",
                    Location = new Point(18, 50),
                    Size = new Size(200, 22),
                    ForeColor = TextMuted
                });

                modelAddress = new TextBox
                {
                    Location = new Point(18, 76),
                    Size = new Size(690, 28),
                    BackColor = Card2,
                    ForeColor = TextMain,
                    BorderStyle = BorderStyle.FixedSingle
                };
                panel.Controls.Add(modelAddress);

                saveModelButton = PrimaryButton("SAVE MODEL", 725, 72, 120, delegate { SaveModel(); });
                deleteModelButton = SecondaryButton("CHANGE MODEL", 850, 72, 120, delegate { DeleteModel(); });
                panel.Controls.Add(saveModelButton);
                panel.Controls.Add(deleteModelButton);

                panel.Controls.Add(new Label
                {
                    Text = "Example: https://chaturbate.com/obsidian_stallion/",
                    Location = new Point(18, 115),
                    Size = new Size(650, 24),
                    ForeColor = TextMuted
                });

                modelStatus = StatusValue("Model: NOT SAVED", 18, 154, 900);
                panel.Controls.Add(modelStatus);

                panel.Controls.Add(new Label
                {
                    Text = "Once saved, the model is locked. Use Change Model and confirm before entering a different room. The shared backend rejects every event that does not match this saved model.",
                    Location = new Point(18, 196),
                    Size = new Size(900, 58),
                    ForeColor = TextMuted
                });

                content.Controls.Add(panel);
                LoadModelState();
            }

            private void ShowHistory()
            {
                ClearContent("History");
                var panel = CardPanel("Session Activity", 0, 0, 990, 585);
                historyList = new ListView
                {
                    Location = new Point(18, 48),
                    Size = new Size(954, 475),
                    View = View.Details,
                    FullRowSelect = true,
                    BackColor = Card2,
                    ForeColor = TextMain
                };
                historyList.Columns.Add("TIME", 160);
                historyList.Columns.Add("EVENT", 180);
                historyList.Columns.Add("DETAIL", 570);
                panel.Controls.Add(historyList);
                panel.Controls.Add(new Label
                {
                    Text = "Accepted live tips are persisted by Studio. Wrong-room and duplicate events are logged under the local V6 diagnostics folder.",
                    Location = new Point(18, 532),
                    Size = new Size(930, 34),
                    ForeColor = TextMuted
                });
                content.Controls.Add(panel);
                LoadPersistedHistory();
            }

            private ListView BuildSupporterList(int x, int y, int w, int h)
            {
                var list = new ListView
                {
                    Location = new Point(x, y),
                    Size = new Size(w, h),
                    View = View.Details,
                    FullRowSelect = true,
                    BackColor = Card2,
                    ForeColor = TextMain
                };
                list.Columns.Add("#", 50);
                list.Columns.Add("USERNAME", 430);
                list.Columns.Add("LABEL", 250);
                list.Columns.Add("LIFETIME", 180);
                return list;
            }

            private void RefreshHealth()
            {
                try
                {
                    bool obsOpen = Process.GetProcessesByName("obs64").Length > 0 || Process.GetProcessesByName("obs32").Length > 0;
                    if (backendStatus != null) backendStatus.Text = "● Backend  RUNNING";
                    if (overlayStatus != null) overlayStatus.Text = "● Overlay Server  RUNNING";
                    if (obsStatus != null)
                    {
                        obsStatus.Text = obsOpen ? "● OBS  OPEN" : "● OBS  NOT OPEN";
                        obsStatus.ForeColor = obsOpen ? Green : TextMuted;
                    }
                    if (bridgeStatus != null && bridgeStatus.Text.IndexOf("RECEIVING", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        bool modelReady = CurrentModelName().Length > 0;
                        bridgeStatus.Text = modelReady ? "● Chrome Bridge  WAITING FOR TIP" : "● Chrome Bridge  WAITING FOR MODEL";
                        bridgeStatus.ForeColor = modelReady ? Green : TextMuted;
                    }
                    LoadRuntimeState();
                    RefreshTopTippers();
                }
                catch { }
            }

            private void ServerEventPublished(string type, string payload)
            {
                if (String.Equals(type, "platform-event-diagnostic", StringComparison.OrdinalIgnoreCase))
                {
                    string status = JsonString(payload, "status").ToUpperInvariant();
                    string reason = JsonString(payload, "reason");
                    AddHistory(status.Length > 0 ? status : "DIAGNOSTIC", reason);
                    return;
                }
                if (String.Equals(type, "show-action-triggered", StringComparison.OrdinalIgnoreCase))
                {
                    AddHistory("ACTION", JsonString(payload, "name") + " • slot " + JsonLong(payload, "slot").ToString("00"));
                    return;
                }
                if (!String.Equals(type, "platform-event", StringComparison.OrdinalIgnoreCase)) return;

                string username = JsonString(payload, "username");
                string room = JsonString(payload, "room");
                long amount = JsonLong(payload, "amount");
                if (!Regex.IsMatch(username, "^[A-Za-z0-9_]+$") || amount <= 0) return;

                MethodInvoker apply = delegate
                {
                    LoadRuntimeState();
                    if (bridgeStatus != null)
                    {
                        bridgeStatus.Text = "● Chrome Bridge  RECEIVING";
                        bridgeStatus.ForeColor = Green;
                    }

                    AddHistory("TIP", username + " • " + amount + " tokens" + (room.Length > 0 ? " • " + room : ""));
                    RefreshTopTippers();

                    if (activePack != null && activePack.IsValid)
                    {
                        Dictionary<string, ShowPackPrice> prices = ReadActionPricing();
                        foreach (ShowPackAction action in ShowPackPricing.Matches(activePack, prices, amount))
                        {
                            bool scheduled = triggerShowPackAction != null && triggerShowPackAction(action);
                            if (!scheduled) AddHistory("ACTION ERROR", action.Name + " could not be started.");
                        }
                    }
                };

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

            private void AddHistory(string type, string detail)
            {
                if (historyList == null || historyList.IsDisposed) return;
                MethodInvoker add = delegate
                {
                    var item = new ListViewItem(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    item.SubItems.Add(type);
                    item.SubItems.Add(detail);
                    historyList.Items.Insert(0, item);
                    while (historyList.Items.Count > 200) historyList.Items.RemoveAt(historyList.Items.Count - 1);
                };
                try
                {
                    if (InvokeRequired) BeginInvoke(add); else add();
                }
                catch { }
            }

            private void LoadPersistedHistory()
            {
                if (historyList == null || historyList.IsDisposed) return;
                try
                {
                    string path = Path.Combine(server.RuntimeV6Folder, "tip-history-v6.tsv");
                    if (!File.Exists(path)) return;
                    string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                    int first = Math.Max(0, lines.Length - 200);
                    for (int index = lines.Length - 1; index >= first; index--)
                    {
                        string[] parts = lines[index].Split('\t');
                        if (parts.Length < 5) continue;
                        DateTime at;
                        string displayTime = DateTime.TryParse(parts[0], out at) ? at.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : parts[0];
                        var item = new ListViewItem(displayTime);
                        item.SubItems.Add("TIP");
                        item.SubItems.Add(parts[3] + " • " + parts[4] + " tokens • " + parts[2]);
                        historyList.Items.Add(item);
                    }
                }
                catch { }
            }

            private void LoadRuntimeState()
            {
                StudioRuntimeSnapshot snapshot = server.GetStudioRuntimeSnapshot();
                sessionTips = snapshot.SessionTips;
                sessionTokens = snapshot.SessionTokens;
                sessionSupport.Clear();
                foreach (KeyValuePair<string, long> pair in snapshot.SessionSupport) sessionSupport[pair.Key] = pair.Value;
                if (sessionTipsLabel != null) sessionTipsLabel.Text = sessionTips.ToString();
                if (sessionTokensLabel != null) sessionTokensLabel.Text = sessionTokens.ToString();
                if (lastTipLabel != null)
                {
                    lastTipLabel.Text = snapshot.LastUsername.Length > 0
                        ? "Last tip: " + snapshot.LastUsername + " • " + snapshot.LastAmount + (snapshot.LastAmount == 1 ? " token" : " tokens")
                        : "Last tip: Waiting for first tip...";
                }
            }

            private void StartNewSession()
            {
                if (MessageBox.Show(
                    "Start a new live session?\r\n\r\nThis resets only the V6 session counters. Lifetime supporter history is kept.",
                    "StimTake Studio 6.0",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                server.ResetStudioSession();
                LoadRuntimeState();
                ShowDashboard();
            }

            private void EndSession()
            {
                server.EndStudioSession();
                LoadRuntimeState();
                MessageBox.Show(
                    "Session finalized and saved.\r\n\r\nLifetime supporter history, the locked model, Show Pack, action pricing, and processed event IDs remain preserved.",
                    "StimTake Studio 6.0",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            private string CurrentModelAddress()
            {
                try { return File.Exists(modelFile) ? File.ReadAllText(modelFile, Encoding.UTF8).Trim() : ""; }
                catch { return ""; }
            }

            private string CurrentModelName()
            {
                return ModelName(CurrentModelAddress());
            }

            private static string ModelName(string address)
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

            private static string NormalizeModelAddress(string address)
            {
                string model = ModelName(address);
                return model.Length > 0 ? "https://chaturbate.com/" + model + "/" : "";
            }

            private void LoadModelState()
            {
                string saved = NormalizeModelAddress(CurrentModelAddress());
                string name = ModelName(saved);

                if (modelHeader != null)
                    modelHeader.Text = name.Length > 0 ? name + "  •  LOCKED" : "No model saved";

                if (modelAddress != null)
                {
                    modelAddress.Text = saved;
                    modelAddress.ReadOnly = saved.Length > 0;
                }

                if (modelStatus != null)
                {
                    modelStatus.Text = name.Length > 0 ? "● Model: " + name + "  •  SAVED + LOCKED" : "Model: NOT SAVED";
                    modelStatus.ForeColor = name.Length > 0 ? Green : TextMuted;
                }

                if (saveModelButton != null) saveModelButton.Enabled = saved.Length == 0;
                if (deleteModelButton != null) deleteModelButton.Enabled = saved.Length > 0;
            }

            private void SaveModel()
            {
                if (modelAddress == null) return;
                string normalized = NormalizeModelAddress(modelAddress.Text);
                if (normalized.Length == 0)
                {
                    MessageBox.Show(
                        "Enter a Chaturbate model address like:\r\nhttps://chaturbate.com/obsidian_stallion/",
                        "StimTake Studio 6.0",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(modelFile));
                    File.WriteAllText(modelFile, normalized, new UTF8Encoding(false));
                    LoadModelState();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("The model address could not be saved.\r\n\r\n" + ex.Message, "StimTake Studio 6.0");
                }
            }

            private void DeleteModel()
            {
                if (MessageBox.Show(
                    "Change the saved model connection?\r\n\r\nThis deliberately unlocks the current model so you can enter and save a different Chaturbate room.",
                    "StimTake Studio 6.0",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                try
                {
                    if (File.Exists(modelFile)) File.Delete(modelFile);
                    LoadModelState();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("The saved model could not be deleted.\r\n\r\n" + ex.Message, "StimTake Studio 6.0");
                }
            }

            private void RefreshTopTippers()
            {
                if (topTippers == null || topTippers.IsDisposed) return;

                try
                {
                    string file = Path.Combine(dataFolder, "tippers.tsv");
                    var rows = new List<SupporterRow>();
                    if (File.Exists(file))
                    {
                        foreach (string line in File.ReadAllLines(file))
                        {
                            string[] parts = line.Split('\t');
                            if (parts.Length < 4) continue;
                            long lifetime;
                            if (!Int64.TryParse(parts[3], out lifetime)) lifetime = 0;
                            rows.Add(new SupporterRow(parts[0], parts[1], lifetime));
                        }
                    }

                    rows.Sort(delegate(SupporterRow a, SupporterRow b)
                    {
                        int amount = b.Lifetime.CompareTo(a.Lifetime);
                        return amount != 0 ? amount : StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
                    });

                    topTippers.BeginUpdate();
                    topTippers.Items.Clear();
                    int rank = 1;
                    foreach (SupporterRow row in rows)
                    {
                        var item = new ListViewItem(rank.ToString());
                        item.SubItems.Add(row.Name);
                        item.SubItems.Add(row.Label);
                        item.SubItems.Add(row.Lifetime.ToString());
                        topTippers.Items.Add(item);
                        rank++;
                        if (rank > 20) break;
                    }
                    if (topTippers.Items.Count == 0)
                    {
                        var empty = new ListViewItem("-");
                        empty.SubItems.Add("Waiting for real supporters...");
                        empty.SubItems.Add("");
                        empty.SubItems.Add("");
                        topTippers.Items.Add(empty);
                    }
                    topTippers.EndUpdate();
                }
                catch { }
            }

            private sealed class SupporterRow
            {
                internal readonly string Name;
                internal readonly string Label;
                internal readonly long Lifetime;
                internal SupporterRow(string name, string label, long lifetime)
                {
                    Name = name;
                    Label = label;
                    Lifetime = lifetime;
                }
            }

            private string ActivePackDisplay()
            {
                return activePack != null && activePack.IsValid
                    ? activePack.PackName + "  •  " + activePack.PackVersion
                    : "No Show Pack selected";
            }

            private void LoadActivePack()
            {
                activePack = null;
                try
                {
                    if (!File.Exists(activePackFile)) return;
                    string path = File.ReadAllText(activePackFile, Encoding.UTF8).Trim();
                    if (path.Length == 0 || !Directory.Exists(path)) return;
                    ShowPackValidation validation = ShowPackValidator.ValidateDirectory(path);
                    if (!validation.IsValid) return;
                    validation.InstalledPath = Path.GetFullPath(path);
                    activePack = validation;
                }
                catch { activePack = null; }
            }

            private void ImportShowPack()
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Title = "Import StimTake Show Pack";
                    dialog.Filter = "StimTake Show Pack (*.zip)|*.zip";
                    dialog.Multiselect = false;
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;

                    try
                    {
                        ShowPackValidation installed = ShowPackValidator.InstallZip(dialog.FileName, Path.Combine(v6Folder, "show-packs"));
                        if (!installed.IsValid)
                        {
                            MessageBox.Show(
                                "This Show Pack was rejected before activation:\r\n\r\n• " + String.Join("\r\n• ", installed.Errors.ToArray()),
                                "StimTake Studio 6.0",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                            return;
                        }

                        string activationError = activateShowPack == null ? "Show Pack action activation is unavailable." : activateShowPack(installed);
                        if (activationError.Length > 0)
                        {
                            MessageBox.Show("The Show Pack was validated but its actions could not be activated.\r\n\r\n" + activationError,
                                "StimTake Studio 6.0", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        activePack = installed;
                        File.WriteAllText(activePackFile, installed.InstalledPath, new UTF8Encoding(false));
                        LoadActionPricing();

                        if (activePackLabel != null) activePackLabel.Text = ActivePackDisplay();
                        MessageBox.Show(
                            "Show Pack validated, installed, and activated.\r\n\r\nAssign model token prices under Show Actions. Matching accepted tips will run the corresponding pack action once.",
                            "StimTake Studio 6.0",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("The Show Pack could not be imported.\r\n\r\n" + ex.Message, "StimTake Studio 6.0");
                    }
                }
            }

            private void LoadActionPricing()
            {
                try
                {
                    if (activePack == null || !activePack.IsValid) return;
                    Dictionary<string, ShowPackPrice> values = ShowPackPricing.Read(pricingFile, activePack);
                    ShowPackPricing.Write(pricingFile, activePack, values);
                }
                catch { }
            }

            private Dictionary<string, ShowPackPrice> ReadActionPricing()
            {
                return activePack == null
                    ? new Dictionary<string, ShowPackPrice>(StringComparer.OrdinalIgnoreCase)
                    : ShowPackPricing.Read(pricingFile, activePack);
            }

            private void WriteActionPricing(Dictionary<string, ShowPackPrice> values)
            {
                try
                {
                    if (activePack == null) throw new InvalidOperationException("Import a Show Pack before editing action prices.");
                    ShowPackPricing.Write(pricingFile, activePack, values);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Action pricing could not be saved.\r\n\r\n" + ex.Message, "StimTake Studio 6.0");
                }
            }

            private void RenderActionPricing()
            {
                if (actionsList == null) return;
                LoadActivePack();
                var prices = ReadActionPricing();

                actionsList.Items.Clear();
                for (int i = 1; i <= 20; i++)
                {
                    ShowPackAction action = activePack == null ? null : activePack.Actions.Find(delegate(ShowPackAction candidate) { return candidate.Slot == i; });
                    var item = new ListViewItem(i.ToString("00"));
                    if (action == null)
                    {
                        item.SubItems.Add("Empty");
                        item.SubItems.Add("—");
                        item.SubItems.Add("Not in pack");
                    }
                    else
                    {
                        ShowPackPrice price = prices[action.Id];
                        item.SubItems.Add(action.Name);
                        item.SubItems.Add(price.Tokens.ToString());
                        item.SubItems.Add(price.Enabled ? "Enabled" : "Disabled");
                        item.Tag = action;
                    }
                    actionsList.Items.Add(item);
                }
            }

            private void EditSelectedActionPrice()
            {
                if (actionsList == null || actionsList.SelectedItems.Count == 0)
                {
                    MessageBox.Show("Select an action slot first.", "StimTake Studio 6.0");
                    return;
                }

                ShowPackAction action = actionsList.SelectedItems[0].Tag as ShowPackAction;
                if (action == null)
                {
                    MessageBox.Show("That action slot is empty in the active Show Pack.", "StimTake Studio 6.0");
                    return;
                }
                var prices = ReadActionPricing();
                ShowPackPrice current = prices[action.Id];

                using (var dialog = new Form())
                {
                    dialog.Text = "Action " + action.Slot.ToString("00") + " Pricing";
                    dialog.BackColor = Bg;
                    dialog.ForeColor = TextMain;
                    dialog.Font = Font;
                    dialog.ClientSize = new Size(390, 220);
                    dialog.StartPosition = FormStartPosition.CenterParent;
                    dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dialog.MaximizeBox = false;
                    dialog.MinimizeBox = false;

                    dialog.Controls.Add(new Label { Text = "Tokens required", Location = new Point(24, 28), Size = new Size(150, 22), ForeColor = TextMuted });
                    var tokens = new NumericUpDown
                    {
                        Location = new Point(24, 54),
                        Size = new Size(170, 28),
                        Minimum = 1,
                        Maximum = 1000000,
                        Value = Math.Max(1, current.Tokens),
                        BackColor = Card2,
                        ForeColor = TextMain
                    };
                    dialog.Controls.Add(tokens);

                    var enabled = new CheckBox
                    {
                        Text = "Action enabled for this model",
                        Checked = current.Enabled,
                        Location = new Point(24, 100),
                        Size = new Size(280, 28),
                        ForeColor = TextMain
                    };
                    dialog.Controls.Add(enabled);

                    var save = PrimaryButton("SAVE", 24, 155, 140, delegate
                    {
                        prices[action.Id] = new ShowPackPrice { PackId = activePack.PackId, ActionId = action.Id, Tokens = (int)tokens.Value, Enabled = enabled.Checked };
                        WriteActionPricing(prices);
                        dialog.DialogResult = DialogResult.OK;
                        dialog.Close();
                    });
                    dialog.Controls.Add(save);

                    if (dialog.ShowDialog(this) == DialogResult.OK) RenderActionPricing();
                }
            }

            private static string JsonString(string json, string field)
            {
                if (String.IsNullOrWhiteSpace(json) || String.IsNullOrWhiteSpace(field)) return "";
                Match match = Regex.Match(
                    json,
                    "\"" + Regex.Escape(field) + "\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"",
                    RegexOptions.IgnoreCase);
                if (!match.Success) return "";
                string value = match.Groups["value"].Value;
                try { value = Regex.Unescape(value); } catch { }
                return value.Replace("\\\"", "\"").Replace("\\\\", "\\").Trim();
            }

            private static long JsonLong(string json, string field)
            {
                if (String.IsNullOrWhiteSpace(json) || String.IsNullOrWhiteSpace(field)) return 0;
                Match match = Regex.Match(
                    json,
                    "\"" + Regex.Escape(field) + "\"\\s*:\\s*(?<value>-?[0-9]+)",
                    RegexOptions.IgnoreCase);
                long value;
                return match.Success && Int64.TryParse(match.Groups["value"].Value, out value) ? value : 0;
            }
        }
    }
}
