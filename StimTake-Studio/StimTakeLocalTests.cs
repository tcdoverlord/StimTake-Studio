using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using StimTakeShared;

namespace CreatorCamOverlayKit
{
    internal static class StimTakeLocalTests
    {
        private static int passed;

        [STAThread]
        private static int Main()
        {
            string root = Path.Combine(Path.GetTempPath(), "StimTake-V6-Tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                TestShowPacks(root);
                TestPlatformRuntime(root);
                Console.WriteLine("PASS: " + passed + " local assertions");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("FAIL: " + error.Message);
                Console.Error.WriteLine(error.StackTrace);
                return 1;
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
            }
        }

        private static void TestShowPacks(string testRoot)
        {
            string one = Path.Combine(testRoot, "one-action");
            WritePack(one, "one-action", 1);
            ShowPackValidation oneValidation = ShowPackValidator.ValidateDirectory(one);
            Assert(oneValidation.IsValid, "one-action workspace validates: " + String.Join("; ", oneValidation.Errors.ToArray()));
            Assert(oneValidation.Actions.Count == 1, "one-action workspace has one action");

            string twenty = Path.Combine(testRoot, "twenty-actions");
            WritePack(twenty, "twenty-actions", 20);
            ShowPackValidation twentyValidation = ShowPackValidator.ValidateDirectory(twenty);
            Assert(twentyValidation.IsValid && twentyValidation.Actions.Count == 20, "20-action workspace validates");

            string twentyOne = Path.Combine(testRoot, "twenty-one-actions");
            WritePack(twentyOne, "twenty-one-actions", 21);
            Assert(!ShowPackValidator.ValidateDirectory(twentyOne).IsValid, "action 21 is rejected");

            string malformed = Path.Combine(testRoot, "malformed-pack");
            WritePack(malformed, "malformed-pack", 1);
            File.WriteAllText(Path.Combine(malformed, "pack.json"), "{ not json", Encoding.UTF8);
            Assert(!ShowPackValidator.ValidateDirectory(malformed).IsValid, "malformed pack.json is rejected");

            string duplicateIds = Path.Combine(testRoot, "duplicate-action-ids");
            WritePack(duplicateIds, "duplicate-action-ids", 2);
            string duplicateManifest = File.ReadAllText(Path.Combine(duplicateIds, "actions", "action-02", "action.json"), Encoding.UTF8)
                .Replace("duplicate-action-ids-action-02", "duplicate-action-ids-action-01");
            File.WriteAllText(Path.Combine(duplicateIds, "actions", "action-02", "action.json"), duplicateManifest, new UTF8Encoding(false));
            Assert(!ShowPackValidator.ValidateDirectory(duplicateIds).IsValid, "duplicate action IDs are rejected");

            string executable = Path.Combine(testRoot, "executable-pack");
            WritePack(executable, "executable-pack", 1);
            string executableAsset = Path.Combine(executable, "theme", "assets", "payload.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(executableAsset));
            File.WriteAllBytes(executableAsset, new byte[] { 77, 90 });
            Assert(!ShowPackValidator.ValidateDirectory(executable).IsValid, "executable payload is rejected");

            string oneZip = Path.Combine(testRoot, "one-action.zip");
            ZipFile.CreateFromDirectory(one, oneZip, CompressionLevel.Optimal, false);
            ShowPackValidation zipValidation = ShowPackValidator.ValidateZip(oneZip);
            Assert(zipValidation.IsValid, "Designer-style ZIP validates");
            ShowPackValidation installed = ShowPackValidator.InstallZip(oneZip, Path.Combine(testRoot, "installed"));
            Assert(installed.IsValid && Directory.Exists(installed.InstalledPath), "validated ZIP installs to a bounded folder");
            Assert(File.Exists(Path.Combine(installed.InstalledPath, "actions", "action-01", "overlay.html")), "installed action overlay exists");
            TestActionActivation(testRoot, installed);

            string traversalZip = Path.Combine(testRoot, "traversal.zip");
            File.Copy(oneZip, traversalZip);
            using (var archive = ZipFile.Open(traversalZip, ZipArchiveMode.Update))
            {
                ZipArchiveEntry escape = archive.CreateEntry("../escape.txt");
                using (var writer = new StreamWriter(escape.Open())) writer.Write("blocked");
            }
            Assert(!ShowPackValidator.ValidateZip(traversalZip).IsValid, "ZIP path traversal is rejected");
            Assert(!File.Exists(Path.Combine(testRoot, "escape.txt")), "traversal entry was never extracted");

            string absoluteZip = Path.Combine(testRoot, "absolute-path.zip");
            File.Copy(oneZip, absoluteZip);
            using (var archive = ZipFile.Open(absoluteZip, ZipArchiveMode.Update))
            {
                ZipArchiveEntry absolute = archive.CreateEntry("C:/escape.txt");
                using (var writer = new StreamWriter(absolute.Open())) writer.Write("blocked");
            }
            Assert(!ShowPackValidator.ValidateZip(absoluteZip).IsValid, "absolute ZIP paths are rejected");

            string pricingPath = Path.Combine(testRoot, "pricing.tsv");
            Dictionary<string, ShowPackPrice> prices = ShowPackPricing.Read(pricingPath, oneValidation);
            ShowPackAction first = oneValidation.Actions[0];
            prices[first.Id].MinTokens = 20;
            prices[first.Id].MaxTokens = 29;
            prices[first.Id].Enabled = true;
            ShowPackPricing.Write(pricingPath, oneValidation, prices);
            Dictionary<string, ShowPackPrice> reloaded = ShowPackPricing.Read(pricingPath, oneValidation);
            Assert(reloaded[first.Id].MinTokens == 20 && reloaded[first.Id].MaxTokens == 29 && reloaded[first.Id].Enabled,
                "model action token range persists separately");
            Assert(ShowPackPricing.Matches(oneValidation, reloaded, 20).Count == 1, "range minimum matches enabled action");
            Assert(ShowPackPricing.Matches(oneValidation, reloaded, 25).Count == 1, "amount inside range matches enabled action");
            Assert(ShowPackPricing.Matches(oneValidation, reloaded, 29).Count == 1, "range maximum matches enabled action");
            Assert(ShowPackPricing.Matches(oneValidation, reloaded, 19).Count == 0, "amount below range triggers no action");
            Assert(ShowPackPricing.Matches(oneValidation, reloaded, 30).Count == 0, "amount above range triggers no action");
            reloaded[first.Id].Enabled = false;
            Assert(ShowPackPricing.Matches(oneValidation, reloaded, 25).Count == 0, "disabled action does not trigger");

            string twoRanges = Path.Combine(testRoot, "two-ranges");
            WritePack(twoRanges, "two-ranges", 2);
            ShowPackValidation twoValidation = ShowPackValidator.ValidateDirectory(twoRanges);
            Dictionary<string, ShowPackPrice> overlapping = ShowPackPricing.Read(Path.Combine(testRoot, "two-ranges.tsv"), twoValidation);
            overlapping[twoValidation.Actions[0].Id].MinTokens = 1;
            overlapping[twoValidation.Actions[0].Id].MaxTokens = 10;
            overlapping[twoValidation.Actions[0].Id].Enabled = true;
            overlapping[twoValidation.Actions[1].Id].MinTokens = 10;
            overlapping[twoValidation.Actions[1].Id].MaxTokens = 20;
            overlapping[twoValidation.Actions[1].Id].Enabled = true;
            string overlapError;
            Assert(!ShowPackPricing.ValidateNoOverlap(twoValidation, overlapping, out overlapError) && overlapError.Length > 0,
                "overlapping enabled action ranges are rejected");
            overlapping[twoValidation.Actions[1].Id].MinTokens = 12;
            Assert(ShowPackPricing.ValidateNoOverlap(twoValidation, overlapping, out overlapError),
                "gaps between enabled action ranges are allowed");

            string changed = Path.Combine(testRoot, "changed-action-id");
            WritePack(changed, "one-action", 1, "replacement-action-01");
            ShowPackValidation changedValidation = ShowPackValidator.ValidateDirectory(changed);
            Dictionary<string, ShowPackPrice> changedPrices = ShowPackPricing.Read(pricingPath, changedValidation);
            Assert(changedPrices["replacement-action-01"].MinTokens == 1 && changedPrices["replacement-action-01"].MaxTokens == 4 && !changedPrices["replacement-action-01"].Enabled,
                "changed action ID does not inherit unrelated old pricing");
        }

        private static void TestActionActivation(string testRoot, ShowPackValidation installed)
        {
            string previousRoot = Environment.GetEnvironmentVariable("STIMTAKE_RUNTIME_ROOT");
            string activationRoot = Path.Combine(testRoot, "activation-runtime");
            Environment.SetEnvironmentVariable("STIMTAKE_RUNTIME_ROOT", activationRoot);
            var server = new Program.StaticServer(18788, activationRoot);
            Program.ControlDeckForm deck = null;
            int triggers = 0;
            int stops = 0;
            int showEvents = 0;
            string showPayload = "";
            server.EventPublished += delegate(string type, string payload)
            {
                if (type == "action-trigger") Interlocked.Increment(ref triggers);
                if (type == "module-action" && payload.Contains("\"action\":\"stop\"")) Interlocked.Increment(ref stops);
                if (type == "show-action-triggered")
                {
                    Interlocked.Increment(ref showEvents);
                    showPayload = payload;
                }
            };
            try
            {
                deck = new Program.ControlDeckForm(server);
                string activationError = deck.ActivateValidatedShowPack(installed);
                Assert(activationError.Length == 0, "validated Show Pack activates in the preserved action engine");
                string managedOverlay = Path.Combine(activationRoot, "CreatorCamOverlayKit", "backupscripts_action_v4", "slot-01", "overlay.html");
                Assert(File.Exists(managedOverlay), "Show Pack action is copied into the managed action slot");
                Assert(deck.TriggerShowPackAction(installed.Actions[0]), "installed Show Pack action schedules successfully");
                DateTime until = DateTime.UtcNow.AddMilliseconds(1700);
                while (DateTime.UtcNow < until)
                {
                    Application.DoEvents();
                    Thread.Sleep(20);
                }
                Assert(triggers == 1, "installed action publishes exactly one action-trigger event");
                Assert(showEvents == 1 && showPayload.Contains("\"url\":\"/external-modules/action-slot-01/overlay.html\"") && showPayload.Contains("\"duration\":1"),
                    "OBS action event contains one managed HTML URL and duration");
                Assert(stops == 1, "Show Pack duration publishes one automatic stop event");
            }
            finally
            {
                if (deck != null)
                {
                    deck.AllowClose = true;
                    deck.Dispose();
                }
                server.Dispose();
                Environment.SetEnvironmentVariable("STIMTAKE_RUNTIME_ROOT", previousRoot);
            }
        }

        private static void TestPlatformRuntime(string testRoot)
        {
            string runtime = Path.Combine(testRoot, "runtime");
            int port = 18787;
            int acceptedEvents = 0;
            var server = new Program.StaticServer(port, runtime);
            File.WriteAllText(Path.Combine(runtime, "CreatorCamOverlayKit", "tippers.tsv"), "Manual Fan\tVIP\tGold\t50\r\n", new UTF8Encoding(false));
            server.EventPublished += delegate(string type, string payload) { if (type == "platform-event") Interlocked.Increment(ref acceptedEvents); };
            server.Start();
            try
            {
                var competitor = new Program.StaticServer(port, Path.Combine(testRoot, "competitor"));
                bool blocked = false;
                try { competitor.Start(); }
                catch (InvalidOperationException) { blocked = true; }
                finally { competitor.Dispose(); }
                Assert(blocked, "a second backend cannot own the same port");

                int status;
                Request(port, PlatformEvent("no-model-1", "obsidian_stallion", "navel72", 67), out status);
                Assert(status == 409 && server.GetStudioRuntimeSnapshot().SessionTips == 0, "events are rejected until a model is saved and locked");
                File.WriteAllText(server.ModelFilePath, "https://chaturbate.com/obsidian_stallion/", new UTF8Encoding(false));
                Request(port, PlatformEvent("wrong-1", "other_model", "navel72", 67), out status);
                Assert(status == 403, "wrong-room event is rejected");
                Assert(server.GetStudioRuntimeSnapshot().SessionTips == 0, "wrong-room event changes no session state");

                Request(port, PlatformEvent("real-1", "obsidian_stallion", "navel72", 67), out status);
                Assert(status == 204, "first valid event is accepted");
                Program.StudioRuntimeSnapshot first = server.GetStudioRuntimeSnapshot();
                Assert(first.SessionTips == 1 && first.SessionTokens == 67 && first.LastUsername == "navel72", "first accepted event updates the correct session state");

                Request(port, PlatformEvent("real-1", "obsidian_stallion", "navel72", 67), out status);
                Assert(status == 204 && server.GetStudioRuntimeSnapshot().SessionTips == 1, "duplicate event_id is idempotently ignored");

                Request(port, PlatformEvent("real-2", "obsidian_stallion", "navel72", 67), out status);
                Program.StudioRuntimeSnapshot repeated = server.GetStudioRuntimeSnapshot();
                Assert(status == 204 && repeated.SessionTips == 2 && repeated.SessionTokens == 134, "same user and amount with a new event_id remains a separate tip");
                Request(port, PlatformEvent("real-3", "obsidian_stallion", "higeva3943", 280), out status);
                Program.StudioRuntimeSnapshot ranked = server.GetStudioRuntimeSnapshot();
                Assert(status == 204 && ranked.SessionSupport["higeva3943"] > ranked.SessionSupport["navel72"],
                    "session supporter totals identify the highest Top Tipper and VIP");
                Assert(acceptedEvents == 3, "only three accepted platform events were published");

                Request(port, "{\"source\":\"chaturbate-browser\",\"type\":\"tip\",\"room\":\"obsidian_stallion\",\"username\":\"navel72\",\"amount\":67}", out status);
                Assert(status == 422 && server.GetStudioRuntimeSnapshot().SessionTips == 3, "missing event_id is rejected without state changes");

                string tippers = File.ReadAllText(Path.Combine(runtime, "CreatorCamOverlayKit", "tippers.tsv"), Encoding.UTF8);
                Assert(tippers.Contains("navel72\tSupporter\tBronze\t134"), "lifetime supporter total is authoritative and not doubled");
                Assert(tippers.Contains("higeva3943\tSupporter\tBronze\t280"), "VIP supporter total is persisted");
                Assert(tippers.Contains("Manual Fan\tVIP\tGold\t50"), "accepted tips preserve unrelated manually managed supporter rows");
                string statusJson = RequestPath(port, "/api/studio-status", out status);
                Assert(status == 200 && statusJson.Contains("\"session_tips\":3") && statusJson.Contains("\"duplicates\":1") &&
                    statusJson.Contains("\"supporters\":{") && statusJson.Contains("\"navel72\":134") && statusJson.Contains("\"higeva3943\":280"),
                    "Studio status endpoint reports diagnostics and authoritative Top Tipper/VIP data");
                string indexHtml = RequestPath(port, "/index.html", out status);
                Assert(status == 200 && indexHtml.Contains("TOP TIPPERS") && indexHtml.Contains("id=\"vip\"") && indexHtml.Contains("id=\"last-tipper\"") &&
                    indexHtml.Contains("id=\"action-layer\"") && !indexHtml.Contains("creator-cam-stage"),
                    "OBS index is the transparent supporter summary plus temporary HTML action layer");
            }
            finally
            {
                server.Dispose();
            }

            Thread.Sleep(150);
            int restartedAccepted = 0;
            var restarted = new Program.StaticServer(port, runtime);
            restarted.EventPublished += delegate(string type, string payload) { if (type == "platform-event") Interlocked.Increment(ref restartedAccepted); };
            restarted.Start();
            try
            {
                int status;
                Assert(restarted.GetStudioRuntimeSnapshot().SessionTips == 3, "session state persists across restart");
                Request(port, PlatformEvent("real-1", "obsidian_stallion", "navel72", 67), out status);
                Assert(status == 204 && restarted.GetStudioRuntimeSnapshot().SessionTips == 3 && restartedAccepted == 0,
                    "processed event_id remains suppressed after restart");

                restarted.ResetStudioSession();
                Assert(restarted.GetStudioRuntimeSnapshot().SessionTips == 0, "Start New Session resets session values");
                Request(port, PlatformEvent("real-2", "obsidian_stallion", "navel72", 67), out status);
                Assert(restarted.GetStudioRuntimeSnapshot().SessionTips == 0, "Start New Session does not clear processed event IDs");
                Request(port, PlatformEvent("real-4", "obsidian_stallion", "navel72", 5), out status);
                Assert(restarted.GetStudioRuntimeSnapshot().SessionTips == 1 && restarted.GetStudioRuntimeSnapshot().SessionTokens == 5,
                    "new event after session reset starts the new session");
                string tippers = File.ReadAllText(Path.Combine(runtime, "CreatorCamOverlayKit", "tippers.tsv"), Encoding.UTF8);
                Assert(tippers.Contains("navel72\tSupporter\tBronze\t139"), "session reset preserves and advances lifetime total");
                restarted.EndStudioSession();
                Assert(!restarted.GetStudioRuntimeSnapshot().SessionActive, "End Session persists an inactive finalized session");
            }
            finally
            {
                restarted.Dispose();
            }
        }

        private static string PlatformEvent(string id, string room, string username, int amount)
        {
            return "{\"type\":\"tip\",\"source\":\"chaturbate-browser\",\"room\":\"" + room + "\",\"username\":\"" + username + "\",\"amount\":" + amount + ",\"message\":\"\",\"event_id\":\"" + id + "\",\"timestamp\":\"2026-08-11T12:00:00Z\"}";
        }

        private static string Request(int port, string json, out int status)
        {
            return RequestPath(port, "/api/platform-event?data=" + Uri.EscapeDataString(json), out status);
        }

        private static string RequestPath(int port, string path, out int status)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + path);
            request.Timeout = 4000;
            request.ReadWriteTimeout = 4000;
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    status = (int)response.StatusCode;
                    using (var reader = new StreamReader(response.GetResponseStream())) return reader.ReadToEnd();
                }
            }
            catch (WebException error)
            {
                HttpWebResponse response = error.Response as HttpWebResponse;
                if (response == null) throw;
                using (response)
                {
                    status = (int)response.StatusCode;
                    using (var reader = new StreamReader(response.GetResponseStream())) return reader.ReadToEnd();
                }
            }
        }

        private static void WritePack(string root, string packId, int actions, string firstActionId = "")
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "theme"));
            File.WriteAllText(Path.Combine(root, "pack.json"),
                "{\"schema_version\":1,\"product\":\"StimTake Show Pack\",\"name\":\"Test Pack\",\"id\":\"" + packId + "\",\"version\":\"1.0.0\",\"theme\":\"test\",\"max_actions\":20}", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root, "theme", "theme.json"), "{\"name\":\"Test\"}", new UTF8Encoding(false));
            for (int slot = 1; slot <= actions; slot++)
            {
                string folder = Path.Combine(root, "actions", "action-" + slot.ToString("00"));
                Directory.CreateDirectory(folder);
                string id = slot == 1 && firstActionId.Length > 0 ? firstActionId : packId + "-action-" + slot.ToString("00");
                File.WriteAllText(Path.Combine(folder, "action.json"),
                    "{\"schema_version\":1,\"slot\":" + slot + ",\"id\":\"" + id + "\",\"name\":\"Action " + slot.ToString("00") + "\",\"type\":\"overlay\",\"overlay\":\"overlay.html\",\"duration\":1,\"default_enabled\":false}", new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(folder, "overlay.html"), "<!doctype html><title>Action</title>", new UTF8Encoding(false));
            }
        }

        private static void Assert(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            passed++;
            Console.WriteLine("PASS: " + label);
        }
    }
}
