using System;
using System.Linq;
namespace BaseUnity.Modularity
{
    public sealed class ModulePreset
    {
        public readonly string name;
        public readonly string[] modules;
        public ModulePreset(string name,params string[] modules) { this.name=name;this.modules=modules; }
    }
    public static class ModulePresets
    {
        public static ModulePreset[] All => new[]{
            new ModulePreset("Minimal","core"),
            new ModulePreset("GameFlow + UI","gameflow-ui"),
            new ModulePreset("Mobile Offline","gameflow-ui","mobile-input","audio","pooling","shop-ui","daily-reward-ui","quests-ui","achievements-ui","localization-ui","gameflow-economy"),
            new ModulePreset("SDK simulation","ads-simulation","consent-simulation","firebase-simulation")
        };
        public static string Summary(ModuleCatalog catalog,System.Collections.Generic.IEnumerable<string> ids)
        { var modules=ModulePackageStore.Resolve(catalog,ids);return modules.Length+" packages / "+modules.Sum(m=>m.files.Length+(m.editorFiles?.Length??0))+" scripts: "+string.Join(", ",modules.Select(m=>m.id)); }
    }
}
