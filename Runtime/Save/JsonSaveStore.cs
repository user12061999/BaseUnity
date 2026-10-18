using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BaseUnity.Save
{
    public interface ISaveStore
    {
        void Save<T>(string slot, T data, int version = 1) where T : class;
        bool TryLoad<T>(string slot, out T data, int version = 1) where T : class;
        void Delete(string slot);
    }

    public enum SaveLoadState { Missing, Loaded, RecoveredFromBackup }

    /// <summary>JsonUtility fields, main-thread use. Version mismatch requires an explicit migration.</summary>
    public sealed class JsonSaveStore : ISaveStore
    {
        [Serializable]
        private sealed class Envelope { public int version; public string payload; }
        private readonly string directory;
        private readonly HashSet<string> recovered = new HashSet<string>();
        public JsonSaveStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Directory is required.", nameof(directory));
            this.directory = Path.GetFullPath(directory);
        }

        private string FilePath(string slot)
        {
            if (slot == null || !Regex.IsMatch(slot, @"\A[a-zA-Z0-9_-]{1,64}\z"))
                throw new ArgumentException("Slot must contain 1-64 letters, numbers, underscores or hyphens.", nameof(slot));
            return Path.Combine(directory, slot + ".json");
        }

        public void Save<T>(string slot, T data, int version = 1) where T : class
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
            string path = FilePath(slot);
            Directory.CreateDirectory(directory);
            string temp = path + ".tmp";
            var json = JsonUtility.ToJson(new Envelope { version = version, payload = JsonUtility.ToJson(data) }, true);
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            // Replace preserves the previous generation; no delete-before-write window.
            if (File.Exists(path)) File.Replace(temp, path, recovered.Contains(path) ? path + ".corrupt-" + Guid.NewGuid().ToString("N") : path + ".bak");
            else File.Move(temp, path);
            recovered.Remove(path);
        }

        public bool TryLoad<T>(string slot, out T data, int version = 1) where T : class
        {
            if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
            string path = FilePath(slot);
            return Read(path, version, out data) || Read(path + ".bak", version, out data);
        }

        /// <summary>Strict loading for valuable data. Never interprets an unreadable existing save as a new player.</summary>
        public SaveLoadState LoadValidated<T>(string slot, out T data, Func<T, bool> valid, int version = 1) where T : class
        {
            if (valid == null) throw new ArgumentNullException(nameof(valid));
            if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
            string path = FilePath(slot);
            // A newer envelope must never be downgraded by falling back to an older backup.
            if (File.Exists(path))
            {
                Envelope envelope = null;
                try { envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(path)); }
                catch (ArgumentException) { }
                if (envelope != null && envelope.version > version) throw new NotSupportedException("Save was created by a newer version: " + slot);
            }
            if (Read(path, version, out data) && valid(data)) return SaveLoadState.Loaded;
            if (Read(path + ".bak", version, out data) && valid(data))
            { recovered.Add(path); return SaveLoadState.RecoveredFromBackup; }
            data = null;
            if (File.Exists(path) || File.Exists(path + ".bak")) throw new InvalidDataException("Save and backup could not be validated: " + slot + ". Files were preserved.");
            return SaveLoadState.Missing;
        }

        private static bool Read<T>(string path, int version, out T data) where T : class
        {
            data = null;
            if (!File.Exists(path)) return false;
            try
            {
                var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(path));
                if (envelope == null || envelope.version != version || string.IsNullOrEmpty(envelope.payload)) return false;
                data = JsonUtility.FromJson<T>(envelope.payload);
                return data != null;
            }
            catch (ArgumentException) { return false; }
            catch (IOException) { return false; }
        }

        public void Delete(string slot)
        {
            string path = FilePath(slot);
            foreach (string suffix in new[] { "", ".bak", ".tmp" }) File.Delete(path + suffix);
        }
    }
}
