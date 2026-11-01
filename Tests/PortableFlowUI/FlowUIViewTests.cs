using System.Collections;
using BaseUnity.Core;
using BaseUnity.Flow;
using BaseUnity.FlowUI;
using BaseUnity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace BaseUnity.Tests {
 public sealed class FlowUIViewTests {
  [UnityTest] public IEnumerator FlowUpdatesScreensResultsAndInputGate() {
   var root=new GameObject("flow test");root.SetActive(false);
   var module=root.AddComponent<GameFlowModule>();module.levelCount=2;root.AddComponent<UIModule>();var bootstrap=root.AddComponent<ModuleBootstrap>();
   var viewObject=new GameObject("UI");viewObject.transform.SetParent(root.transform);var view=viewObject.AddComponent<GameFlowView>();
   view.home=Panel("Home",viewObject.transform);view.ingame=Panel("Game",viewObject.transform);view.loading=Panel("Loading",viewObject.transform);
   view.pause=Panel("Pause",viewObject.transform);view.win=Panel("Win",viewObject.transform);view.lose=Panel("Lose",viewObject.transform);
   root.SetActive(true);yield return null;yield return null;
   Assert.IsTrue(bootstrap.IsReady);Assert.IsTrue(view.home.IsVisible);Assert.IsFalse(view.ingame.IsVisible);
   module.Play();yield return null;Assert.IsTrue(view.ingame.IsVisible);Assert.IsTrue(module.Flow.CanPlay);
   module.Pause();Assert.IsTrue(view.pause.IsVisible);Assert.IsFalse(module.Flow.CanPlay);
   module.Resume();Assert.IsFalse(view.pause.IsVisible);module.Win();Assert.IsTrue(view.win.IsVisible);
   module.Next();Assert.AreEqual(2,module.Flow.Level);Assert.IsFalse(view.win.IsVisible);
   module.Lose();Assert.IsTrue(view.lose.IsVisible);module.Retry();Assert.IsFalse(view.lose.IsVisible);Assert.AreEqual(2,module.Flow.Level);
   Object.Destroy(root);yield return null;Assert.IsNull(ModuleBootstrap.Instance);
  }
  private static UIPanel Panel(string name,Transform parent) { var go=new GameObject(name);go.transform.SetParent(parent);go.SetActive(false);return go.AddComponent<UIPanel>(); }
 }
}