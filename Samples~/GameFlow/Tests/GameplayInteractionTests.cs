using System;
using System.Collections;
using BaseUnity.Core;
using BaseUnity.Features;
using BaseUnity.Input;
using BaseUnity.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace BaseUnity.GameFlow.Tests
{
    public sealed class InteractionTestBootstrap : GameBootstrap { }
    public sealed class GameplayInteractionTests
    {
        private GameObject owner, canvasObject, eventsObject;
        private InteractionTestBootstrap bootstrap;
        private FeatureConfig config;
        private string slot;
        private EventSystem events;
        [UnitySetUp] public IEnumerator Setup()
        {
            if(GameBootstrap.Instance) { UnityEngine.Object.Destroy(GameBootstrap.Instance.gameObject); yield return null; }
            slot="interaction_test_"+Guid.NewGuid().ToString("N"); config=ScriptableObject.CreateInstance<FeatureConfig>(); config.offline.saveSlot=slot; config.gameplay.enabled=true;
            owner=new GameObject("Interaction tests"); owner.SetActive(false); bootstrap=owner.AddComponent<InteractionTestBootstrap>(); bootstrap.FeatureConfig=config; owner.SetActive(true);
            canvasObject=new GameObject("Interaction test canvas",typeof(RectTransform),typeof(Canvas)); canvasObject.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            eventsObject=new GameObject("Interaction test events",typeof(EventSystem)); events=eventsObject.GetComponent<EventSystem>(); yield return null; Canvas.ForceUpdateCanvases();
        }
        private RectTransform Rect(string name,float width=200,float height=200)
        { var rect=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>(); rect.SetParent(canvasObject.transform,false); rect.sizeDelta=new Vector2(width,height); return rect; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(bootstrap && bootstrap.IsReady) bootstrap.Services.Get<ISaveStore>().Delete(slot);
            UnityEngine.Object.Destroy(owner); UnityEngine.Object.Destroy(canvasObject); UnityEngine.Object.Destroy(eventsObject); UnityEngine.Object.Destroy(config); yield return null;
        }
        [UnityTest] public IEnumerator HeldButtonOwnsOnePointerAndReleasesWhenGameplayIsBlocked()
        {
            var rect=Rect("Hold"); var button=rect.gameObject.AddComponent<MobileActionButton>(); var first=new PointerEventData(events){pointerId=1}; var second=new PointerEventData(events){pointerId=2};
            button.OnPointerDown(first); Assert.IsTrue(bootstrap.Services.Get<InputRouter>().IsPressed("Fire")); button.OnPointerDown(second); button.OnPointerUp(second); Assert.IsTrue(bootstrap.Services.Get<InputRouter>().IsPressed("Fire"));
            bootstrap.Services.Get<InputRouter>().Enabled=false; yield return null; Assert.IsFalse(bootstrap.Services.Get<ManualInputSource>().IsPressed("Fire"));
            bootstrap.Services.Get<InputRouter>().Enabled=true; Assert.IsFalse(bootstrap.Services.Get<InputRouter>().IsPressed("Fire"));
        }
        [UnityTest] public IEnumerator JoystickResetsVectorOnDisableAndIgnoresForeignPointer()
        {
            var area=Rect("Joystick"); var stick=area.gameObject.AddComponent<MobileJoystick>(); stick.area=area; Canvas.ForceUpdateCanvases();
            var center=RectTransformUtility.WorldToScreenPoint(null,area.position); var data=new PointerEventData(events){pointerId=1,position=center+new Vector2(90,0)};
            stick.OnPointerDown(data); Assert.Greater(stick.Value.x,.5f); stick.OnPointerUp(new PointerEventData(events){pointerId=2}); Assert.Greater(stick.Value.x,.5f);
            area.gameObject.SetActive(false); Assert.AreEqual(Vector2.zero,bootstrap.Services.Get<ManualInputSource>().ReadVector2("Move")); yield return null;
        }
        [UnityTest] public IEnumerator CollectIsOncePerActivationAndCannotRunWhilePaused()
        {
            var rect=Rect("Collectible"); var item=rect.gameObject.AddComponent<GameplayInteractable>(); item.mode=GameplayInteractionMode.Collect; item.hideOnCollect=false; int count=0; item.collected.AddListener(()=>{count++; item.OnPointerClick(new PointerEventData(events));});
            bootstrap.Services.Get<InputRouter>().Enabled=false; item.OnPointerClick(new PointerEventData(events)); Assert.AreEqual(0,count);
            bootstrap.Services.Get<InputRouter>().Enabled=true; item.OnPointerClick(new PointerEventData(events)); item.OnPointerClick(new PointerEventData(events)); Assert.AreEqual(1,count);
            rect.gameObject.SetActive(false); rect.gameObject.SetActive(true); item.OnPointerClick(new PointerEventData(events)); Assert.AreEqual(2,count); yield return null;
        }
        private sealed class PreferenceStore : ISaveStore
        {
            private string json;
            public void Save<T>(string slot,T value,int version=1) where T:class { json=JsonUtility.ToJson(value); }
            public bool TryLoad<T>(string slot,out T value,int version=1) where T:class { value=json==null?null:JsonUtility.FromJson<T>(json); return value!=null; }
            public void Delete(string slot) { json=null; }
        }
        [UnityTest] public IEnumerator SavedControlPreferencesApplyLayoutSensitivityAndReleaseHeldInput()
        {
            var area=Rect("Preference joystick"); area.anchoredPosition=new Vector2(-200,-200); var stick=area.gameObject.AddComponent<MobileJoystick>(); stick.area=area; Canvas.ForceUpdateCanvases();
            var layout=area.GetComponent<MobileControlLayout>(); var prefs=new PlayerPreferencesService(new PreferenceStore()); layout.Bind(prefs);
            stick.OnPointerDown(new PointerEventData(events){pointerId=1,position=RectTransformUtility.WorldToScreenPoint(null,area.position)+new Vector2(90,0)}); Assert.Greater(stick.Value.x,.5f);
            var next=prefs.Current; next.controlScale=1.2f; next.sensitivity=2; next.leftHanded=true; next.joystickOffset=new Vector2(-60,0); Assert.IsTrue(prefs.Save(next));
            Assert.AreEqual(Vector2.zero,stick.Value); Assert.AreEqual(1.2f,area.localScale.x,.001f); Assert.Greater(area.anchoredPosition.x,0);
            stick.OnPointerDown(new PointerEventData(events){pointerId=2,position=RectTransformUtility.WorldToScreenPoint(null,area.position)+new Vector2(90,0)}); Assert.AreEqual(1,stick.Value.x,.01f); yield return null;
        }
        [UnityTest] public IEnumerator DropZoneAcceptsOneDropAndRestoresSourceRaycasts()
        {
            var source=Rect("Draggable"); var item=source.gameObject.AddComponent<GameplayInteractable>(); item.mode=GameplayInteractionMode.Drag;
            var zone=Rect("Drop zone").gameObject.AddComponent<GameplayDropZone>(); int count=0; zone.accepted.AddListener(value=>count++); Canvas.ForceUpdateCanvases();
            var data=new PointerEventData(events){pointerId=1,position=RectTransformUtility.WorldToScreenPoint(null,source.position),pointerDrag=source.gameObject};
            item.OnBeginDrag(data); Assert.IsTrue(item.Dragging); Assert.IsFalse(source.GetComponent<CanvasGroup>().blocksRaycasts);
            zone.OnDrop(data); zone.OnDrop(data); Assert.AreEqual(1,count); Assert.IsFalse(item.Dragging); Assert.IsTrue(source.GetComponent<CanvasGroup>().blocksRaycasts); yield return null;
        }
        [UnityTest] public IEnumerator EffectsAreBoundedAndReturnedAfterTheirLifetime()
        {
            var rect=Rect("Effects",900,1600); var effects=rect.gameObject.AddComponent<GameplayEffects>(); var labelObject=new GameObject("Text template",typeof(RectTransform),typeof(Text)); labelObject.transform.SetParent(canvasObject.transform,false); ((RectTransform)labelObject.transform).sizeDelta=new Vector2(200,80); var label=labelObject.GetComponent<Text>(); label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); effects.floatingTextPrefab=label; effects.maximumActive=2; effects.seconds=.05f;
            var center=RectTransformUtility.WorldToScreenPoint(null,rect.position); effects.Show("+1",center,Color.yellow); effects.Show("+2",center,Color.yellow); effects.Show("+3",center,Color.yellow); Assert.AreEqual(2,effects.ActiveCount);
            yield return new WaitForSecondsRealtime(.15f); Assert.AreEqual(0,effects.ActiveCount);
        }
    }
}
