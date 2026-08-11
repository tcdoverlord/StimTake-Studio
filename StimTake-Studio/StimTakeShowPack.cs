using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace StimTakeShared
{
    internal sealed class ShowPackAction
    {
        internal int Slot;
        internal string Id;
        internal string Name;
        internal string Overlay;
        internal int DurationSeconds;
        internal bool DefaultEnabled;
    }

    internal sealed class ShowPackValidation
    {
        internal readonly List<string> Errors = new List<string>();
        internal readonly List<ShowPackAction> Actions = new List<ShowPackAction>();
        internal string PackId = "";
        internal string PackName = "";
        internal string PackVersion = "";
        internal string Theme = "";
        internal string InstalledPath = "";
        internal bool IsValid { get { return Errors.Count == 0; } }
    }

    internal static class ShowPackValidator
    {
        private const int MaximumEntries = 500;
        private const long MaximumArchiveBytes = 100L * 1024L * 1024L;
        private const long MaximumEntryBytes = 32L * 1024L * 1024L;
        private const long MaximumExpandedBytes = 150L * 1024L * 1024L;
        private const int MaximumManifestBytes = 1024 * 1024;
        private static readonly Regex SafePackId = new Regex("^[a-z0-9][a-z0-9_-]{0,63}$", RegexOptions.Compiled);
        private static readonly Regex SafeActionId = new Regex("^[a-z0-9][a-z0-9_-]{0,95}$", RegexOptions.Compiled);
        private static readonly Regex ActionManifestPath = new Regex("^actions/action-(?<slot>0[1-9]|1[0-9]|20)/action\\.json$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex AllowedExtension = new Regex("^\\.(json|html|css|js|mjs|png|jpg|jpeg|gif|webp|svg|wav|mp3|ogg|txt|woff|woff2|ttf|otf|mp4|webm)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        internal static ShowPackValidation ValidateZip(string zipPath)
        {
            var result = new ShowPackValidation();
            if (String.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
            {
                result.Errors.Add("Show Pack ZIP was not found.");
                return result;
            }

            try
            {
                var archiveInfo = new FileInfo(zipPath);
                if (archiveInfo.Length <= 0 || archiveInfo.Length > MaximumArchiveBytes)
                {
                    result.Errors.Add("Show Pack ZIP must be between 1 byte and 100 MB.");
                    return result;
                }

                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
                    long expanded = 0;
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (String.IsNullOrEmpty(entry.Name)) continue;
                        string normalized;
                        if (!TryNormalizeEntry(entry.FullName, out normalized, result.Errors)) continue;
                        if (entries.ContainsKey(normalized))
                        {
                            result.Errors.Add("Duplicate package path: " + normalized);
                            continue;
                        }
                        if (entry.Length < 0 || entry.Length > MaximumEntryBytes)
                            result.Errors.Add("Package file exceeds the 32 MB limit: " + normalized);
                        if (entry.CompressedLength > 0 && entry.Length > Math.Max(10L * 1024L * 1024L, entry.CompressedLength * 200L))
                            result.Errors.Add("Package file has an unsafe compression ratio: " + normalized);
                        expanded = expanded > Int64.MaxValue - entry.Length ? Int64.MaxValue : expanded + entry.Length;
                        entries[normalized] = entry;
                    }

                    if (entries.Count > MaximumEntries) result.Errors.Add("Show Packs may contain at most 500 files.");
                    if (expanded > MaximumExpandedBytes) result.Errors.Add("Expanded Show Pack content exceeds 150 MB.");
                    if (result.Errors.Count > 0) return result;

                    ValidateEntries(
                        new List<string>(entries.Keys),
                        delegate(string path)
                        {
                            ZipArchiveEntry entry = entries[path];
                            if (entry.Length > MaximumManifestBytes) throw new InvalidDataException("Manifest exceeds 1 MB: " + path);
                            using (var reader = new StreamReader(entry.Open(), Encoding.UTF8, true)) return reader.ReadToEnd();
                        },
                        result);
                }
            }
            catch (InvalidDataException error)
            {
                result.Errors.Add("Show Pack ZIP is invalid: " + error.Message);
            }
            catch (Exception error)
            {
                result.Errors.Add("Show Pack ZIP could not be validated: " + error.Message);
            }
            return result;
        }

        internal static ShowPackValidation ValidateDirectory(string rootPath)
        {
            var result = new ShowPackValidation();
            if (String.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                result.Errors.Add("Show Pack workspace was not found.");
                return result;
            }

            try
            {
                string root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                foreach (string directory in Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories))
                {
                    if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                        result.Errors.Add("Reparse points are not allowed in Show Packs: " + directory.Substring(root.Length));
                }

                var paths = new List<string>();
                long expanded = 0;
                foreach (string file in Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories))
                {
                    string full = Path.GetFullPath(file);
                    if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Errors.Add("File escaped the Show Pack workspace: " + file);
                        continue;
                    }
                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    {
                        result.Errors.Add("Reparse points are not allowed in Show Packs: " + full.Substring(root.Length));
                        continue;
                    }
                    string normalized;
                    if (!TryNormalizeEntry(full.Substring(root.Length), out normalized, result.Errors)) continue;
                    var info = new FileInfo(file);
                    if (info.Length > MaximumEntryBytes) result.Errors.Add("Package file exceeds the 32 MB limit: " + normalized);
                    expanded = expanded > Int64.MaxValue - info.Length ? Int64.MaxValue : expanded + info.Length;
                    paths.Add(normalized);
                }

                if (paths.Count > MaximumEntries) result.Errors.Add("Show Packs may contain at most 500 files.");
                if (expanded > MaximumExpandedBytes) result.Errors.Add("Expanded Show Pack content exceeds 150 MB.");
                if (result.Errors.Count > 0) return result;

                ValidateEntries(
                    paths,
                    delegate(string path)
                    {
                        string full = Path.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
                        var info = new FileInfo(full);
                        if (info.Length > MaximumManifestBytes) throw new InvalidDataException("Manifest exceeds 1 MB: " + path);
                        return File.ReadAllText(full, Encoding.UTF8);
                    },
                    result);
            }
            catch (Exception error)
            {
                result.Errors.Add("Show Pack workspace could not be validated: " + error.Message);
            }
            return result;
        }

        internal static ShowPackValidation InstallZip(string zipPath, string installationRoot)
        {
            ShowPackValidation validation = ValidateZip(zipPath);
            if (!validation.IsValid) return validation;

            Directory.CreateDirectory(installationRoot);
            string hash = FileSha256(zipPath).Substring(0, 12).ToLowerInvariant();
            string folderName = SafeFolder(validation.PackId) + "-" + SafeFolder(validation.PackVersion) + "-" + hash;
            string root = Path.GetFullPath(installationRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string destination = Path.GetFullPath(Path.Combine(root, folderName));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                validation.Errors.Add("Show Pack installation path was blocked.");
                return validation;
            }
            if (Directory.Exists(destination))
            {
                validation.InstalledPath = destination;
                return validation;
            }

            string staging = Path.GetFullPath(Path.Combine(root, ".installing-" + Guid.NewGuid().ToString("N")));
            try
            {
                Directory.CreateDirectory(staging);
                string safeStage = staging.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (String.IsNullOrEmpty(entry.Name)) continue;
                        string normalized;
                        var errors = new List<string>();
                        if (!TryNormalizeEntry(entry.FullName, out normalized, errors)) continue;
                        string output = Path.GetFullPath(Path.Combine(safeStage, normalized.Replace('/', Path.DirectorySeparatorChar)));
                        if (!output.StartsWith(safeStage, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsafe extraction path was blocked.");
                        Directory.CreateDirectory(Path.GetDirectoryName(output));
                        using (Stream source = entry.Open())
                        using (var target = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None)) source.CopyTo(target);
                    }
                }

                ShowPackValidation extracted = ValidateDirectory(staging);
                if (!extracted.IsValid)
                {
                    validation.Errors.AddRange(extracted.Errors);
                    return validation;
                }
                Directory.Move(staging, destination);
                validation.InstalledPath = destination;
            }
            catch (Exception error)
            {
                validation.Errors.Add("Show Pack could not be installed safely: " + error.Message);
            }
            finally
            {
                try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
            }
            return validation;
        }

        private static void ValidateEntries(List<string> paths, Func<string, string> readText, ShowPackValidation result)
        {
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
            {
                if (!uniquePaths.Add(path)) result.Errors.Add("Duplicate package path: " + path);
                if (!IsAllowedContentPath(path)) result.Errors.Add("Unexpected or unsafe package path: " + path);
                string extension = Path.GetExtension(path);
                if (!AllowedExtension.IsMatch(extension ?? "")) result.Errors.Add("Executable or unsupported package file was blocked: " + path);
            }
            if (!uniquePaths.Contains("pack.json")) result.Errors.Add("pack.json is missing from the ZIP root.");
            if (!uniquePaths.Contains("theme/theme.json")) result.Errors.Add("theme/theme.json is missing.");
            if (result.Errors.Count > 0) return;

            try
            {
                Dictionary<string, object> pack = ParseObject(readText("pack.json"), "pack.json");
                RequireInteger(pack, "schema_version", 1, 1, "pack.json");
                string product = RequireString(pack, "product", 80, "pack.json");
                if (!String.Equals(product, "StimTake Show Pack", StringComparison.Ordinal)) result.Errors.Add("pack.json product must be 'StimTake Show Pack'.");
                result.PackName = RequireString(pack, "name", 100, "pack.json");
                result.PackId = RequireString(pack, "id", 64, "pack.json").ToLowerInvariant();
                result.PackVersion = RequireString(pack, "version", 32, "pack.json");
                result.Theme = RequireString(pack, "theme", 64, "pack.json");
                int maximum = RequireInteger(pack, "max_actions", 1, 20, "pack.json");
                if (maximum > 20) result.Errors.Add("pack.json max_actions may not exceed 20.");
                if (!SafePackId.IsMatch(result.PackId)) result.Errors.Add("pack.json id must use only lowercase letters, numbers, underscores, or hyphens.");
                if (!Regex.IsMatch(result.PackVersion, "^[A-Za-z0-9][A-Za-z0-9._-]{0,31}$")) result.Errors.Add("pack.json version is invalid.");
                ParseObject(readText("theme/theme.json"), "theme/theme.json");
            }
            catch (Exception error)
            {
                result.Errors.Add(error.Message);
            }

            var slots = new HashSet<int>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
            {
                Match match = ActionManifestPath.Match(path);
                if (!match.Success) continue;
                try
                {
                    int folderSlot = Int32.Parse(match.Groups["slot"].Value);
                    Dictionary<string, object> action = ParseObject(readText(path), path);
                    RequireInteger(action, "schema_version", 1, 1, path);
                    int slot = RequireInteger(action, "slot", 1, 20, path);
                    string id = RequireString(action, "id", 96, path).ToLowerInvariant();
                    string name = RequireString(action, "name", 100, path);
                    string type = RequireString(action, "type", 20, path);
                    string overlay = RequireString(action, "overlay", 80, path).Replace('\\', '/');
                    int duration = RequireInteger(action, "duration", 1, 3600, path);
                    bool defaultEnabled = OptionalBoolean(action, "default_enabled", false, path);
                    if (slot != folderSlot) result.Errors.Add(path + " slot must match its action folder.");
                    if (!slots.Add(slot)) result.Errors.Add("Duplicate action slot: " + slot);
                    if (!SafeActionId.IsMatch(id)) result.Errors.Add(path + " id is invalid.");
                    if (!ids.Add(id)) result.Errors.Add("Duplicate action id: " + id);
                    if (!String.Equals(type, "overlay", StringComparison.OrdinalIgnoreCase)) result.Errors.Add(path + " type must be overlay.");
                    if (!String.Equals(overlay, "overlay.html", StringComparison.OrdinalIgnoreCase)) result.Errors.Add(path + " overlay must be overlay.html.");
                    string expectedOverlay = "actions/action-" + slot.ToString("00") + "/overlay.html";
                    if (!uniquePaths.Contains(expectedOverlay)) result.Errors.Add("Action " + slot.ToString("00") + " overlay.html is missing.");
                    result.Actions.Add(new ShowPackAction { Slot = slot, Id = id, Name = name, Overlay = overlay, DurationSeconds = duration, DefaultEnabled = defaultEnabled });
                }
                catch (Exception error)
                {
                    result.Errors.Add(error.Message);
                }
            }

            result.Actions.Sort(delegate(ShowPackAction left, ShowPackAction right) { return left.Slot.CompareTo(right.Slot); });
            if (result.Actions.Count == 0) result.Errors.Add("Show Pack must contain at least one action.");
            if (result.Actions.Count > 20) result.Errors.Add("Show Packs may contain at most 20 actions.");
        }

        private static bool IsAllowedContentPath(string path)
        {
            if (String.Equals(path, "pack.json", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.Equals(path, "theme/theme.json", StringComparison.OrdinalIgnoreCase)) return true;
            if (Regex.IsMatch(path, "^theme/assets/[A-Za-z0-9_. -]+(?:/[A-Za-z0-9_. -]+)*$", RegexOptions.IgnoreCase)) return true;
            if (Regex.IsMatch(path, "^actions/action-(0[1-9]|1[0-9]|20)/(action\\.json|overlay\\.html)$", RegexOptions.IgnoreCase)) return true;
            if (Regex.IsMatch(path, "^actions/action-(0[1-9]|1[0-9]|20)/assets/[A-Za-z0-9_. -]+(?:/[A-Za-z0-9_. -]+)*$", RegexOptions.IgnoreCase)) return true;
            return false;
        }

        private static bool TryNormalizeEntry(string raw, out string normalized, List<string> errors)
        {
            normalized = (raw ?? "").Replace('\\', '/').Trim();
            if (normalized.Length == 0) return false;
            if (normalized.StartsWith("/", StringComparison.Ordinal) || Path.IsPathRooted(raw ?? "") || normalized.IndexOf(':') >= 0 || normalized.IndexOf('\0') >= 0)
            {
                errors.Add("Absolute package path was blocked: " + raw);
                return false;
            }
            string[] parts = normalized.Split('/');
            foreach (string part in parts)
            {
                if (part.Length == 0 || part == "." || part == "..")
                {
                    errors.Add("Path traversal or an empty path segment was blocked: " + raw);
                    return false;
                }
            }
            return true;
        }

        private static Dictionary<string, object> ParseObject(string json, string label)
        {
            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = MaximumManifestBytes, RecursionLimit = 32 };
                object value = serializer.DeserializeObject(json ?? "");
                var result = value as Dictionary<string, object>;
                if (result == null) throw new InvalidDataException(label + " must contain one JSON object.");
                return result;
            }
            catch (InvalidDataException) { throw; }
            catch (Exception error) { throw new InvalidDataException(label + " contains malformed JSON: " + error.Message); }
        }

        private static string RequireString(Dictionary<string, object> value, string field, int maximumLength, string label)
        {
            object raw;
            string text = value.TryGetValue(field, out raw) && raw is string ? ((string)raw).Trim() : "";
            if (text.Length == 0 || text.Length > maximumLength) throw new InvalidDataException(label + " field '" + field + "' is missing or too long.");
            return text;
        }

        private static int RequireInteger(Dictionary<string, object> value, string field, int minimum, int maximum, string label)
        {
            object raw;
            int number;
            if (!value.TryGetValue(field, out raw) || !Int32.TryParse(Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture), out number) || number < minimum || number > maximum)
                throw new InvalidDataException(label + " field '" + field + "' must be an integer from " + minimum + " to " + maximum + ".");
            return number;
        }

        private static bool OptionalBoolean(Dictionary<string, object> value, string field, bool fallback, string label)
        {
            object raw;
            if (!value.TryGetValue(field, out raw)) return fallback;
            if (raw is bool) return (bool)raw;
            bool parsed;
            if (Boolean.TryParse(Convert.ToString(raw), out parsed)) return parsed;
            throw new InvalidDataException(label + " field '" + field + "' must be true or false.");
        }

        private static string SafeFolder(string value)
        {
            string safe = Regex.Replace((value ?? "").ToLowerInvariant(), "[^a-z0-9._-]+", "-").Trim('-', '.');
            return safe.Length > 0 ? safe : "show-pack";
        }

        private static string FileSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
            {
                byte[] bytes = hash.ComputeHash(stream);
                var text = new StringBuilder(bytes.Length * 2);
                foreach (byte value in bytes) text.Append(value.ToString("x2"));
                return text.ToString();
            }
        }
    }

    internal sealed class ShowPackPrice
    {
        internal string PackId;
        internal string ActionId;
        internal int Tokens;
        internal bool Enabled;
    }

    internal static class ShowPackPricing
    {
        internal static Dictionary<string, ShowPackPrice> Read(string path, ShowPackValidation pack)
        {
            var values = new Dictionary<string, ShowPackPrice>(StringComparer.OrdinalIgnoreCase);
            if (pack == null) return values;
            try
            {
                if (File.Exists(path)) foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string[] parts = line.Split('\t');
                    int tokens;
                    bool enabled;
                    if (parts.Length < 4 || !String.Equals(parts[0], pack.PackId, StringComparison.OrdinalIgnoreCase) || !Int32.TryParse(parts[2], out tokens) || tokens <= 0 || !Boolean.TryParse(parts[3], out enabled)) continue;
                    values[parts[1]] = new ShowPackPrice { PackId = parts[0], ActionId = parts[1], Tokens = tokens, Enabled = enabled };
                }
            }
            catch { }
            foreach (ShowPackAction action in pack.Actions)
            {
                if (!values.ContainsKey(action.Id))
                    values[action.Id] = new ShowPackPrice { PackId = pack.PackId, ActionId = action.Id, Tokens = action.Slot * 5, Enabled = action.DefaultEnabled };
            }
            return values;
        }

        internal static void Write(string path, ShowPackValidation pack, Dictionary<string, ShowPackPrice> current)
        {
            if (pack == null) throw new ArgumentNullException("pack");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var retained = new List<string>();
            if (File.Exists(path)) foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
            {
                string[] parts = line.Split('\t');
                if (parts.Length >= 4 && !String.Equals(parts[0], pack.PackId, StringComparison.OrdinalIgnoreCase)) retained.Add(line);
            }
            foreach (ShowPackAction action in pack.Actions)
            {
                ShowPackPrice price;
                if (!current.TryGetValue(action.Id, out price)) price = new ShowPackPrice { PackId = pack.PackId, ActionId = action.Id, Tokens = action.Slot * 5, Enabled = action.DefaultEnabled };
                retained.Add(pack.PackId + "\t" + action.Id + "\t" + Math.Max(1, price.Tokens) + "\t" + price.Enabled);
            }
            File.WriteAllLines(path, retained.ToArray(), new UTF8Encoding(false));
        }

        internal static List<ShowPackAction> Matches(ShowPackValidation pack, Dictionary<string, ShowPackPrice> prices, long amount)
        {
            var matches = new List<ShowPackAction>();
            if (pack == null || prices == null || amount <= 0 || amount > Int32.MaxValue) return matches;
            foreach (ShowPackAction action in pack.Actions)
            {
                ShowPackPrice price;
                if (prices.TryGetValue(action.Id, out price) && price.Enabled && price.Tokens == (int)amount) matches.Add(action);
            }
            return matches;
        }
    }
}
