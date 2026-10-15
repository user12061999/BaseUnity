using System;
using System.IO;
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

    /// <summary>JsonUtility fields, main-thread use. Version mismatch requires an explicit migration.</summary>
    public sealed class JsonSaveStore : ISaveStore
    {
        [Serializable]
        private sealed class Envelope { public int version; public string payload; }
        private readonly string directory;
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
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }

        public bool TryLoad<T>(string slot, out T data, int version = 1) where T : class
        {
            if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
            string path = FilePath(slot);
            return Read(path, version, out data) || Read(path + ".bak", version, out data);
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
