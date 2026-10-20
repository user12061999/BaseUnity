using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using UnityEngine;
namespace BaseUnity.Save
{
    public interface ISaveStore
    {
        void Save<T>(string slot, T data, int version = 1) where T : class;
        bool TryLoad<T>(string slot, out T data, int version = 1) where T : class;
        void Delete(string slot);
    }
    public enum SaveLoadState { Missing, Loaded, RecoveredFromBackup, Migrated, RecoveredAndMigrated }
    public sealed class SaveSlotStatus
    { public bool primaryExists, backupExists, primaryValid, backupValid; public int primaryVersion, backupVersion; }
    /// <summary>Main-thread JSON store. Legacy envelopes remain readable; checksums detect accidental corruption, not cheating.</summary>
    public sealed class JsonSaveStore : ISaveStore
    {
        [Serializable] private sealed class Envelope { public int version; public string payload, checksum; }
        private readonly string directory;
        private readonly HashSet<string> recovered = new HashSet<string>();
        public SaveMigrationRegistry Migrations { get; }
        public SaveLoadState LastLoadState { get; private set; }
        public JsonSaveStore(string directory, SaveMigrationRegistry migrations = null)
        { if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Directory is required.", nameof(directory)); this.directory = Path.GetFullPath(directory); Migrations = migrations ?? new SaveMigrationRegistry(); }
        private string FilePath(string slot)
        {
            if (slot == null || !Regex.IsMatch(slot, @"\A[a-zA-Z0-9_-]{1,64}\z")) throw new ArgumentException("Slot must contain 1-64 letters, numbers, underscores or hyphens.", nameof(slot));
            return Path.Combine(directory, slot + ".json");
        }
        private static string Checksum(int version, string payload)
        { using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(version.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n" + payload))); }
        private static Envelope ReadEnvelope(string path)
        {
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<Envelope>(File.ReadAllText(path)); }
            catch (ArgumentException) { return null; }
        }
        private static bool Intact(Envelope envelope) => envelope != null && envelope.version >= 1 && !string.IsNullOrEmpty(envelope.payload) && (string.IsNullOrEmpty(envelope.checksum) || envelope.checksum == Checksum(envelope.version, envelope.payload));
        private static bool Future(Envelope primary, Envelope backup, int version) => (primary != null && primary.version > version) || (backup != null && backup.version > version);
        public SaveSlotStatus Inspect(string slot)
        {
            string path = FilePath(slot); var main = ReadEnvelope(path); var backup = ReadEnvelope(path + ".bak");
            return new SaveSlotStatus { primaryExists = File.Exists(path), backupExists = File.Exists(path + ".bak"), primaryVersion = main?.version ?? 0, backupVersion = backup?.version ?? 0, primaryValid = Intact(main), backupValid = Intact(backup) };
        }
        public void Save<T>(string slot, T data, int version = 1) where T : class
        {
            if (data == null) throw new ArgumentNullException(nameof(data)); if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
            string path = FilePath(slot); var current = ReadEnvelope(path); var backup = ReadEnvelope(path + ".bak");
            if (Future(current, backup, version)) throw new NotSupportedException("Refusing to overwrite a newer save version: " + slot);
            Directory.CreateDirectory(directory); string temp = path + ".tmp";
            string payload = JsonUtility.ToJson(data);
            string json = JsonUtility.ToJson(new Envelope { version = version, payload = payload, checksum = Checksum(version, payload) }, true);
            try
            {
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) { writer.Write(json); writer.Flush(); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temp, path, recovered.Contains(path) || !Intact(current) ? path + ".corrupt-" + Guid.NewGuid().ToString("N") : path + ".bak");
                else File.Move(temp, path);
                recovered.Remove(path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public bool TryLoad<T>(string slot, out T data, int version = 1) where T : class
        { return Load(slot, out data, value => value != null, version, false, false) != SaveLoadState.Missing; }
        public SaveLoadState LoadValidated<T>(string slot, out T data, Func<T, bool> valid, int version = 1) where T : class
        { return Load(slot, out data, valid, version, true, false); }
        /// <summary>Validate all migration steps in memory; only then persist the upgraded envelope atomically.</summary>
        public SaveLoadState LoadVersioned<T>(string slot, out T data, int currentVersion, Func<T, bool> valid, bool persistMigration = true) where T : class
        { return Load(slot, out data, valid, currentVersion, true, persistMigration); }
        private bool Candidate<T>(Envelope envelope, Func<T, bool> valid, int version, bool strict, out T data, out bool migrated) where T : class
        {
            data = null; migrated = false; if (!Intact(envelope)) return false;
            if (envelope.version != version && !Migrations.CanMigrate<T>(envelope.version, version))
            { if (strict) throw new NotSupportedException("Register migrations for " + typeof(T).Name + " v" + envelope.version + " -> v" + version + ". Files were preserved."); return false; }
            string payload = envelope.payload; migrated = envelope.version < version;
            if (migrated) payload = Migrations.Migrate<T>(payload, envelope.version, version);
            try { data = JsonUtility.FromJson<T>(payload); }
            catch (ArgumentException error) { if (migrated) throw new SaveMigrationException("Migration produced invalid JSON. Files were preserved.", error); return false; }
            bool accepted = data != null && valid(data);
            if (!accepted && migrated) throw new SaveMigrationException("Migrated data failed validation. Files were preserved.");
            return accepted;
        }
        private SaveLoadState Load<T>(string slot, out T data, Func<T, bool> valid, int version, bool strict, bool persist) where T : class
        {
            data = null; LastLoadState = SaveLoadState.Missing;
            if (valid == null) throw new ArgumentNullException(nameof(valid)); if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
            string path = FilePath(slot); var main = ReadEnvelope(path); var backup = ReadEnvelope(path + ".bak");
            if (Future(main, backup, version)) { if (strict) throw new NotSupportedException("Save was created by a newer version: " + slot); return SaveLoadState.Missing; }
            bool fromBackup = false, migrated; T candidate;
            if (!Candidate(main, valid, version, strict, out candidate, out migrated))
            {
                if (!Candidate(backup, valid, version, strict, out candidate, out migrated))
                { if (strict && (File.Exists(path) || File.Exists(path + ".bak"))) throw new InvalidDataException("Save and backup could not be validated: " + slot + ". Files were preserved."); return SaveLoadState.Missing; }
                fromBackup = true; recovered.Add(path);
            }
            if (persist && migrated) Save(slot, candidate, version);
            data = candidate;
            return LastLoadState = migrated ? (fromBackup ? SaveLoadState.RecoveredAndMigrated : SaveLoadState.Migrated) : fromBackup ? SaveLoadState.RecoveredFromBackup : SaveLoadState.Loaded;
        }
        public void RestoreBackup<T>(string slot, Func<T, bool> valid, int version = 1) where T : class
        {
            if (valid == null) throw new ArgumentNullException(nameof(valid)); if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
            string path = FilePath(slot); var main = ReadEnvelope(path); var backup = ReadEnvelope(path + ".bak");
            if (Future(main, backup, version)) throw new NotSupportedException("Refusing to restore over a newer version: " + slot);
            if (!Candidate(backup, valid, version, true, out T data, out _)) throw new InvalidDataException("Backup failed validation. Files were preserved: " + slot);
            recovered.Add(path); Save(slot, data, version); LastLoadState = SaveLoadState.RecoveredFromBackup;
        }
        public void Delete(string slot)
        { string path = FilePath(slot); foreach (string suffix in new[] { "", ".bak", ".tmp" }) File.Delete(path + suffix); recovered.Remove(path); }
    }
}
