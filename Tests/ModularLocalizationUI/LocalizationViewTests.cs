using System.Collections;
using BaseUnity.Core;
using BaseUnity.Save;
using BaseUnity.Localization.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace BaseUnity.Tests
{
    public sealed class LocalizationTestBootstrap : ModuleBootstrap
    {
        private sealed class MemoryStore : ISaveStore {
            public void Save<T>(string key,T data,int version=1) where T:class { }
            public bool TryLoad<T>(string key,out T data,int version=1) where T:class { data=null;return false; }
            public void Delete(string key) { }
        }
        protected override void Configure(ServiceRegistry services) { services.Register(new BaseUnity.Localization.LocalizationService(new MemoryStore())); }
    }
    public sealed class LocalizationViewTests
    {
        [UnityTest] public IEnumerator DynamicTextAndLanguageChangesPreserveEnglishSources()
        {
            var root=new GameObject("Localized test");root.SetActive(false);var bootstrap=root.AddComponent<LocalizationTestBootstrap>();
            var canvas=new GameObject("Canvas");canvas.transform.SetParent(root.transform);var view=canvas.AddComponent<LocalizedCanvas>();
            var go=new GameObject("Text",typeof(RectTransform),typeof(Text));go.transform.SetParent(canvas.transform);var label=go.GetComponent<Text>();label.text="Play";
            try {
                root.SetActive(true);yield return null;yield return null;var service=bootstrap.Services.Get<BaseUnity.Localization.LocalizationService>();service.SetLanguage("vi");yield return null;Assert.AreEqual("Chơi",label.text);
                label.text="coin: 1000";yield return null;Assert.AreEqual("Xu: 1.000",label.text);service.SetLanguage("en");yield return null;Assert.AreEqual("coin: 1000",label.text);
                service.SetLanguage("vi");yield return null;canvas.SetActive(false);canvas.SetActive(true);yield return null;Assert.AreEqual("Xu: 1.000",label.text);service.SetLanguage("en");yield return null;Assert.AreEqual("coin: 1000",label.text);
            } finally { Object.Destroy(root); }
            yield return null;
        }
    }
}
