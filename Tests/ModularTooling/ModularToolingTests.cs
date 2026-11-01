using System;
using System.IO;
using System.Linq;
using BaseUnity.Modularity;
using BaseUnity.Flow;
using BaseUnity.FlowEconomy;
using BaseUnity.EconomyModules;
using BaseUnity.Features;
using BaseUnity.IntegrationModules;
using BaseUnity.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace BaseUnity.Tests
{
    public sealed class ModularToolingTests
    {
        [Test] public void PresetsResolveWithoutNativeSdkAndReportScriptCount()
        {
            var catalog=ModulePackageStore.ReadCatalog(Path.GetFullPath(Path.Combine(Application.dataPath,"..")));
            foreach(var preset in ModulePresets.All) {
                var modules=ModulePackageStore.Resolve(catalog,preset.modules);Assert.IsTrue(modules.All(m=>(m.sdkTypes?.Length??0)==0));Assert.That(ModulePresets.Summary(catalog,preset.modules),Does.Contain("scripts"));
            }
            CollectionAssert.AreEqual(new[]{"core"},ModulePackageStore.Resolve(catalog,ModulePresets.All[0].modules).Select(m=>m.id));
            var shop=ModulePackageStore.Resolve(catalog,new[]{"ads-simulation"}).Select(m=>m.id).ToArray();Assert.IsFalse(shop.Contains("firebase"));Assert.IsFalse(shop.Contains("wallet-rewards"));Assert.IsFalse(shop.Contains("ads-admob"));
        }
        [Test] public void SdkPrerequisiteMetadataReportsUnknownTypes()
        { var missing=ModulePackageStore.MissingSdkTypes(new[]{new ModuleDefinition { sdkTypes=new[]{"DefinitelyMissingSdk.Type"} }});CollectionAssert.AreEqual(new[]{"DefinitelyMissingSdk.Type"},missing); }
        [Test] public void ValidatorCatchesMissingCatalogSceneAndInstallerDependency()
        {
            var root=new GameObject("invalid");root.SetActive(false);
            try { root.AddComponent<ModuleBootstrap>();root.AddComponent<GameFlowModule>().homeScene="NoSuchScene";root.AddComponent<ShopModule>();root.AddComponent<DailyRewardModule>();root.AddComponent<FlowEconomyModule>();
                var issues=ModularBuildValidator.ValidateRoots(new[]{root},BuildTarget.Android);Assert.IsTrue(issues.Any(i=>i.error&&i.message.Contains("NoSuchScene")));Assert.IsTrue(issues.Any(i=>i.message.Contains("catalog")));Assert.IsTrue(issues.Any(i=>i.message.Contains("DailyRewardSchedule")));Assert.IsTrue(issues.Any(i=>i.message.Contains("WalletModule")));Assert.IsTrue(issues.Any(i=>i.message.Contains("FlowRewardConfig")));
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test] public void ValidatorRejectsSaveSlotOutsideJsonStoreAsciiAlphabet()
        {
            var root=new GameObject("slot test");root.SetActive(false);
            try { root.AddComponent<ModuleBootstrap>();root.AddComponent<BaseUnity.Save.SaveModule>();root.AddComponent<WalletModule>().saveSlot="\u0111_save";
                Assert.IsTrue(ModularBuildValidator.ValidateRoots(new[]{root},BuildTarget.Android).Any(i=>i.error&&i.message.Contains("save slot")));
            } finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test] public void ValidatorFindsDuplicateOfferIdsAndInvalidRewards()
        {
            var root=new GameObject("invalid catalog");root.SetActive(false);var catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.offers=new[]{new ShopOffer { rewards=new[]{new EconomyReward { amount=-1 }} },new ShopOffer()};
            try { root.AddComponent<ModuleBootstrap>();root.AddComponent<BaseUnity.Save.SaveModule>();root.AddComponent<WalletModule>();root.AddComponent<ShopModule>().catalog=catalog;
                var issues=ModularBuildValidator.ValidateRoots(new[]{root},BuildTarget.Android);Assert.IsTrue(issues.Any(i=>i.message.Contains("Duplicate")));Assert.IsTrue(issues.Any(i=>i.message.Contains("reward")));
            } finally { UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(catalog); }
        }
        [Test] public void ValidatorBlocksSimulationInReleaseAndSkipsDisabledFeature()
        {
            bool before=EditorUserBuildSettings.development;var root=new GameObject("simulation");root.SetActive(false);var provider=ScriptableObject.CreateInstance<SimulatedFirebaseProvider>();
            try { EditorUserBuildSettings.development=false;root.AddComponent<ModuleBootstrap>();var module=root.AddComponent<FirebaseModule>();module.provider=provider;
                Assert.IsTrue(ModularBuildValidator.ValidateRoots(new[]{root},BuildTarget.Android).Any(i=>i.error&&i.message.Contains("development-only")));
                Assert.IsFalse(ModularBuildValidator.ValidateRoots(new[]{root},BuildTarget.Android,true).Any(i=>i.error&&i.message.Contains("development-only")));
                module.settings.enabled=false;Assert.IsFalse(ModularBuildValidator.ValidateRoots(new[]{root},BuildTarget.Android).Any(i=>i.message.Contains("development-only")));
            } finally { EditorUserBuildSettings.development=before;UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(provider); }
        }
    }
}
