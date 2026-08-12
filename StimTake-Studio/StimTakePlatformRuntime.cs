using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CreatorCamOverlayKit
{
    internal static partial class Program
    {
        internal sealed class StudioRuntimeSnapshot
        {
            internal bool SessionActive;
            internal int SessionTips;
            internal long SessionTokens;
            internal string LastUsername = "";
            internal long LastAmount;
            internal string LastRoom = "";
            internal string LastEventId = "";
            internal DateTime SessionStartedUtc;
            internal readonly Dictionary<string, long> SessionSupport = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class PlatformTipEvent
        {
            internal string Source;
            internal string Type;
            internal string Room;
            internal string Username;
            internal long Amount;
            internal string EventId;
            internal string Timestamp;

            internal static bool TryParse(string json, out PlatformTipEvent value, out string error)
            {
                value = null;
                error = "";
                try
                {
                    var serializer = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024, RecursionLimit = 16 };
                    var item = serializer.DeserializeObject(json ?? "") as Dictionary<string, object>;
                    if (item == null) { error = "Event body must be one JSON object."; return false; }

                    string source = StringField(item, "source", 64);
                    string type = StringField(item, "type", 32);
                    string room = StringField(item, "room", 64).Trim('/').ToLowerInvariant();
                    string username = StringField(item, "username", 64);
                    string eventId = StringField(item, "event_id", 200);
                    string timestamp = StringField(item, "timestamp", 80, true);
                    long amount;
                    object rawAmount;
                    if (!item.TryGetValue("amount", out rawAmount) || !Int64.TryParse(Convert.ToString(rawAmount, System.Globalization.CultureInfo.InvariantCulture), out amount))
                    {
                        error = "amount must be a positive integer.";
                        return false;
                    }

                    if (!String.Equals(source, "chaturbate-browser", StringComparison.OrdinalIgnoreCase)) { error = "source must be chaturbate-browser."; return false; }
                    if (!String.Equals(type, "tip", StringComparison.OrdinalIgnoreCase)) { error = "type must be tip."; return false; }
                    if (room.Length == 0 || room.Length > 64 || !Regex.IsMatch(room, "^[A-Za-z0-9_]+$")) { error = "room is missing or invalid."; return false; }
                    if (username.Length == 0 || username.Length > 64 || !Regex.IsMatch(username, "^[A-Za-z0-9_]+$")) { error = "username is missing or invalid."; return false; }
                    if (amount <= 0) { error = "amount must be greater than zero."; return false; }
                    if (eventId.Length == 0 || eventId.Length > 200 || Regex.IsMatch(eventId, "[\\r\\n\\t]")) { error = "event_id is missing or invalid."; return false; }

                    value = new PlatformTipEvent
                    {
                        Source = "chaturbate-browser",
                        Type = "tip",
                        Room = room,
                        Username = username,
                        Amount = amount,
                        EventId = eventId,
                        Timestamp = timestamp
                    };
                    return true;
                }
                catch (Exception parseError)
                {
                    error = "Event body contains malformed JSON: " + parseError.Message;
                    return false;
                }
            }

            private static string StringField(Dictionary<string, object> item, string key, int maximum, bool optional = false)
            {
                object raw;
                if (!item.TryGetValue(key, out raw) || !(raw is string))
                {
                    if (optional) return "";
                    return "";
                }
                string value = ((string)raw).Trim();
                return value.Length <= maximum ? value : value.Substring(0, maximum + 1);
            }
        }

        internal sealed partial class StaticServer
        {
            private readonly object platformGate = new object();
            private readonly HashSet<string> processedPlatformEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private string platformDataFolder;
            private string platformV6Folder;
            private string platformModelFile;
            private string platformProcessedFile;
            private string platformLogFile;
            private string platformTipHistoryFile;
            private string platformSessionFile;
            private string platformSessionHistoryFile;
            private string platformTippersFile;
            private StudioRuntimeSnapshot platformSession = new StudioRuntimeSnapshot();
            private long acceptedPlatformCount;
            private long duplicatePlatformCount;
            private long rejectedPlatformCount;

            internal string ModelFilePath { get { return platformModelFile; } }
            internal string RuntimeV6Folder { get { return platformV6Folder; } }

            private void InitializeV6Runtime(string overrideRoot)
            {
                string local = String.IsNullOrWhiteSpace(overrideRoot)
                    ? LocalDataRoot()
                    : Path.GetFullPath(overrideRoot);
                platformDataFolder = Path.Combine(local, "CreatorCamOverlayKit");
                platformV6Folder = Path.Combine(local, "StimTakeStudioV6");
                Directory.CreateDirectory(platformDataFolder);
                Directory.CreateDirectory(platformV6Folder);
                platformModelFile = Path.Combine(platformDataFolder, "chaturbate-model-address-v1.txt");
                platformProcessedFile = Path.Combine(platformV6Folder, "processed-event-ids-v6.txt");
                platformLogFile = Path.Combine(platformV6Folder, "platform-event-log-v6.tsv");
                platformTipHistoryFile = Path.Combine(platformV6Folder, "tip-history-v6.tsv");
                platformSessionFile = Path.Combine(platformV6Folder, "session-state-v6.json");
                platformSessionHistoryFile = Path.Combine(platformV6Folder, "session-history-v6.tsv");
                platformTippersFile = Path.Combine(platformDataFolder, "tippers.tsv");
                LoadProcessedPlatformEvents();
                LoadPlatformSession();
            }

            private void LoadProcessedPlatformEvents()
            {
                try
                {
                    if (!File.Exists(platformProcessedFile)) return;
                    foreach (string line in File.ReadAllLines(platformProcessedFile, Encoding.UTF8))
                    {
                        string id = (line ?? "").Trim();
                        if (id.Length > 0 && id.Length <= 200) processedPlatformEvents.Add(id);
                    }
                }
                catch (Exception error) { LogRuntimeError("Load processed platform event IDs", error); }
            }

            private void LoadPlatformSession()
            {
                platformSession = new StudioRuntimeSnapshot { SessionActive = true, SessionStartedUtc = DateTime.UtcNow };
                try
                {
                    if (!File.Exists(platformSessionFile)) return;
                    var serializer = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024, RecursionLimit = 16 };
                    var root = serializer.DeserializeObject(File.ReadAllText(platformSessionFile, Encoding.UTF8)) as Dictionary<string, object>;
                    if (root == null) return;
                    platformSession.SessionActive = JsonBoolean(root, "session_active", true);
                    platformSession.SessionTips = (int)Math.Max(0, Math.Min(Int32.MaxValue, JsonLong(root, "session_tips")));
                    platformSession.SessionTokens = Math.Max(0, JsonLong(root, "session_tokens"));
                    platformSession.LastUsername = JsonText(root, "last_username", 64);
                    platformSession.LastAmount = Math.Max(0, JsonLong(root, "last_amount"));
                    platformSession.LastRoom = JsonText(root, "last_room", 64);
                    platformSession.LastEventId = JsonText(root, "last_event_id", 200);
                    DateTime started;
                    if (DateTime.TryParse(JsonText(root, "session_started_utc", 80), null, System.Globalization.DateTimeStyles.RoundtripKind, out started)) platformSession.SessionStartedUtc = started.ToUniversalTime();
                    object rawSupport;
                    var support = root.TryGetValue("session_support", out rawSupport) ? rawSupport as Dictionary<string, object> : null;
                    if (support != null) foreach (KeyValuePair<string, object> pair in support)
                    {
                        long amount;
                        if (Regex.IsMatch(pair.Key, "^[A-Za-z0-9_]+$") && Int64.TryParse(Convert.ToString(pair.Value, System.Globalization.CultureInfo.InvariantCulture), out amount) && amount > 0)
                            platformSession.SessionSupport[pair.Key] = amount;
                    }
                }
                catch (Exception error) { LogRuntimeError("Load Studio V6 session state", error); }
            }

            internal StudioRuntimeSnapshot GetStudioRuntimeSnapshot()
            {
                lock (platformGate)
                {
                    var copy = new StudioRuntimeSnapshot
                    {
                        SessionActive = platformSession.SessionActive,
                        SessionTips = platformSession.SessionTips,
                        SessionTokens = platformSession.SessionTokens,
                        LastUsername = platformSession.LastUsername,
                        LastAmount = platformSession.LastAmount,
                        LastRoom = platformSession.LastRoom,
                        LastEventId = platformSession.LastEventId,
                        SessionStartedUtc = platformSession.SessionStartedUtc
                    };
                    foreach (KeyValuePair<string, long> pair in platformSession.SessionSupport) copy.SessionSupport[pair.Key] = pair.Value;
                    return copy;
                }
            }

            internal void ResetStudioSession()
            {
                lock (platformGate)
                {
                    platformSession = new StudioRuntimeSnapshot { SessionActive = true, SessionStartedUtc = DateTime.UtcNow };
                    SavePlatformSession();
                    RecordPlatformLog("SESSION_RESET", null, "Session values reset; processed event IDs and lifetime totals preserved.");
                }
                Publish("session-reset", "{}");
            }

            internal void EndStudioSession()
            {
                lock (platformGate)
                {
                    string line = DateTime.UtcNow.ToString("o") + "\t" + platformSession.SessionStartedUtc.ToString("o") + "\t" + platformSession.SessionTips + "\t" + platformSession.SessionTokens + "\t" + CleanLog(platformSession.LastUsername);
                    File.AppendAllLines(platformSessionHistoryFile, new string[] { line }, new UTF8Encoding(false));
                    platformSession.SessionActive = false;
                    SavePlatformSession();
                    RecordPlatformLog("SESSION_END", null, "Session finalized.");
                }
                NotifyEvolutionEvent("studio-session-ended", GetStudioStatusJson());
            }

            private bool AcceptPlatformEvent(string raw, out string responseStatus, out string responseText)
            {
                PlatformTipEvent tip;
                string error;
                if (!PlatformTipEvent.TryParse(raw, out tip, out error))
                {
                    lock (platformGate) rejectedPlatformCount++;
                    RecordPlatformLog("REJECTED", null, error);
                    NotifyEvolutionEvent("platform-event-diagnostic", "{\"status\":\"rejected\",\"reason\":\"" + ControlDeckForm.Json(error) + "\"}");
                    responseStatus = "422 Unprocessable Entity";
                    responseText = error;
                    return false;
                }

                string locked = ReadLockedModelName();
                if (locked.Length == 0)
                {
                    lock (platformGate) rejectedPlatformCount++;
                    RecordPlatformLog("REJECTED", tip, "No locked model is configured.");
                    NotifyPlatformDiagnostic("rejected", tip, "No locked model is configured.");
                    responseStatus = "409 Conflict";
                    responseText = "Save and lock a Chaturbate model in StimTake Studio first.";
                    return false;
                }
                if (!String.Equals(locked, tip.Room, StringComparison.OrdinalIgnoreCase))
                {
                    lock (platformGate) rejectedPlatformCount++;
                    RecordPlatformLog("REJECTED", tip, "Wrong room; locked model is " + locked + ".");
                    NotifyPlatformDiagnostic("rejected", tip, "Wrong room.");
                    responseStatus = "403 Forbidden";
                    responseText = "Event room does not match the locked model.";
                    return false;
                }

                lock (platformGate)
                {
                    if (processedPlatformEvents.Contains(tip.EventId))
                    {
                        duplicatePlatformCount++;
                        RecordPlatformLog("DUPLICATE", tip, "event_id already consumed.");
                        NotifyPlatformDiagnostic("duplicate", tip, "event_id already consumed.");
                        responseStatus = "204 No Content";
                        responseText = "";
                        return false;
                    }

                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(platformProcessedFile));
                        File.AppendAllLines(platformProcessedFile, new string[] { tip.EventId }, new UTF8Encoding(false));
                        processedPlatformEvents.Add(tip.EventId);
                        ApplyAcceptedTip(tip);
                        acceptedPlatformCount++;
                        RecordPlatformLog("ACCEPTED", tip, "Processed exactly once.");
                    }
                    catch (Exception persistError)
                    {
                        LogRuntimeError("Persist accepted platform event", persistError);
                        responseStatus = "503 Service Unavailable";
                        responseText = "StimTake could not persist the event safely.";
                        return false;
                    }
                }

                Publish("platform-event", raw);
                responseStatus = "204 No Content";
                responseText = "";
                return true;
            }

            private void ApplyAcceptedTip(PlatformTipEvent tip)
            {
                UpdateLifetimeSupporter(tip.Username, tip.Amount);
                if (!platformSession.SessionActive)
                {
                    platformSession.SessionActive = true;
                    platformSession.SessionStartedUtc = DateTime.UtcNow;
                }
                if (platformSession.SessionTips < Int32.MaxValue) platformSession.SessionTips++;
                platformSession.SessionTokens = SaturatingAdd(platformSession.SessionTokens, tip.Amount);
                long current;
                platformSession.SessionSupport.TryGetValue(tip.Username, out current);
                platformSession.SessionSupport[tip.Username] = SaturatingAdd(current, tip.Amount);
                platformSession.LastUsername = tip.Username;
                platformSession.LastAmount = tip.Amount;
                platformSession.LastRoom = tip.Room;
                platformSession.LastEventId = tip.EventId;
                SavePlatformSession();
                string history = DateTime.UtcNow.ToString("o") + "\t" + CleanLog(tip.EventId) + "\t" + CleanLog(tip.Room) + "\t" + CleanLog(tip.Username) + "\t" + tip.Amount;
                File.AppendAllLines(platformTipHistoryFile, new string[] { history }, new UTF8Encoding(false));
            }

            private void UpdateLifetimeSupporter(string username, long amount)
            {
                var rows = new List<string[]>();
                if (File.Exists(platformTippersFile)) foreach (string line in File.ReadAllLines(platformTippersFile, Encoding.UTF8))
                {
                    string[] parts = line.Split('\t');
                    if (parts.Length < 3 || String.IsNullOrWhiteSpace(parts[0])) continue;
                    long total = 0;
                    if (parts.Length > 3) Int64.TryParse(parts[3], out total);
                    rows.Add(new string[] { CleanLog(parts[0]), CleanLog(parts[1]), CleanLog(parts[2]), Math.Max(0, total).ToString() });
                }
                int index = rows.FindIndex(delegate(string[] row) { return String.Equals(row[0], username, StringComparison.OrdinalIgnoreCase); });
                if (index >= 0)
                {
                    long total;
                    if (!Int64.TryParse(rows[index][3], out total)) total = 0;
                    rows[index][3] = SaturatingAdd(total, amount).ToString();
                }
                else rows.Add(new string[] { username, "Supporter", "Bronze", amount.ToString() });
                rows.Sort(delegate(string[] left, string[] right)
                {
                    long leftAmount, rightAmount;
                    if (!Int64.TryParse(left[3], out leftAmount)) leftAmount = 0;
                    if (!Int64.TryParse(right[3], out rightAmount)) rightAmount = 0;
                    int amountOrder = rightAmount.CompareTo(leftAmount);
                    return amountOrder != 0 ? amountOrder : StringComparer.OrdinalIgnoreCase.Compare(left[0], right[0]);
                });
                var output = new List<string>();
                foreach (string[] row in rows) output.Add(String.Join("\t", row));
                File.WriteAllLines(platformTippersFile, output.ToArray(), new UTF8Encoding(false));
            }

            private void SavePlatformSession()
            {
                var support = new List<string>();
                foreach (KeyValuePair<string, long> pair in platformSession.SessionSupport)
                    support.Add("\"" + ControlDeckForm.Json(pair.Key) + "\":" + Math.Max(0, pair.Value));
                string json = "{\r\n" +
                    "  \"schema_version\": 1,\r\n" +
                    "  \"session_active\": " + platformSession.SessionActive.ToString().ToLowerInvariant() + ",\r\n" +
                    "  \"session_started_utc\": \"" + platformSession.SessionStartedUtc.ToString("o") + "\",\r\n" +
                    "  \"session_tips\": " + platformSession.SessionTips + ",\r\n" +
                    "  \"session_tokens\": " + platformSession.SessionTokens + ",\r\n" +
                    "  \"last_username\": \"" + ControlDeckForm.Json(platformSession.LastUsername) + "\",\r\n" +
                    "  \"last_amount\": " + platformSession.LastAmount + ",\r\n" +
                    "  \"last_room\": \"" + ControlDeckForm.Json(platformSession.LastRoom) + "\",\r\n" +
                    "  \"last_event_id\": \"" + ControlDeckForm.Json(platformSession.LastEventId) + "\",\r\n" +
                    "  \"session_support\": {" + String.Join(",", support.ToArray()) + "}\r\n" +
                    "}\r\n";
                File.WriteAllText(platformSessionFile, json, new UTF8Encoding(false));
            }

            internal string GetStudioStatusJson()
            {
                lock (platformGate)
                {
                    var support = new List<string>();
                    foreach (KeyValuePair<string, long> pair in platformSession.SessionSupport)
                        support.Add("\"" + ControlDeckForm.Json(pair.Key) + "\":" + Math.Max(0, pair.Value));
                    return "{\"backend\":\"RUNNING\",\"model\":\"" + ControlDeckForm.Json(ReadLockedModelName()) + "\",\"session_active\":" + platformSession.SessionActive.ToString().ToLowerInvariant() +
                        ",\"session_tips\":" + platformSession.SessionTips + ",\"session_tokens\":" + platformSession.SessionTokens +
                        ",\"last_username\":\"" + ControlDeckForm.Json(platformSession.LastUsername) + "\",\"last_amount\":" + platformSession.LastAmount +
                        ",\"last_room\":\"" + ControlDeckForm.Json(platformSession.LastRoom) + "\",\"last_event_id\":\"" + ControlDeckForm.Json(platformSession.LastEventId) +
                        "\",\"supporters\":{" + String.Join(",", support.ToArray()) + "}" +
                        ",\"accepted\":" + acceptedPlatformCount + ",\"duplicates\":" + duplicatePlatformCount + ",\"rejected\":" + rejectedPlatformCount + "}";
                }
            }

            private string ReadLockedModelName()
            {
                try
                {
                    if (!File.Exists(platformModelFile)) return "";
                    Uri address;
                    if (!Uri.TryCreate(File.ReadAllText(platformModelFile, Encoding.UTF8).Trim(), UriKind.Absolute, out address)) return "";
                    if (!String.Equals(address.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return "";
                    string host = (address.Host ?? "").ToLowerInvariant();
                    if (host != "chaturbate.com" && host != "www.chaturbate.com") return "";
                    string room = (address.AbsolutePath ?? "").Trim('/');
                    return Regex.IsMatch(room, "^[A-Za-z0-9_]+$") ? room.ToLowerInvariant() : "";
                }
                catch { return ""; }
            }

            private void NotifyPlatformDiagnostic(string status, PlatformTipEvent tip, string reason)
            {
                NotifyEvolutionEvent("platform-event-diagnostic", "{\"status\":\"" + ControlDeckForm.Json(status) + "\",\"reason\":\"" + ControlDeckForm.Json(reason) + "\",\"room\":\"" + ControlDeckForm.Json(tip.Room) + "\",\"username\":\"" + ControlDeckForm.Json(tip.Username) + "\",\"amount\":" + tip.Amount + ",\"event_id\":\"" + ControlDeckForm.Json(tip.EventId) + "\"}");
            }

            private void RecordPlatformLog(string status, PlatformTipEvent tip, string reason)
            {
                try
                {
                    string line = DateTime.UtcNow.ToString("o") + "\t" + CleanLog(status) + "\t" + CleanLog(tip == null ? "" : tip.EventId) + "\t" + CleanLog(tip == null ? "" : tip.Room) + "\t" + CleanLog(tip == null ? "" : tip.Username) + "\t" + (tip == null ? "0" : tip.Amount.ToString()) + "\t" + CleanLog(reason);
                    File.AppendAllLines(platformLogFile, new string[] { line }, new UTF8Encoding(false));
                }
                catch (Exception error) { LogRuntimeError("Write platform event diagnostic", error); }
            }

            private static string CleanLog(string value)
            {
                string clean = (value ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ").Trim();
                return clean.Length <= 300 ? clean : clean.Substring(0, 300);
            }

            private static long SaturatingAdd(long current, long amount)
            {
                if (amount <= 0) return Math.Max(0, current);
                return current > Int64.MaxValue - amount ? Int64.MaxValue : current + amount;
            }

            private static string JsonText(Dictionary<string, object> item, string key, int maximum)
            {
                object raw;
                string value = item.TryGetValue(key, out raw) && raw is string ? ((string)raw).Trim() : "";
                return value.Length <= maximum ? value : value.Substring(0, maximum);
            }

            private static long JsonLong(Dictionary<string, object> item, string key)
            {
                object raw;
                long value;
                return item.TryGetValue(key, out raw) && Int64.TryParse(Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture), out value) ? value : 0;
            }

            private static bool JsonBoolean(Dictionary<string, object> item, string key, bool fallback)
            {
                object raw;
                if (!item.TryGetValue(key, out raw)) return fallback;
                if (raw is bool) return (bool)raw;
                bool value;
                return Boolean.TryParse(Convert.ToString(raw), out value) ? value : fallback;
            }
        }
    }
}
