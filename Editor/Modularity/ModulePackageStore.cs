using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
namespace BaseUnity.Modularity
{
    [Serializable] public sealed class ModuleDefinition
    { public string id,name,package,assembly,description; public string[] requires,references,engineDependencies,files,editorFiles,assets,sdkTypes; }
    [Serializable] public sealed class ModuleCatalog { public int format; public ModuleDefinition[] modules; }
    [Serializable] public sealed class ModuleFile { public string path,hash; }
    [Serializable] public sealed class ModuleReceipt
    { public string owner="BaseUnity.ModuleManager",id,version; public ModuleFile[] files; }
    [Serializable] public sealed class ModuleBackup { public string[] before,after; public string version; }
    public sealed class ModulePlan
    { public string[] selected,added,updated,removed; public string version; public string Summary => "Install/update: "+string.Join(", ",added.Concat(updated))+"\nRemove: "+string.Join(", ",removed); }
    public static class ModulePackageStore
    {
        private const string Marker=".baseunity-module.json";
        [Serializable] private sealed class VersionInfo { public string name,version; }
        public static string Root(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        private static bool Inside(string path,string parent) => Root(path).StartsWith(Root(parent)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
        private static string Hash(byte[] value) { using(var hash=SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(value)).Replace("-","").ToLowerInvariant(); }
        private static string Quote(string value)
        {
            var result=new StringBuilder("\""); foreach(char c in value ?? "") { if(c=='\\')result.Append("\\\\"); else if(c=='\"')result.Append("\\\""); else if(c<32)result.Append("\\u"+((int)c).ToString("x4")); else result.Append(c); } return result.Append('"').ToString();
        }
        private static void PlainPath(string path)
        { for(var item=Root(path); !string.IsNullOrEmpty(item); item=Path.GetDirectoryName(item)) if((Directory.Exists(item)||File.Exists(item)) && (File.GetAttributes(item)&FileAttributes.ReparsePoint)!=0) throw new IOException("Refusing linked path: "+item); }
        private static string Relative(string root,string relative)
        {
            if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.Split('/','\\').Any(x=>x==".."||x==".")) throw new IOException("Unsafe relative path: "+relative);
            var result=Root(Path.Combine(root,relative)); if(!Inside(result,root)) throw new IOException("Path escapes root"); PlainPath(result); return result;
        }
        public static ModuleCatalog ReadCatalog(string repository)
        {
            repository=Root(repository); PlainPath(repository);
            var info=JsonUtility.FromJson<VersionInfo>(File.ReadAllText(Path.Combine(repository,"package.json")));
            if(info==null||info.name!="com.baseunity.foundation") throw new InvalidOperationException("Select the BaseUnity repository root");
            var catalog=JsonUtility.FromJson<ModuleCatalog>(File.ReadAllText(Path.Combine(repository,"Modules~/catalog.json")));
            if(catalog==null||catalog.format!=1||catalog.modules==null) throw new InvalidDataException("Unsupported module catalog");
            var names=new HashSet<string>(); var packages=new HashSet<string>(); var sources=new HashSet<string>();
            foreach(var module in catalog.modules) {
                if(module==null||!Regex.IsMatch(module.id ?? "",@"\A[a-z][a-z0-9-]*\z")||!Regex.IsMatch(module.package ?? "",@"\Acom\.baseunity\.[a-z0-9-]+\z")||!names.Add(module.id)||!packages.Add(module.package)||module.files==null||module.files.Length==0) throw new InvalidDataException("Invalid/duplicate module definition");
                foreach(var file in module.files.Concat(module.editorFiles??new string[0])) { Relative(repository,file); if(!file.EndsWith(".cs",StringComparison.Ordinal)||!sources.Add(file)) throw new InvalidDataException("Duplicate or unsupported source: "+file); }
            }
            foreach(var module in catalog.modules)foreach(string file in module.assets??new string[0]) { Relative(repository,file);if(!sources.Add(file)||!file.StartsWith("Samples~/Integrations/",StringComparison.Ordinal)||file.EndsWith(".dll",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid integration asset: "+file); }
            Resolve(catalog,catalog.modules.Select(x=>x.id)); return catalog;
        }
        public static ModuleDefinition[] Resolve(ModuleCatalog catalog,IEnumerable<string> selected)
        {
            var result=new List<ModuleDefinition>(); var visiting=new HashSet<string>(); var done=new HashSet<string>();
            Action<string> visit=null; visit=id=> { if(done.Contains(id))return; if(!visiting.Add(id))throw new InvalidDataException("Circular dependency: "+id); var module=catalog.modules.SingleOrDefault(x=>x.id==id); if(module==null)throw new InvalidDataException("Unknown module: "+id); foreach(var requirement in module.requires ?? new string[0])visit(requirement); visiting.Remove(id); done.Add(id); result.Add(module); };
            var requested=selected.Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray(); if(requested.Length>0) visit("core"); foreach(var id in requested)visit(id); return result.ToArray();
        }
        private static string Version(string repository) => JsonUtility.FromJson<VersionInfo>(File.ReadAllText(Path.Combine(repository,"package.json"))).version;
        private static string PackagePath(string project,ModuleDefinition module) => Relative(Path.Combine(Root(project),"Packages"),module.package);
        private static ModuleReceipt Receipt(string directory)
        {
            string path=Path.Combine(directory,Marker); if(!File.Exists(path))throw new IOException("Package is not owned by BaseUnity Module Manager: "+directory);
            var receipt=JsonUtility.FromJson<ModuleReceipt>(File.ReadAllText(path)); if(receipt==null||receipt.owner!="BaseUnity.ModuleManager"||receipt.files==null)throw new IOException("Invalid package receipt"); return receipt;
        }
        private static void Clean(string directory,ModuleDefinition module)
        {
            PlainPath(directory); var receipt=Receipt(directory); if(receipt.id!=module.id)throw new IOException("Package receipt ID mismatch"); var known=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var item in receipt.files) { var path=Relative(directory,item.path); if(!known.Add(item.path)||!File.Exists(path)||Hash(File.ReadAllBytes(path))!=item.hash)throw new IOException("Local edits detected. Copy them back to your source or restore the file first: "+path); }
            foreach(string path in Directory.GetFiles(directory,"*",SearchOption.AllDirectories)) { string relative=path.Substring(Root(directory).Length+1).Replace('\\','/'); if(relative==Marker||known.Contains(relative))continue; if(path.EndsWith(".meta",StringComparison.OrdinalIgnoreCase)&&Directory.Exists(path.Substring(0,path.Length-5)))continue; throw new IOException("Unmanaged file in package; preserved: "+path); }
        }
        public static string[] Installed(string repository,string project)
        { return ReadCatalog(repository).modules.Where(x=>Directory.Exists(PackagePath(project,x))).Select(x=>x.id).ToArray(); }
        public static ModulePlan Preview(string repository,string project,IEnumerable<string> selected)
        {
            var catalog=ReadCatalog(repository); var resolved=Resolve(catalog,selected); var installed=Installed(repository,project); var desired=resolved.Select(x=>x.id).ToArray();
            foreach(var module in catalog.modules.Where(x=>installed.Contains(x.id)))Clean(PackagePath(project,module),module);
            return new ModulePlan { selected=desired,added=desired.Except(installed).ToArray(),updated=desired.Intersect(installed).ToArray(),removed=installed.Except(desired).ToArray(),version=Version(repository) };
        }
        private static void ValidateProject(string repository,string project)
        {
            project=Root(project); PlainPath(project);
            if(!Directory.Exists(Path.Combine(project,"Assets"))||!File.Exists(Path.Combine(project,"Packages/manifest.json")))throw new IOException("Choose a Unity project with Assets and Packages/manifest.json");
            if(Inside(repository,Path.Combine(project,"Assets"))||Inside(repository,Path.Combine(project,"Packages")))throw new IOException("Keep the source repository outside Assets and Packages (for example Tools/BaseUnity)");
            if(Root(Path.Combine(Application.dataPath,"..")).Equals(project,StringComparison.OrdinalIgnoreCase)&&AppDomain.CurrentDomain.GetAssemblies().Any(x=>x.GetName().Name=="BaseUnity.Runtime"))throw new InvalidOperationException("This project still uses the legacy full bundle. Use a clean project for selective modules; do not install duplicate Core/Save classes over the old bundle.");
        }
        private static void WriteFile(string directory,string relative,byte[] contents) { string path=Relative(directory,relative); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path,contents); }
        private static void TextFile(string directory,string relative,string contents) => WriteFile(directory,relative,new UTF8Encoding(false).GetBytes(contents));
        private static void Meta(string directory,string relative,string id) => TextFile(directory,relative+".meta","fileFormatVersion: 2\nguid: "+Hash(Encoding.UTF8.GetBytes(id+":"+relative)).Substring(0,32)+"\n");
        private static void Generate(string repository,string directory,ModuleDefinition module,ModuleCatalog catalog)
        {
            Directory.CreateDirectory(directory); var dependencies=new SortedDictionary<string,string>(StringComparer.Ordinal);
            foreach(var id in module.requires ?? new string[0]) dependencies[catalog.modules.Single(x=>x.id==id).package]=Version(repository);
            foreach(var requirement in module.engineDependencies ?? new string[0]) { int at=requirement.LastIndexOf('@'); if(at<1)throw new InvalidDataException("Invalid engine dependency"); dependencies[requirement.Substring(0,at)]=requirement.Substring(at+1); }
            string json="{\n  \"name\": "+Quote(module.package)+",\n  \"version\": "+Quote(Version(repository))+",\n  \"displayName\": "+Quote("BaseUnity "+module.name)+",\n  \"unity\": \"6000.0\",\n  \"description\": "+Quote(module.description)+",\n  \"dependencies\": {"+string.Join(",",dependencies.Select(x=>Quote(x.Key)+":"+Quote(x.Value)))+"}\n}\n";
            TextFile(directory,"package.json",json); Meta(directory,"package.json",module.package);
            TextFile(directory,"Runtime/"+module.assembly+".asmdef","{\"name\":"+Quote(module.assembly)+",\"rootNamespace\":\"BaseUnity\",\"references\":["+string.Join(",",(module.references ?? new string[0]).Select(Quote))+"]}"); Meta(directory,"Runtime/"+module.assembly+".asmdef",module.package);
            foreach(var file in module.files.Concat(module.assets??new string[0]))CopySource(repository,directory,file,SourceTarget(file),module.package);
            if((module.editorFiles?.Length??0)>0) {
                string asm="Editor/"+module.assembly+".Editor.asmdef";
                TextFile(directory,asm,"{\"name\":"+Quote(module.assembly+".Editor")+",\"includePlatforms\":[\"Editor\"],\"references\":["+Quote(module.assembly)+"]}");Meta(directory,asm,module.package);
                foreach(var file in module.editorFiles)CopySource(repository,directory,file,"Editor/"+Path.GetFileName(file),module.package);
            }
            var receipt=new ModuleReceipt { id=module.id,version=Version(repository),files=Directory.GetFiles(directory,"*",SearchOption.AllDirectories).OrderBy(x=>x,StringComparer.Ordinal).Select(x=>new ModuleFile { path=x.Substring(Root(directory).Length+1).Replace('\\','/'),hash=Hash(File.ReadAllBytes(x)) }).ToArray() };
            TextFile(directory,Marker,JsonUtility.ToJson(receipt,true));
        }
        private static string SourceTarget(string file)
        {
            if(file.StartsWith("Runtime/",StringComparison.Ordinal))return file;
            if(file.StartsWith("Samples~/Integrations/",StringComparison.Ordinal))return "Runtime/Integrations/"+file.Substring("Samples~/Integrations/".Length);
            throw new InvalidDataException("Unsupported source root: "+file);
        }
        private static void CopySource(string repository,string directory,string file,string target,string id)
        { string source=Relative(repository,file);if(!File.Exists(source)||!File.Exists(source+".meta"))throw new FileNotFoundException("Missing source/meta: "+file);WriteFile(directory,target,File.ReadAllBytes(source));WriteFile(directory,target+".meta",File.ReadAllBytes(source+".meta")); }
        public static string[] MissingSdkTypes(IEnumerable<ModuleDefinition> modules)
        { return modules.SelectMany(m=>m.sdkTypes??new string[0]).Distinct().Where(name=>!AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetType(name)!=null)).ToArray(); }
        public static void Export(string repository,string output,IEnumerable<string> selected)
        {
            var catalog=ReadCatalog(repository); output=Root(output); PlainPath(output); if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new IOException("Export folder must be empty");
            Directory.CreateDirectory(output); foreach(var module in Resolve(catalog,selected))Generate(repository,Relative(output,module.package),module,catalog);
        }
        private static void CopyPackage(string source,string target)
        { foreach(var path in Directory.GetFiles(source,"*",SearchOption.AllDirectories))WriteFile(target,path.Substring(Root(source).Length+1),File.ReadAllBytes(path)); }
        private static void CheckDependents(string project,ModuleCatalog catalog,IEnumerable<string> removed,IEnumerable<string> owned)
        {
            var removedModules=catalog.modules.Where(x=>removed.Contains(x.id)).ToArray();
            string manifest=File.ReadAllText(Path.Combine(project,"Packages/manifest.json")); foreach(var module in removedModules)if(Regex.IsMatch(manifest,Regex.Escape(Quote(module.package))+@"\s*:"))throw new IOException("Remove the explicit manifest dependency before uninstalling "+module.id);
            foreach(var path in Directory.GetFiles(Path.Combine(project,"Packages"),"package.json",SearchOption.AllDirectories)) {
                if(catalog.modules.Any(x=>owned.Contains(x.id)&&Inside(path,PackagePath(project,x))))continue;
                var text=File.ReadAllText(path); foreach(var module in removedModules)if(Regex.IsMatch(text,Regex.Escape(Quote(module.package))+@"\s*:"))throw new IOException("Another package depends on "+module.id+": "+path);
            }
            foreach(var path in Directory.GetFiles(Path.Combine(project,"Assets"),"*.asmdef",SearchOption.AllDirectories)) { string text=File.ReadAllText(path); foreach(var module in removedModules)if(text.Contains(Quote(module.assembly)) || text.IndexOf(Quote("GUID:"+Hash(Encoding.UTF8.GetBytes(module.package+":Runtime/"+module.assembly+".asmdef")).Substring(0,32)),StringComparison.OrdinalIgnoreCase)>=0)throw new IOException("Remove the assembly reference before removing "+module.id+": "+path); }
        }
        public static string Apply(string repository,string project,IEnumerable<string> selected,string restoreBackup=null)
        {
            repository=Root(repository); project=Root(project); ValidateProject(repository,project); var catalog=ReadCatalog(repository);
            if(restoreBackup!=null) { restoreBackup=Root(restoreBackup); if(!Inside(restoreBackup,Path.Combine(project,"ProjectSettings/BaseUnityModuleBackups")))throw new IOException("Backup must belong to this project"); PlainPath(restoreBackup); selected=JsonUtility.FromJson<ModuleBackup>(File.ReadAllText(Path.Combine(restoreBackup,"operation.json"))).before; }
            var plan=Preview(repository,project,selected);
            if(restoreBackup==null&&Root(Path.Combine(Application.dataPath,"..")).Equals(project,StringComparison.OrdinalIgnoreCase)) {
                var missing=MissingSdkTypes(Resolve(catalog,plan.selected));if(missing.Length>0)throw new InvalidOperationException("Install the official SDK first, then retry: "+string.Join(", ",missing));
            }
            var installed=Installed(repository,project); CheckDependents(project,catalog,plan.removed,installed);
            string stamp=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"); string stage=Relative(project,"Library/BaseUnityModuleStaging/"+stamp); string backup=Relative(project,"ProjectSettings/BaseUnityModuleBackups/"+stamp);
            Directory.CreateDirectory(stage);
            foreach(var module in Resolve(catalog,plan.selected)) {
                string target=Relative(stage,module.package);
                if(restoreBackup==null)Generate(repository,target,module,catalog);
                else { string source=Relative(restoreBackup,module.package); Clean(source,module); CopyPackage(source,target); }
            }
            // Recheck ownership/edits immediately before mutation. Staging has not touched installed packages.
            foreach(var module in catalog.modules.Where(x=>installed.Contains(x.id)))Clean(PackagePath(project,module),module);
            Directory.CreateDirectory(backup); TextFile(backup,"operation.json",JsonUtility.ToJson(new ModuleBackup { before=installed,after=plan.selected,version=plan.version },true));
            var moved=new List<ModuleDefinition>(); var added=new List<ModuleDefinition>();
            try {
                foreach(var module in catalog.modules.Where(x=>installed.Contains(x.id))) { Directory.Move(PackagePath(project,module),Relative(backup,module.package)); moved.Add(module); }
                foreach(var module in Resolve(catalog,plan.selected)) { Directory.Move(Relative(stage,module.package),PackagePath(project,module)); added.Add(module); }
            } catch {
                foreach(var module in added.AsEnumerable().Reverse())Directory.Move(PackagePath(project,module),Relative(stage,"rollback-"+module.package));
                foreach(var module in moved.AsEnumerable().Reverse())Directory.Move(Relative(backup,module.package),PackagePath(project,module));
                throw;
            }
            return backup;
        }
    }
}
