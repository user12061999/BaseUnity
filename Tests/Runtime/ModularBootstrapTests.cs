using System.Collections;
using BaseUnity.Audio;
using BaseUnity.Core;
using BaseUnity.Features;
using BaseUnity.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace BaseUnity.Tests
{
    public sealed class ModularBootstrapTests
    {
        [UnityTest] public IEnumerator CoreDoesNotCreateOptionalServices()
        {
            var root=new GameObject("Core-only test"); root.SetActive(false); var bootstrap=root.AddComponent<ModuleBootstrap>(); root.SetActive(true); yield return null;
            Assert.IsTrue(bootstrap.IsReady); Assert.IsNotNull(bootstrap.Services.Get<EventBus>());
            Assert.IsFalse(bootstrap.Services.TryGet<FeatureServices>(out _)); Assert.IsFalse(bootstrap.Services.TryGet<AudioService>(out _)); Assert.IsFalse(bootstrap.Services.TryGet<ISaveStore>(out _));
            Object.Destroy(root); yield return null; Assert.IsNull(ModuleBootstrap.Instance);
        }
        [UnityTest] public IEnumerator InstallerOrderingRegistersSaveBeforeAudioAndDisposesSafely()
        {
            var root=new GameObject("Optional module test"); root.SetActive(false); root.AddComponent<AudioModule>(); root.AddComponent<SaveModule>(); var bootstrap=root.AddComponent<ModuleBootstrap>(); root.SetActive(true); yield return null;
            Assert.IsTrue(bootstrap.IsReady); Assert.IsNotNull(bootstrap.Services.Get<ISaveStore>()); Assert.IsNotNull(bootstrap.Services.Get<AudioService>()); Assert.IsFalse(bootstrap.Services.TryGet<FeatureServices>(out _));
            Object.Destroy(root); yield return null; Assert.IsNull(ModuleBootstrap.Instance);
        }
    }
}
