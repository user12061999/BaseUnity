using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
namespace BaseUnity.Modularity
{
    public sealed class ModuleFileReview
    {
        public string path,installedPath,sourcePath;
        public bool sourceChanged,localChanged,conflict;
    }
    public sealed class ModuleVersionReport
    {
        public string id,installedVersion,sourceVersion,status,error,package,signature;
        public ModuleFileReview[] files=new ModuleFileReview[0];
        public bool HasLocalChanges => files.Any(f=>f.localChanged);
        public bool Installed => installedVersion!=null;
    }
    public static partial class ModulePackageStore
    {
        public static string SourceVersion(string repository) => Version(repository);
        public static string InstalledVersion(string repository,string project,string id)
        { var module=ReadCatalog(repository).modules.Single(m=>m.id==id);string dir=PackagePath(project,module);if(!Directory.Exists(dir))return null;try { return Receipt(dir).version??"unknown"; }catch(Exception) { return "unmanaged"; } }
        public static int? CompareVersions(string first,string second)
        {
            var pattern=@"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z";
            var a=Regex.Match(first??"",pattern);var b=Regex.Match(second??"",pattern);if(!a.Success||!b.Success)return null;
            foreach(var match in new[]{a,b})if(match.Groups[4].Success&&match.Groups[4].Value.Split('.').Any(part=>part.Length>1&&part[0]=='0'&&part.All(c=>c>='0'&&c<='9')))return null;
            for(int i=1;i<=3;i++) { var x=a.Groups[i].Value;var y=b.Groups[i].Value;int c=x.Length.CompareTo(y.Length);if(c==0)c=string.CompareOrdinal(x,y);if(c!=0)return Math.Sign(c); }
            string ap=a.Groups[4].Value,bp=b.Groups[4].Value;if(ap==bp)return 0;if(ap.Length==0)return 1;if(bp.Length==0)return -1;
            var ax=ap.Split('.');var bx=bp.Split('.');
            for(int i=0;i<Math.Min(ax.Length,bx.Length);i++) {
                bool an=ax[i].All(c=>c>='0'&&c<='9'),bn=bx[i].All(c=>c>='0'&&c<='9');int c;
                if(an&&bn) { string x=ax[i].TrimStart('0'),y=bx[i].TrimStart('0');c=x.Length.CompareTo(y.Length);if(c==0)c=string.CompareOrdinal(x,y); }
                else c=an!=bn?(an?-1:1):string.CompareOrdinal(ax[i],bx[i]);
                if(c!=0)return Math.Sign(c);
            }return Math.Sign(ax.Length.CompareTo(bx.Length));
        }
        private static string[] PackageFiles(string directory)
        {
            PlainPath(directory);var files=new List<string>();var pending=new Stack<string>();pending.Push(Root(directory));
            while(pending.Count>0)foreach(string item in Directory.GetFileSystemEntries(pending.Pop())) {
                if((File.GetAttributes(item)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked file/folder must be resolved manually: "+item);
                if(Directory.Exists(item))pending.Push(item);else files.Add(item);
            }return files.ToArray();
        }
        private static void VerifyOwned(string directory,ModuleDefinition module)
        { var receipt=Receipt(directory);if(receipt.id!=module.id)throw new IOException("Package receipt ID mismatch");foreach(var file in receipt.files)Relative(directory,file.path);PackageFiles(directory); }
        private static void CheckForUpdate(string directory,ModuleDefinition module,string[] replaceLocalModuleIds)
        { if((replaceLocalModuleIds??new string[0]).Contains(module.id))VerifyOwned(directory,module);else Clean(directory,module); }
        private static void WriteMarkerAtomic(string directory,byte[] bytes)
        {
            string marker=Relative(directory,Marker),temp=Relative(directory,Marker+".snapshot-"+Guid.NewGuid().ToString("N"));
            try { File.WriteAllBytes(temp,bytes);File.Replace(temp,marker,null); }
            finally { if(File.Exists(temp))File.Delete(temp); }
        }
        private static void CaptureSnapshot(string directory)
        {
            var receipt=Receipt(directory);
            receipt.originalMarkerBase64=Convert.ToBase64String(File.ReadAllBytes(Path.Combine(directory,Marker)));receipt.hasBackupSnapshot=true;receipt.backupSnapshot=PackageFiles(directory).Where(p=>!Root(p).Equals(Root(Path.Combine(directory,Marker)),StringComparison.OrdinalIgnoreCase)).Select(p=>new ModuleFile { path=p.Substring(Root(directory).Length+1).Replace('\\','/'),hash=Hash(File.ReadAllBytes(p)) }).ToArray();
            WriteMarkerAtomic(directory,new System.Text.UTF8Encoding(false).GetBytes(JsonUtility.ToJson(receipt,true)));
        }
        private static void ClearSnapshot(string directory)
        {
            var receipt=Receipt(directory);if(!receipt.hasBackupSnapshot)return;
            if(!string.IsNullOrEmpty(receipt.originalMarkerBase64)) {
                byte[] original=Convert.FromBase64String(receipt.originalMarkerBase64);var baseline=JsonUtility.FromJson<ModuleReceipt>(System.Text.Encoding.UTF8.GetString(original).TrimStart('\uFEFF'));
                if(baseline==null||baseline.owner!="BaseUnity.ModuleManager"||baseline.id!=receipt.id||baseline.files==null)throw new IOException("Invalid original package marker");
                foreach(var file in baseline.files)Relative(directory,file.path);WriteMarkerAtomic(directory,original);
            } else { receipt.hasBackupSnapshot=false;receipt.backupSnapshot=null;WriteMarkerAtomic(directory,new System.Text.UTF8Encoding(false).GetBytes(JsonUtility.ToJson(receipt,true))); }
        }
        public static ModuleVersionReport[] ReviewInstalled(string repository,string project)
        {
            repository=Root(repository);project=Root(project);var catalog=ReadCatalog(repository);string version=Version(repository);var result=new List<ModuleVersionReport>();
            string stage=Relative(project,"Library/BaseUnityModuleReview/"+Guid.NewGuid().ToString("N"));
            foreach(var module in catalog.modules.Where(m=>Directory.Exists(PackagePath(project,m)))) {
                var report=new ModuleVersionReport { id=module.id,package=module.package,sourceVersion=version,installedVersion="unknown" };result.Add(report);
                try {
                    string installed=PackagePath(project,module);VerifyOwned(installed,module);var receipt=Receipt(installed);report.installedVersion=receipt.version??"unknown";
                    string generated=Relative(stage,module.package);Generate(repository,generated,module,catalog);var target=Receipt(generated);
                    var baseline=receipt.files.ToDictionary(f=>f.path,f=>f.hash,StringComparer.OrdinalIgnoreCase);var wanted=target.files.ToDictionary(f=>f.path,f=>f.hash,StringComparer.OrdinalIgnoreCase);
                    var actual=PackageFiles(installed).Where(p=>!Root(p).Equals(Root(Path.Combine(installed,Marker)),StringComparison.OrdinalIgnoreCase))
                        .Where(p=>!(p.EndsWith(".meta",StringComparison.OrdinalIgnoreCase)&&Directory.Exists(p.Substring(0,p.Length-5))))
                        .ToDictionary(p=>p.Substring(Root(installed).Length+1).Replace('\\','/'),p=>Hash(File.ReadAllBytes(p)),StringComparer.OrdinalIgnoreCase);
                    var files=new List<ModuleFileReview>();
                    foreach(string path in baseline.Keys.Union(wanted.Keys,StringComparer.OrdinalIgnoreCase).Union(actual.Keys,StringComparer.OrdinalIgnoreCase).OrderBy(p=>p,StringComparer.Ordinal)) {
                        baseline.TryGetValue(path,out var old);wanted.TryGetValue(path,out var next);actual.TryGetValue(path,out var local);
                        bool sourceChanged=old!=next,localChanged=old!=local;if(!sourceChanged&&!localChanged)continue;
                        files.Add(new ModuleFileReview { path=path,installedPath=Relative(installed,path),sourcePath=Relative(generated,path),sourceChanged=sourceChanged,localChanged=localChanged,conflict=sourceChanged&&localChanged&&next!=local });
                    }
                    report.signature=Hash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n",baseline.OrderBy(k=>k.Key).Select(k=>"B:"+k.Key+":"+k.Value).Concat(wanted.OrderBy(k=>k.Key).Select(k=>"S:"+k.Key+":"+k.Value)).Concat(actual.OrderBy(k=>k.Key).Select(k=>"L:"+k.Key+":"+k.Value)))+Hash(File.ReadAllBytes(Path.Combine(installed,Marker)))));
                    report.files=files.ToArray();var comparison=CompareVersions(version,report.installedVersion);
                    report.status=!comparison.HasValue?"Unknown version":comparison.Value>0?"Update available":comparison.Value<0?"Installed is newer":files.Any(f=>f.sourceChanged)?"Source changed":"Up to date";
                    if(report.HasLocalChanges)report.status+=" / local edits";
                } catch(Exception error) { report.error=error.Message;report.status="Needs manual resolution"; }
            }
            string packages=Path.Combine(project,"Packages");if(Directory.Exists(packages))foreach(string dir in Directory.GetDirectories(packages,"com.baseunity.*")) {
                if(catalog.modules.Any(m=>m.package==Path.GetFileName(dir)))continue;
                result.Add(new ModuleVersionReport { id=Path.GetFileName(dir),package=Path.GetFileName(dir),installedVersion="unknown",sourceVersion=version,status="Unknown to catalog",error="This package is outside the current catalog; review its source/version manually" });
            }
            return result.ToArray();
        }
    }
}
