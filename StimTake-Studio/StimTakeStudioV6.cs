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
        /// Creator Cam / Backstage implementation.  The legacy Control Deck remains internal to preserve the proven action engine,
        /// but the normal model-facing UI intentionally does not expose Backstage.
        /// </summary>
        private sealed class StimTakeStudioV6Form : Form
        {
            private static readonly Color Bg = Color.FromArgb(11, 16, 24);
            private static readonly Color Sidebar = Color.FromArgb(10, 15, 23);
            private static readonly Color Card = Color.FromArgb(18, 25, 35);
            private static readonly Color Card2 = Color.FromArgb(22, 30, 41);
            private static readonly Color Purple = Color.FromArgb(126, 58, 235);
            private static readonly Color PurpleSoft = Color.FromArgb(171, 103, 255);
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
            private Label vipLabel;
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
                MinimumSize = new Size(1180, 760);
                ClientSize = new Size(1500, 900);
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
                var header = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 72,
                    BackColor = Sidebar,
                    Padding = new Padding(18, 8, 18, 8)
                };
                Controls.Add(header);

                header.Controls.Add(new Label
                {
                    Text = "ST",
                    Location = new Point(18, 12),
                    Size = new Size(56, 46),
                    ForeColor = PurpleSoft,
                    Font = new Font("Segoe UI", 25, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter
                });
                header.Controls.Add(new Label
                {
                    Text = "StimTake Studio v6.0",
                    Location = new Point(82, 10),
                    Size = new Size(350, 28),
                    ForeColor = TextMain,
                    Font = new Font("Segoe UI", 14, FontStyle.Bold)
                });
                header.Controls.Add(new Label
                {
                    Text = "Final Automation Edition",
                    Location = new Point(84, 39),
                    Size = new Size(300, 20),
                    ForeColor = PurpleSoft
                });

                modelHeader = new Label
                {
                    Text = "No model saved",
                    Dock = DockStyle.Right,
                    Width = 430,
                    ForeColor = TextMain,
                    Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleRight
                };
                header.Controls.Add(modelHeader);

                var side = new Panel
                {
                    Dock = DockStyle.Left,
                    Width = 220,
                    BackColor = Sidebar,
                    Padding = new Padding(12, 20, 12, 18)
                };
                Controls.Add(side);

                AddNavButton(side, "DASHBOARD", 24, delegate { ShowDashboard(); });
                AddNavButton(side, "MY ROOM", 78, delegate { ShowModelPage(); });
                AddNavButton(side, "TOP TIPPERS", 132, delegate { ShowTopTippersPage(); });
                AddNavButton(side, "ACTION DECK", 186, delegate { ShowActions(); });
                AddNavButton(side, "HISTORY", 240, delegate { ShowHistory(); });

                var healthBox = new Panel
                {
                    Location = new Point(14, 610),
                    Size = new Size(192, 118),
                    BackColor = Card,
                    Padding = new Padding(12)
                };
                side.Controls.Add(healthBox);
                healthBox.Controls.Add(new Label
                {
                    Text = "SYSTEM HEALTH",
                    Location = new Point(12, 12),
                    Size = new Size(165, 22),
                    ForeColor = TextMuted,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
                });
                healthBox.Controls.Add(new Label
                {
                    Text = "● READY",
                    Location = new Point(12, 44),
                    Size = new Size(165, 26),
                    ForeColor = Green,
                    Font = new Font("Segoe UI", 11, FontStyle.Bold)
                });
                healthBox.Controls.Add(new Label
                {
                    Text = "Studio v6.0",
                    Location = new Point(12, 78),
                    Size = new Size(165, 20),
                    ForeColor = TextMuted
                });

                content = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    BackColor = Bg,
                    Padding = new Padding(20, 12, 20, 24)
                };
                Controls.Add(content);
                content.BringToFront();

                pageTitle = new Label
                {
                    Text = "Dashboard",
                    Location = new Point(2, 0),
                    Size = new Size(1080, 34),
                    ForeColor = TextMain,
                    Font = new Font("Segoe UI", 14, FontStyle.Bold)
                };
                content.Controls.Add(pageTitle);
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
                    BackColor = Sidebar,
                    ForeColor = TextMuted,
                    FlatAppearance = { BorderSize = 0 },
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(18, 0, 0, 0),
                    Cursor = Cursors.Hand
                };
                b.MouseEnter += delegate { b.BackColor = Card; b.ForeColor = PurpleSoft; };
                b.MouseLeave += delegate { b.BackColor = Sidebar; b.ForeColor = TextMuted; };
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
                content.SuspendLayout();
                content.Controls.Clear();
                pageTitle = new Label
                {
                    Text = title.ToUpperInvariant(),
                    Location = new Point(2, 0),
                    Size = new Size(1080, 34),
                    ForeColor = TextMain,
                    Font = new Font("Segoe UI", 14, FontStyle.Bold)
                };
                content.Controls.Add(pageTitle);
                content.ResumeLayout();
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
                        ? "Last Tipper: " + snapshot.LastUsername + " • " + snapshot.LastAmount + (snapshot.LastAmount == 1 ? " token" : " tokens")
                        : "Last Tipper: Waiting...";
                }
            }

            private void ShowDashboard()
            {
                ClearContent("Dashboard");
                int y = 46;

                var model = CardPanel("My Model", 0, y, 300, 182);
                model.Controls.Add(new Label
                {
                    Text = CurrentModelName().Length > 0 ? CurrentModelName() + "  🔒" : "No model configured",
                    Location = new Point(18, 48),
                    Size = new Size(260, 34),
                    ForeColor = PurpleSoft,
                    Font = new Font("Segoe UI", 17, FontStyle.Bold)
                });
                model.Controls.Add(new Label
                {
                    Text = CurrentModelAddress().Length > 0 ? CurrentModelAddress() : "Save one Chaturbate room.",
                    Location = new Point(18, 86),
                    Size = new Size(260, 44),
                    ForeColor = TextMuted
                });
                model.Controls.Add(SecondaryButton("CHANGE MODEL", 18, 134, 145, delegate { ShowModelPage(); }));
                content.Controls.Add(model);

                var status = CardPanel("System Status", 318, y, 690, 182);
                backendStatus = StatusValue("● Backend\nRUNNING", 18, 54, 150);
                bridgeStatus = StatusValue("● Chrome Bridge\nWAITING", 180, 54, 165);
                obsStatus = StatusValue("● OBS\nNOT OPEN", 360, 54, 135);
                overlayStatus = StatusValue("● Overlay\nRUNNING", 510, 54, 150);
                backendStatus.Height = bridgeStatus.Height = obsStatus.Height = overlayStatus.Height = 70;
                status.Controls.Add(backendStatus);
                status.Controls.Add(bridgeStatus);
                status.Controls.Add(obsStatus);
                status.Controls.Add(overlayStatus);
                content.Controls.Add(status);

                var session = CardPanel("Live Session", 0, y + 202, 570, 248);
                sessionTipsLabel = BigValue(sessionTips.ToString(), 18, 54, 120);
                sessionTokensLabel = BigValue(sessionTokens.ToString(), 154, 54, 160);
                session.Controls.Add(sessionTipsLabel);
                session.Controls.Add(sessionTokensLabel);
                session.Controls.Add(new Label { Text = "TIPS", Location = new Point(20, 93), Size = new Size(90, 20), ForeColor = TextMuted });
                session.Controls.Add(new Label { Text = "TOKENS", Location = new Point(156, 93), Size = new Size(90, 20), ForeColor = TextMuted });

                lastTipLabel = new Label
                {
                    Text = "Last Tipper: Waiting...",
                    Location = new Point(18, 122),
                    Size = new Size(520, 28),
                    ForeColor = TextMain,
                    Font = new Font("Segoe UI", 11, FontStyle.Bold)
                };
                session.Controls.Add(lastTipLabel);

                vipLabel = new Label
                {
                    Text = "VIP: Waiting for first supporter...",
                    Location = new Point(18, 153),
                    Size = new Size(520, 28),
                    ForeColor = PurpleSoft,
                    Font = new Font("Segoe UI", 10.5f, FontStyle.Bold)
                };
                session.Controls.Add(vipLabel);

                session.Controls.Add(PrimaryButton("START NEW SESSION", 18, 194, 190, delegate { StartNewSession(); }));
                session.Controls.Add(SecondaryButton("END SESSION", 222, 194, 150, delegate { EndSession(); }));
                content.Controls.Add(session);

                var top = CardPanel("Top Tippers — Session", 590, y + 202, 418, 248);
                topTippers = BuildSupporterList(18, 48, 382, 178);
                top.Controls.Add(topTippers);
                content.Controls.Add(top);

                var actionSummary = CardPanel("Show Actions", 0, y + 470, 1008, 352);
                actionSummary.Controls.Add(new Label
                {
                    Text = "Designer supplies the HTML overlay. You choose the token range and whether each action is ON or OFF.",
                    Location = new Point(18, 45),
                    Size = new Size(950, 26),
                    ForeColor = TextMuted
                });

                actionsList = new ListView
                {
                    Location = new Point(18, 76),
                    Size = new Size(972, 220),
                    View = View.Details,
                    FullRowSelect = true,
                    BackColor = Card2,
                    ForeColor = TextMain
                };
                actionsList.Columns.Add("SLOT", 65);
                actionsList.Columns.Add("ACTION / HTML OVERLAY", 515);
                actionsList.Columns.Add("TIP RANGE", 210);
                actionsList.Columns.Add("STATE", 150);
                actionsList.DoubleClick += delegate { EditSelectedActionPrice(); };
                actionSummary.Controls.Add(actionsList);

                actionSummary.Controls.Add(PrimaryButton("EDIT RANGE / ON-OFF", 18, 306, 190, delegate { EditSelectedActionPrice(); }));
                actionSummary.Controls.Add(SecondaryButton("IMPORT SHOW PACK", 222, 306, 180, delegate { ImportShowPack(); }));

                activePackLabel = new Label
                {
                    Text = "Pack: " + ActivePackDisplay(),
                    Location = new Point(420, 314),
                    Size = new Size(555, 24),
                    ForeColor = PurpleSoft,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
                };
                actionSummary.Controls.Add(activePackLabel);
                content.Controls.Add(actionSummary);

                RefreshTopTippers();
                RenderActionPricing();
                RefreshHealth();
            }

            private void ShowActions()
            {
                ClearContent("Action Deck");

                var intro = CardPanel("20 HTML Overlay Actions", 0, 46, 1008, 100);
                intro.Controls.Add(new Label
                {
                    Text = "Each accepted tip updates the session. At most one enabled range can trigger one HTML overlay. Gaps are allowed; overlaps are blocked.",
                    Location = new Point(18, 48),
                    Size = new Size(960, 38),
                    ForeColor = TextMuted
                });
                content.Controls.Add(intro);

                var panel = CardPanel("Model Trigger Ranges", 0, 166, 1008, 560);
                actionsList = new ListView
                {
                    Location = new Point(18, 48),
                    Size = new Size(972, 420),
                    View = View.Details,
                    FullRowSelect = true,
                    BackColor = Card2,
                    ForeColor = TextMain
                };
                actionsList.Columns.Add("SLOT", 70);
                actionsList.Columns.Add("ACTION / HTML OVERLAY", 520);
                actionsList.Columns.Add("TIP RANGE", 190);
                actionsList.Columns.Add("STATE", 160);
                actionsList.DoubleClick += delegate { EditSelectedActionPrice(); };
                panel.Controls.Add(actionsList);

                panel.Controls.Add(PrimaryButton("EDIT RANGE / ON-OFF", 18, 486, 190, delegate { EditSelectedActionPrice(); }));
                panel.Controls.Add(SecondaryButton("IMPORT SHOW PACK", 222, 486, 180, delegate { ImportShowPack(); }));
                actionHint = new Label
                {
                    Text = "The Designer owns overlay content. The model owns trigger ranges and ON/OFF state.",
                    Location = new Point(420, 490),
                    Size = new Size(555, 44),
                    ForeColor = TextMuted
                };
                panel.Controls.Add(actionHint);
                content.Controls.Add(panel);
                RenderActionPricing();
            }

            private void ShowTopTippersPage()
            {
                ClearContent("Top Tippers");

                var mode = CardPanel("Automatic Session Tracking", 0, 46, 1008, 100);
                mode.Controls.Add(StatusValue("● LIVE MODE  •  Top Tippers and VIP update automatically from accepted real tips", 18, 50, 960));
                content.Controls.Add(mode);

                var panel = CardPanel("Current Session Ranking", 0, 166, 1008, 505);
                topTippers = BuildSupporterList(18, 48, 972, 420);
                panel.Controls.Add(topTippers);
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
                list.Columns.Add("#", 45);
                list.Columns.Add("USERNAME", Math.Max(180, w - 185));
                list.Columns.Add("SESSION TOKENS", 120);
                return list;
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

                var rows = new List<KeyValuePair<string, long>>(sessionSupport);
                rows.Sort(delegate(KeyValuePair<string, long> a, KeyValuePair<string, long> b)
                {
                    int amount = b.Value.CompareTo(a.Value);
                    return amount != 0 ? amount : StringComparer.OrdinalIgnoreCase.Compare(a.Key, b.Key);
                });

                topTippers.BeginUpdate();
                topTippers.Items.Clear();
                int rank = 1;
                foreach (KeyValuePair<string, long> row in rows)
                {
                    var item = new ListViewItem(rank.ToString());
                    item.SubItems.Add(row.Key);
                    item.SubItems.Add(row.Value.ToString());
                    topTippers.Items.Add(item);
                    rank++;
                    if (rank > 20) break;
                }

                if (topTippers.Items.Count == 0)
                {
                    var empty = new ListViewItem("-");
                    empty.SubItems.Add("Waiting for first real tip...");
                    empty.SubItems.Add("");
                    topTippers.Items.Add(empty);
                }
                topTippers.EndUpdate();

                if (vipLabel != null)
                {
                    vipLabel.Text = rows.Count == 0
                        ? "VIP: Waiting for first supporter..."
                        : "VIP: " + rows[0].Key + " • " + rows[0].Value + " tokens this session";
                }
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
                        item.SubItems.Add(price.RangeText);
                        item.SubItems.Add(price.Enabled ? "ON" : "OFF");
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
                    MessageBox.Show("That slot is empty in the active Show Pack.", "StimTake Studio 6.0");
                    return;
                }

                var prices = ReadActionPricing();
                ShowPackPrice current = prices[action.Id];

                using (var dialog = new Form())
                {
                    dialog.Text = "Action " + action.Slot.ToString("00") + " — " + action.Name;
                    dialog.BackColor = Bg;
                    dialog.ForeColor = TextMain;
                    dialog.Font = Font;
                    dialog.ClientSize = new Size(470, 320);
                    dialog.StartPosition = FormStartPosition.CenterParent;
                    dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dialog.MaximizeBox = false;
                    dialog.MinimizeBox = false;

                    dialog.Controls.Add(new Label { Text = "Minimum tokens", Location = new Point(24, 28), Size = new Size(160, 22), ForeColor = TextMuted });
                    var minimum = new NumericUpDown
                    {
                        Location = new Point(24, 54),
                        Size = new Size(180, 28),
                        Minimum = 1,
                        Maximum = 1000000,
                        Value = Math.Max(1, current.MinTokens),
                        BackColor = Card2,
                        ForeColor = TextMain
                    };
                    dialog.Controls.Add(minimum);

                    dialog.Controls.Add(new Label { Text = "Maximum tokens", Location = new Point(240, 28), Size = new Size(160, 22), ForeColor = TextMuted });
                    var maximum = new NumericUpDown
                    {
                        Location = new Point(240, 54),
                        Size = new Size(180, 28),
                        Minimum = 0,
                        Maximum = 1000000,
                        Value = Math.Max(0, current.MaxTokens),
                        BackColor = Card2,
                        ForeColor = TextMain
                    };
                    dialog.Controls.Add(maximum);

                    dialog.Controls.Add(new Label
                    {
                        Text = "Maximum 0 means no upper limit (example: 500+ tokens).",
                        Location = new Point(24, 92),
                        Size = new Size(395, 36),
                        ForeColor = TextMuted
                    });

                    var enabled = new CheckBox
                    {
                        Text = "Action overlay ON",
                        Checked = current.Enabled,
                        Location = new Point(24, 142),
                        Size = new Size(280, 28),
                        ForeColor = TextMain
                    };
                    dialog.Controls.Add(enabled);

                    dialog.Controls.Add(new Label
                    {
                        Text = "When OFF, tips still count toward Last Tipper, Top Tippers and VIP, but this overlay will not play.",
                        Location = new Point(24, 180),
                        Size = new Size(405, 48),
                        ForeColor = TextMuted
                    });

                    var save = PrimaryButton("SAVE", 24, 250, 140, delegate
                    {
                        int min = (int)minimum.Value;
                        int max = (int)maximum.Value;
                        if (max > 0 && max < min)
                        {
                            MessageBox.Show("Maximum must be 0 or greater than/equal to minimum.", "StimTake Studio 6.0");
                            return;
                        }

                        ShowPackPrice previous = prices[action.Id];
                        prices[action.Id] = new ShowPackPrice
                        {
                            PackId = activePack.PackId,
                            ActionId = action.Id,
                            MinTokens = min,
                            MaxTokens = max,
                            Enabled = enabled.Checked
                        };

                        string overlapError;
                        if (!ShowPackPricing.ValidateNoOverlap(activePack, prices, out overlapError))
                        {
                            prices[action.Id] = previous;
                            MessageBox.Show(overlapError + "\r\n\r\nEnabled ranges may not overlap. Gaps are allowed.",
                                "StimTake Studio 6.0", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        WriteActionPricing(prices);
                        dialog.DialogResult = DialogResult.OK;
                        dialog.Close();
                    });
                    dialog.Controls.Add(save);

                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        RenderActionPricing();
                        if (actionsList != null && actionsList.Parent != null) actionsList.Refresh();
                    }
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
