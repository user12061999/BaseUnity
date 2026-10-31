using System.Collections;
using BaseUnity.Input;
using BaseUnity.MobileInput;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
namespace BaseUnity.Tests
{
    public sealed class PortableInputTests
    {
        [UnityTest] public IEnumerator DisabledContextBlocksPressAndHeldInputIsReleased()
        {
            var root=new GameObject("Portable input test"); var context=root.AddComponent<TouchInputContext>(); var manual=new ManualInputSource(); var router=new InputRouter(manual); context.Bind(manual,router);
            var eventRoot=new GameObject("Input test event",typeof(EventSystem)); var button=root.AddComponent<TouchButton>(); button.context=context; var data=new PointerEventData(eventRoot.GetComponent<EventSystem>()){pointerId=1};
            button.OnPointerDown(data); Assert.IsTrue(router.IsPressed("Fire")); button.OnPointerUp(new PointerEventData(eventRoot.GetComponent<EventSystem>()){pointerId=2}); Assert.IsTrue(router.IsPressed("Fire"));
            context.enabled=false; Assert.IsFalse(manual.IsPressed("Fire")); button.OnPointerDown(data); Assert.IsFalse(manual.IsPressed("Fire")); yield return null;
            context.enabled=true; button.OnPointerDown(data); Assert.IsTrue(router.IsPressed("Fire")); router.Enabled=false; yield return null; Assert.IsFalse(manual.IsPressed("Fire"));
            Object.Destroy(root); Object.Destroy(eventRoot); yield return null;
        }
    }
}
