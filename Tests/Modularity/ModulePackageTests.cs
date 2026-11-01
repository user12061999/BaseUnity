using System;
using System.IO;
using System.Linq;
using BaseUnity.Modularity;
using NUnit.Framework;
using UnityEngine;
namespace BaseUnity.Modularity.Tests
{
    public sealed class ModulePackageTests
    {
        private string source,temp,project;
        [SetUp] public void Setup()
        {
            source=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            temp=Path.Combine(Path.GetTempPath(),"BaseUnityModules-"+Guid.NewGuid().ToString("N")); project=Path.Combine(temp,"Project");
            Directory.CreateDirectory(Path.Combine(project,"Assets")); Directory.CreateDirectory(Path.Combine(project,"Packages")); File.WriteAllText(Path.Combine(project,"Packages/manifest.json"),"{\"dependencies\":{}}");
        }
        [TearDown] public void Cleanup() { string checkedPath=Path.GetFullPath(temp); if(checkedPath.StartsWith(Path.Combine(Path.GetTempPath(),"BaseUnityModules-"),StringComparison.OrdinalIgnoreCase)&&Directory.Exists(checkedPath))Directory.Delete(checkedPath,true); }
        [Test] public void DependenciesAreMinimalAndTopologicallyOrdered()
        {
            var catalog=ModulePackageStore.ReadCatalog(source);
            CollectionAssert.AreEqual(new[]{"core","save","audio"},ModulePackageStore.Resolve(catalog,new[]{"audio"}).Select(x=>x.id));
            CollectionAssert.AreEqual(new[]{"core","pooling"},ModulePackageStore.Resolve(catalog,new[]{"pooling"}).Select(x=>x.id));
            CollectionAssert.AreEqual(new[]{"core","gameflow"},ModulePackageStore.Resolve(catalog,new[]{"gameflow"}).Select(x=>x.id));
            CollectionAssert.AreEqual(new[]{"core","ui"},ModulePackageStore.Resolve(catalog,new[]{"ui"}).Select(x=>x.id));
            CollectionAssert.AreEqual(new[]{"core","gameflow","ui","gameflow-ui"},ModulePackageStore.Resolve(catalog,new[]{"gameflow-ui"}).Select(x=>x.id));
            CollectionAssert.AreEqual(new[]{"core","mobile-input"},ModulePackageStore.Resolve(catalog,new[]{"mobile-input"}).Select(x=>x.id));
            Assert.Throws<InvalidDataException>(()=>ModulePackageStore.Resolve(catalog,new[]{"unknown"}));
            catalog.modules[0].requires=new[]{"audio"}; Assert.Throws<InvalidDataException>(()=>ModulePackageStore.Resolve(catalog,new[]{"audio"}));
        }
        [Test] public void CoreExportContainsNoLegacyBootstrapOrOptionalFeatureCode()
        {
            string output=Path.Combine(temp,"Export"); ModulePackageStore.Export(source,output,new[]{"core"});
            Assert.AreEqual(1,Directory.GetDirectories(output).Length);
            var scripts=Directory.GetFiles(output,"*.cs",SearchOption.AllDirectories); Assert.AreEqual(6,scripts.Length);
            Assert.IsFalse(scripts.Any(x=>Path.GetFileName(x)=="GameBootstrap.cs"));
            Assert.IsFalse(scripts.Select(File.ReadAllText).Any(x=>x.Contains("BaseUnity.Features")||x.Contains("AudioService")||x.Contains("ISaveStore")));
            Assert.Throws<IOException>(()=>ModulePackageStore.Export(source,output,new[]{"core"}));
        }
        [Test] public void FlowUIExportExcludesLegacyFeaturesAndGeneratedSamples()
        {
            string output=Path.Combine(temp,"FlowUI"); ModulePackageStore.Export(source,output,new[]{"gameflow-ui"});
            Assert.AreEqual(4,Directory.GetDirectories(output).Length);
            var scripts=Directory.GetFiles(output,"*.cs",SearchOption.AllDirectories);
            Assert.AreEqual(17,scripts.Length);
            Assert.IsFalse(scripts.Select(File.ReadAllText).Any(x=>x.Contains("BaseUnity.Features")||x.Contains("ISaveStore")||x.Contains("Firebase")));
            Assert.AreEqual(0,Directory.GetFiles(output,"*.prefab",SearchOption.AllDirectories).Length);
            Assert.IsFalse(Directory.Exists(Path.Combine(output,"com.baseunity.audio")));
        }
        [Test] public void InstallRemoveAndRestorePreserveProjectManifest()
        {
            string manifest=File.ReadAllText(Path.Combine(project,"Packages/manifest.json"));
            ModulePackageStore.Apply(source,project,new[]{"audio","pooling"}); Assert.AreEqual(4,ModulePackageStore.Installed(source,project).Length);
            string backup=ModulePackageStore.Apply(source,project,new[]{"pooling"}); CollectionAssert.AreEquivalent(new[]{"core","pooling"},ModulePackageStore.Installed(source,project));
            ModulePackageStore.Apply(source,project,new string[0],backup); Assert.AreEqual(4,ModulePackageStore.Installed(source,project).Length);
            Assert.AreEqual(manifest,File.ReadAllText(Path.Combine(project,"Packages/manifest.json")));
        }
        [Test] public void EditedOrUnmanagedFilesBlockRemovalWithoutChangingThem()
        {
            ModulePackageStore.Apply(source,project,new[]{"core"}); string file=Directory.GetFiles(Path.Combine(project,"Packages/com.baseunity.core"),"EventBus.cs",SearchOption.AllDirectories).Single();
            File.AppendAllText(file,"\n// project edit"); string original=File.ReadAllText(file);
            Assert.Throws<IOException>(()=>ModulePackageStore.Apply(source,project,new string[0])); Assert.AreEqual(original,File.ReadAllText(file));
        }
        [Test] public void ForeignPackageIsNeverOverwritten()
        {
            string dir=Path.Combine(project,"Packages/com.baseunity.core"); Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir,"mine.txt"),"mine");
            Assert.Throws<IOException>(()=>ModulePackageStore.Apply(source,project,new[]{"core"})); Assert.AreEqual("mine",File.ReadAllText(Path.Combine(dir,"mine.txt")));
        }
        [Test] public void UnmanagedAdditionalFileAndExternalAsmdefDependencyBlockUninstall()
        {
            ModulePackageStore.Apply(source,project,new[]{"save"}); string file=Path.Combine(project,"Packages/com.baseunity.save/MyExtra.cs"); File.WriteAllText(file,"// mine");
            Assert.Throws<IOException>(()=>ModulePackageStore.Apply(source,project,new[]{"core"})); File.Delete(file);
            File.WriteAllText(Path.Combine(project,"Assets/Game.asmdef"),"{\"name\":\"Game\",\"references\":[\"BaseUnity.Save\"]}");
            Assert.Throws<IOException>(()=>ModulePackageStore.Apply(source,project,new[]{"core"})); Assert.IsTrue(Directory.Exists(Path.Combine(project,"Packages/com.baseunity.save")));
            string metadata=File.ReadAllText(Path.Combine(project,"Packages/com.baseunity.save/Runtime/BaseUnity.Save.asmdef.meta")); string guid=metadata.Split('\n').Single(x=>x.StartsWith("guid:")).Substring(5).Trim();
            File.WriteAllText(Path.Combine(project,"Assets/Game.asmdef"),"{\"name\":\"Game\",\"references\":[\"GUID:"+guid+"\"]}"); Assert.Throws<IOException>(()=>ModulePackageStore.Apply(source,project,new[]{"core"}));
        }
        [Test] public void ExplicitManifestReferenceBlocksRemovalAndIsPreserved()
        {
            ModulePackageStore.Apply(source,project,new[]{"save"}); string file=Path.Combine(project,"Packages/manifest.json"); string value="{\"dependencies\":{\"com.baseunity.save\":\"file:com.baseunity.save\"}}"; File.WriteAllText(file,value);
            Assert.Throws<IOException>(()=>ModulePackageStore.Apply(source,project,new[]{"core"})); Assert.AreEqual(value,File.ReadAllText(file)); Assert.IsTrue(Directory.Exists(Path.Combine(project,"Packages/com.baseunity.save")));
        }
        [Test] public void TraversalInSourceCatalogIsRejected()
        {
            string repo=Path.Combine(temp,"BadRepo"); Directory.CreateDirectory(Path.Combine(repo,"Modules~")); File.Copy(Path.Combine(source,"package.json"),Path.Combine(repo,"package.json"));
            var catalog=ModulePackageStore.ReadCatalog(source); catalog.modules[0].files=new[]{"../outside.cs"}; File.WriteAllText(Path.Combine(repo,"Modules~/catalog.json"),JsonUtility.ToJson(catalog));
            Assert.Throws<IOException>(()=>ModulePackageStore.ReadCatalog(repo));
        }
        [Test] public void MissingSourceDuringUpdateLeavesInstalledPackageUnchanged()
        {
            ModulePackageStore.Apply(source,project,new[]{"core"}); string original=File.ReadAllText(Path.Combine(project,"Packages/com.baseunity.core/package.json"));
            string repo=Path.Combine(temp,"BrokenRepo"); Directory.CreateDirectory(Path.Combine(repo,"Modules~")); File.Copy(Path.Combine(source,"package.json"),Path.Combine(repo,"package.json")); File.Copy(Path.Combine(source,"Modules~/catalog.json"),Path.Combine(repo,"Modules~/catalog.json"));
            Assert.Throws<FileNotFoundException>(()=>ModulePackageStore.Apply(repo,project,new[]{"core"})); Assert.AreEqual(original,File.ReadAllText(Path.Combine(project,"Packages/com.baseunity.core/package.json")));
        }
    }
}
