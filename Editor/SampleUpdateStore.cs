using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace BaseUnity.Editor
{
    public enum SampleFileState { Identical, Add, Update, LocalModified, Conflict, LocalDeleted, SourceRemoved, AlreadyRemoved }
    [Serializable] public sealed class SampleBaseline { public string path, hash, content; }
    [Serializable] public sealed class SampleInstallManifest
    {
        public int schema = 1;
        public string destination, installedVersion, lastSourceVersion;
        public List<SampleBaseline> files = new List<SampleBaseline>();
    }
    public sealed class SampleUpdateEntry
    {
        public string path, currentHash, sourceHash, baselineContent, currentContent, sourceContent;
        public SampleFileState state;
        public bool selected;
        public bool IsRemoval => state == SampleFileState.SourceRemoved;
    }
    public sealed class SampleUpdatePlan
    {
        public string source, destination, version, manifestHash;
        public List<SampleUpdateEntry> files = new List<SampleUpdateEntry>();
    }
    [Serializable] public sealed class SampleFileBackup
    { public string path, before, afterHash; public bool existed; }
    [Serializable] public sealed class SampleSceneState { public string path; public bool enabled; }
    [Serializable] public sealed class SampleBackup
    {
        public int schema = 1;
        public string id, destination, createdUtc, description, status;
        public List<SampleFileBackup> files = new List<SampleFileBackup>();
        public bool hasSceneSnapshot;
        public SampleSceneState[] beforeScenes, afterScenes;
    }

    /// <summary>Filesystem-only updater. Paths and stale previews are checked before writes; no Unity assets are generated here.</summary>
    public sealed class SampleUpdateStore
    {
        private readonly string project;
        private static readonly StringComparison PathComparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        public string BackupDirectory => Path.Combine(project, "ProjectSettings/BaseUnity/SampleBackups");
        public SampleUpdateStore(string projectRoot) { project = Path.GetFullPath(projectRoot); }
        private static bool Within(string path, string root) => path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, PathComparison);
        private static void NoLinks(string path)
        {
            var current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked paths are not allowed for sample updates: " + current);
                current = Path.GetDirectoryName(current);
            }
        }
        public string ValidateDestination(string destination)
        {
            destination = Relative(destination);
            if (!destination.StartsWith("Assets/", StringComparison.Ordinal)) throw new IOException("Choose a folder inside Assets.");
            string path = Path.GetFullPath(Path.Combine(project, destination));
            if (!Within(path, Path.Combine(project, "Assets"))) throw new IOException("Choose a sample folder inside Assets, not Assets itself.");
            if (File.Exists(path)) throw new IOException("The destination is a file, not a folder.");
            NoLinks(path); return path;
        }
        private static string Relative(string path)
        {
            path = path.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains(':') || path.Split('/').Any(x => x == ".." || x == "." || x.Length == 0 || x.EndsWith(" ") || x.EndsWith(".") || x.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                throw new IOException("Invalid relative sample path: " + path);
            return path;
        }
        private string Target(string destination, string relative)
        {
            string root = ValidateDestination(destination), path = Path.GetFullPath(Path.Combine(root, Relative(relative)));
            if (!Within(path, root)) throw new IOException("Sample path escapes the destination.");
            NoLinks(path); return path;
        }
        public string ManifestPath(string destination)
        {
            ValidateDestination(destination);
            string key = Relative(destination).ToLowerInvariant();
            return Path.GetFullPath(Path.Combine(project, "ProjectSettings/BaseUnity/Samples/" + Hash(Encoding.UTF8.GetBytes(key)) + ".json"));
        }
        // Preserve early local tracking records while moving to a project-relative, portable key.
        private string LegacyManifestPath(string destination) => Path.GetFullPath(Path.Combine(project, "ProjectSettings/BaseUnity/Samples/" + Hash(Encoding.UTF8.GetBytes(ValidateDestination(destination).ToLowerInvariant())) + ".json"));
        private string ManifestFingerprint(string destination) => (RawHash(ManifestPath(destination)) ?? "missing") + ":" + (RawHash(LegacyManifestPath(destination)) ?? "missing");
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static string RawHash(string path) => File.Exists(path) ? Hash(File.ReadAllBytes(path)) : null;
        public static string ContentHash(string path, byte[] bytes)
        {
            if (new[] { ".cs", ".json", ".asmdef", ".md", ".txt", ".yaml", ".yml" }.Contains(Path.GetExtension(path).ToLowerInvariant()))
                try { bytes = Encoding.UTF8.GetBytes(new UTF8Encoding(false, true).GetString(bytes).Replace("\r\n", "\n").Replace("\r", "\n")); } catch (DecoderFallbackException) { }
            return Hash(bytes);
        }
        private static IEnumerable<string> Files(string root)
        {
            NoLinks(root);
            foreach (string file in Directory.GetFiles(root)) { NoLinks(file); yield return file; }
            foreach (string directory in Directory.GetDirectories(root)) foreach (string file in Files(directory)) yield return file;
        }
        public SampleInstallManifest ReadManifest(string destination)
        {
            string path = ManifestPath(destination);
            if (!File.Exists(path) && File.Exists(LegacyManifestPath(destination))) path = LegacyManifestPath(destination);
            NoLinks(path);
            if (!File.Exists(path)) return new SampleInstallManifest { destination = destination };
            SampleInstallManifest manifest;
            try { manifest = JsonUtility.FromJson<SampleInstallManifest>(File.ReadAllText(path)); }
            catch (Exception e) { throw new IOException("Cannot read sample manifest; it was preserved.", e); }
            if (manifest == null || manifest.schema != 1 || manifest.files == null || manifest.destination != destination)
                throw new IOException("Unsupported or invalid sample manifest; it was preserved.");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in manifest.files)
            {
                if (file == null || !seen.Add(Relative(file.path))) throw new IOException("Invalid/duplicate manifest path.");
                Target(destination, file.path);
                if (file.hash != ContentHash(file.path, Convert.FromBase64String(file.content))) throw new IOException("Manifest baseline is corrupt.");
            }
            return manifest;
        }
        public SampleUpdatePlan Preview(string source, string destination, string version)
        {
            source = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); NoLinks(source);
            var root = ValidateDestination(destination);
            if (source == root || Within(source, root) || Within(root, source)) throw new IOException("Source and destination must be separate folders.");
            if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
            var manifest = ReadManifest(destination);
            var tracked = manifest.files.ToDictionary(x => x.path, StringComparer.OrdinalIgnoreCase);
            var plan = new SampleUpdatePlan { source = source, destination = destination, version = version, manifestHash = ManifestFingerprint(destination) };
            foreach (var file in Files(source).OrderBy(x => x, StringComparer.Ordinal))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                string relative = Relative(file.Substring(source.Length + 1)); string target = Target(destination, relative);
                byte[] incoming = File.ReadAllBytes(file), current = File.Exists(target) ? File.ReadAllBytes(target) : null;
                tracked.TryGetValue(relative, out var baseline);
                string incomingHash = ContentHash(relative, incoming), currentHash = current == null ? null : ContentHash(relative, current);
                SampleFileState state = current == null ? (baseline == null ? SampleFileState.Add : SampleFileState.LocalDeleted) :
                    currentHash == incomingHash ? SampleFileState.Identical : baseline == null ? SampleFileState.Conflict :
                    currentHash == baseline.hash ? SampleFileState.Update : incomingHash == baseline.hash ? SampleFileState.LocalModified : SampleFileState.Conflict;
                plan.files.Add(new SampleUpdateEntry { path = relative, state = state, selected = state == SampleFileState.Add || state == SampleFileState.Update,
                    currentHash = RawHash(target), sourceHash = Hash(incoming), baselineContent = baseline?.content,
                    currentContent = current == null ? null : Convert.ToBase64String(current), sourceContent = Convert.ToBase64String(incoming) });
                tracked.Remove(relative);
            }
            foreach (var old in tracked.Values.OrderBy(x => x.path))
            {
                string target = Target(destination, old.path);
                plan.files.Add(new SampleUpdateEntry { path = old.path, baselineContent = old.content, currentHash = RawHash(target),
                    currentContent = File.Exists(target) ? Convert.ToBase64String(File.ReadAllBytes(target)) : null,
                    state = File.Exists(target) ? SampleFileState.SourceRemoved : SampleFileState.AlreadyRemoved });
            }
            return plan;
        }
        private static void AtomicWrite(string path, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".baseunity-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllBytes(temp, bytes);
                for (int attempt = 0; ; attempt++)
                {
                    try { if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path); break; }
                    catch (IOException) when (attempt < 3)
                    {
                        // Antivirus/indexers may briefly hold newly written files on Windows.
                        if (!File.Exists(temp) && RawHash(path) == Hash(bytes)) break;
                        System.Threading.Thread.Sleep(20 * (attempt + 1));
                    }
                }
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        private string ProjectPath(string path) => Path.GetFullPath(path).Substring(project.Length + 1).Replace('\\', '/');
        private string BackupPath(string id)
        { if (id == null || id.Length != 32 || id.Any(c => !Uri.IsHexDigit(c))) throw new IOException("Invalid backup ID."); string path = Path.Combine(BackupDirectory, id + ".json"); NoLinks(path); return path; }
        private void SaveBackup(SampleBackup backup) => AtomicWrite(BackupPath(backup.id), Encoding.UTF8.GetBytes(JsonUtility.ToJson(backup, true)));
        private string BackupTarget(SampleBackup backup, string path)
        {
            path = Relative(path); string absolute = Path.GetFullPath(Path.Combine(project, path));
            if (!Within(absolute, ValidateDestination(backup.destination)) && !string.Equals(absolute, ManifestPath(backup.destination), PathComparison) && !string.Equals(absolute, LegacyManifestPath(backup.destination), PathComparison) && !string.Equals(absolute, Path.GetFullPath(Path.Combine(project, "ProjectSettings/EditorBuildSettings.asset")), PathComparison))
                throw new IOException("Backup path is outside this sample: " + path);
            NoLinks(absolute); return absolute;
        }
        public SampleBackup ReadBackup(string id)
        {
            var backup = JsonUtility.FromJson<SampleBackup>(File.ReadAllText(BackupPath(id)));
            if (backup == null || backup.schema != 1 || backup.id != id || backup.files == null) throw new IOException("Invalid backup.");
            ValidateDestination(backup.destination);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in backup.files)
            { if (entry == null || !seen.Add(entry.path)) throw new IOException("Duplicate backup path.");
                if (string.IsNullOrEmpty(entry.afterHash)) entry.afterHash = null;
                BackupTarget(backup, entry.path); if (entry.existed) Convert.FromBase64String(entry.before); }
            return backup;
        }
        public SampleBackup[] Backups(string destination)
        {
            ValidateDestination(destination);
            if (!Directory.Exists(BackupDirectory)) return Array.Empty<SampleBackup>();
            return Directory.GetFiles(BackupDirectory, "*.json").Select(path => ReadBackup(Path.GetFileNameWithoutExtension(path)))
                .Where(x => x.destination == destination).OrderByDescending(x => x.createdUtc).ToArray();
        }
        private SampleFileBackup Capture(string absolute, byte[] after) => new SampleFileBackup { path = ProjectPath(absolute), existed = File.Exists(absolute),
            before = File.Exists(absolute) ? Convert.ToBase64String(File.ReadAllBytes(absolute)) : null, afterHash = after == null ? null : Hash(after) };
        public string Apply(SampleUpdatePlan plan)
        {
            var fresh = Preview(plan.source, plan.destination, plan.version);
            if (fresh.manifestHash != plan.manifestHash || fresh.files.Count != plan.files.Count) throw new IOException("Preview is stale; scan again.");
            foreach (var entry in fresh.files)
            {
                var reviewed = plan.files.SingleOrDefault(x => x.path == entry.path);
                if (reviewed == null || entry.currentHash != reviewed.currentHash || entry.sourceHash != reviewed.sourceHash) throw new IOException("A file changed after preview: " + entry.path);
                entry.selected = reviewed.selected;
            }
            var manifest = ReadManifest(plan.destination); var records = manifest.files.ToDictionary(x => x.path, StringComparer.OrdinalIgnoreCase);
            var writes = new Dictionary<string, byte[]>(); bool unresolved = false;
            foreach (var entry in fresh.files)
            {
                if (entry.state == SampleFileState.AlreadyRemoved) { records.Remove(entry.path); continue; }
                if (entry.selected && entry.state != SampleFileState.Identical)
                {
                    string target = Target(plan.destination, entry.path);
                    writes.Add(target, entry.IsRemoval ? null : Convert.FromBase64String(entry.sourceContent));
                    if (File.Exists(target + ".meta")) writes.Add(target + ".meta", entry.IsRemoval ? null : File.ReadAllBytes(target + ".meta"));
                }
                if (entry.IsRemoval) { if (entry.selected) records.Remove(entry.path); else unresolved = true; }
                else if (entry.selected || entry.state == SampleFileState.Identical)
                    records[entry.path] = new SampleBaseline { path = entry.path, hash = ContentHash(entry.path, Convert.FromBase64String(entry.sourceContent)), content = entry.sourceContent };
                else if (entry.state != SampleFileState.LocalModified) unresolved = true;
            }
            manifest.lastSourceVersion = plan.version; if (!unresolved) manifest.installedVersion = plan.version;
            manifest.files = records.Values.OrderBy(x => x.path).ToList();
            writes.Add(ManifestPath(plan.destination), Encoding.UTF8.GetBytes(JsonUtility.ToJson(manifest, true)));
            var backup = new SampleBackup { id = Guid.NewGuid().ToString("N"), destination = plan.destination, createdUtc = DateTime.UtcNow.ToString("O"),
                description = "Sample update to " + plan.version, status = "Preparing", files = writes.Select(x => Capture(x.Key, x.Value)).ToList() };
            SaveBackup(backup); // Persist recovery information before changing any project file.
            try
            {
                foreach (var write in writes) { NoLinks(write.Key); if (write.Value == null) File.Delete(write.Key); else AtomicWrite(write.Key, write.Value); }
                backup.status = "Committed"; SaveBackup(backup); return backup.id;
            }
            catch
            {
                try { Restore(backup.id); } catch { backup.status = "RecoveryRequired"; SaveBackup(backup); }
                throw;
            }
        }
        public void ValidateRestore(string id)
        {
            var backup = ReadBackup(id);
            if (backup.status == "Restored") throw new IOException("This backup was already restored.");
            foreach (var file in backup.files)
            {
                string current = RawHash(BackupTarget(backup, file.path));
                string before = file.existed ? Hash(Convert.FromBase64String(file.before)) : null;
                if (current != file.afterHash && current != before) throw new IOException("Restore would overwrite newer changes: " + file.path);
            }
        }
        public void Restore(string id)
        {
            ValidateRestore(id); var backup = ReadBackup(id);
            foreach (var file in backup.files.AsEnumerable().Reverse())
            {
                string target = BackupTarget(backup, file.path);
                if (file.existed) AtomicWrite(target, Convert.FromBase64String(file.before));
                else { if (File.Exists(target)) File.Delete(target); if (!file.path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) && Within(target, ValidateDestination(backup.destination)) && !backup.files.Any(x => x.path == file.path + ".meta")) File.Delete(target + ".meta"); }
            }
            backup.status = "Restored"; SaveBackup(backup);
        }
        // Used around asset generation/configuration. If interrupted, original snapshots remain available.
        public string BeginSetup(string destination, SampleSceneState[] scenes)
        {
            string root = ValidateDestination(destination);
            var before = Directory.Exists(root) ? Files(root).ToArray() : Array.Empty<string>();
            string settings = Path.Combine(project, "ProjectSettings/EditorBuildSettings.asset");
            var backup = new SampleBackup { id = Guid.NewGuid().ToString("N"), destination = destination, createdUtc = DateTime.UtcNow.ToString("O"),
                description = "Setup/configuration", status = "Preparing", hasSceneSnapshot = true, beforeScenes = scenes,
                files = before.Concat(new[] { settings }).Select(path => Capture(path, File.Exists(path) ? File.ReadAllBytes(path) : null)).ToList() };
            SaveBackup(backup); return backup.id;
        }
        public void CompleteSetup(string id, SampleSceneState[] scenes, bool completed = true)
        {
            var backup = ReadBackup(id); var known = backup.files.ToDictionary(x => x.path, StringComparer.OrdinalIgnoreCase);
            string root = ValidateDestination(backup.destination);
            foreach (string file in Directory.Exists(root) ? Files(root) : Enumerable.Empty<string>())
            {
                string relative = ProjectPath(file);
                if (!known.ContainsKey(relative)) backup.files.Add(new SampleFileBackup { path = relative, existed = false, afterHash = RawHash(file) });
            }
            foreach (var file in backup.files) file.afterHash = RawHash(BackupTarget(backup, file.path));
            backup.files = backup.files.Where(file => file.afterHash != (file.existed ? Hash(Convert.FromBase64String(file.before)) : null)).ToList();
            backup.afterScenes = scenes;
            backup.hasSceneSnapshot = !(backup.beforeScenes ?? Array.Empty<SampleSceneState>()).Select(x => x.path + ":" + x.enabled).SequenceEqual((scenes ?? Array.Empty<SampleSceneState>()).Select(x => x.path + ":" + x.enabled));
            backup.status = completed ? "Committed" : "Incomplete"; SaveBackup(backup);
        }
    }
}
