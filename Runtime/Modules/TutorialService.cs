using System;
using System.Collections.Generic;
using System.Linq;
using BaseUnity.Save;
using UnityEngine;

namespace BaseUnity.Features
{
    public sealed class TutorialService : IDisposable
    {
        [Serializable] public sealed class Progress { public string id; public int step; public bool completed; }
        [Serializable] public sealed class Data { public int schema = 1; public List<Progress> entries = new List<Progress>(); }
        private readonly TutorialSettings settings;
        private readonly ISaveStore saves;
        private Data data = new Data();
        private TutorialDefinition active;
        private bool disposed;
        public string LastError { get; private set; }
        public string LoadError { get; private set; }
        public bool Enabled => settings.enabled && !disposed;
        public string ActiveId => active?.id;
        public TutorialStep Current => active == null ? null : active.steps[data.entries.First(x => x.id == active.id).step];
        public bool CanSkip => active != null && active.allowSkip;
        public event Action Changed;
        public TutorialService(TutorialSettings settings, ISaveStore saves)
        {
            this.settings = JsonUtility.FromJson<TutorialSettings>(JsonUtility.ToJson(settings ?? new TutorialSettings())); this.saves = saves;
            try { data = ModulePersistence.Load(saves, this.settings.saveSlot, Valid, data); }
            catch (Exception error) { LoadError = LastError = error.Message; }
        }
        private static bool Valid(Data value)
        {
            if (value.schema > 1) throw new NotSupportedException("Tutorial progress needs a newer BaseUnity.");
            return value.schema == 1 && value.entries != null && value.entries.All(x => x != null && !string.IsNullOrWhiteSpace(x.id) && x.step >= 0) && value.entries.Select(x => x.id).Distinct().Count() == value.entries.Count;
        }
        public bool StartNext()
        {
            var next = (settings.tutorials ?? Array.Empty<TutorialDefinition>()).FirstOrDefault(x => x != null && !IsCompleted(x.id));
            return next != null && Start(next.id);
        }
        public bool AutoStartAtHome => Enabled && settings.autoStartAtHome;
        public bool IsCompleted(string id) => data.entries.Any(x => x.id == id && x.completed);
        public bool Start(string id)
        {
            if (!Enabled || LoadError != null || active != null || IsCompleted(id)) return false;
            var definition = (settings.tutorials ?? Array.Empty<TutorialDefinition>()).FirstOrDefault(x => x != null && x.id == id);
            if (definition == null || definition.steps == null || definition.steps.Length == 0 || definition.steps.Any(x => x == null)) return false;
            var progress = data.entries.FirstOrDefault(x => x.id == id);
            if (progress != null && progress.step >= definition.steps.Length) { LastError = "Tutorial steps changed; migrate or reset its progress."; return false; }
            if (progress == null && !Commit(next => next.entries.Add(new Progress { id = id }))) return false;
            active = definition; Changed?.Invoke(); return true;
        }
        public bool Advance(string targetAction = null)
        {
            if (active == null || (Current.waitForTargetAction && Current.target != targetAction)) return false;
            string id = active.id; int count = active.steps.Length;
            if (!Commit(next => { var entry = next.entries.First(x => x.id == id); entry.step++; entry.completed = entry.step >= count; })) return false;
            if (IsCompleted(id)) active = null; Changed?.Invoke(); return true;
        }
        public bool Skip()
        {
            if (!CanSkip) return false; string id = active.id;
            if (!Commit(next => next.entries.First(x => x.id == id).completed = true)) return false;
            active = null; Changed?.Invoke(); return true;
        }
        public void Dispose() { disposed = true; active = null; Changed = null; }
        public void Stop() { active = null; Changed?.Invoke(); }
        public bool Reset(string id)
        {
            if (!Enabled || !Commit(next => next.entries.RemoveAll(x => x.id == id))) return false;
            if (ActiveId == id) active = null; Changed?.Invoke(); return true;
        }
        private bool Commit(Action<Data> edit)
        {
            if (LoadError != null) return false;
            try { var next = JsonUtility.FromJson<Data>(JsonUtility.ToJson(data)); edit(next); saves.Save(settings.saveSlot, next); data = next; LastError = null; return true; }
            catch (Exception error) { LastError = error.Message; return false; }
        }
    }
}
