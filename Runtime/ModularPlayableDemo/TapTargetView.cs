using UnityEngine;
namespace BaseUnity.Demo
{
    public sealed class TapTargetView : MonoBehaviour
    {
        public PlayableDemoController controller;
        public void Hit() { if(!controller)controller=GetComponentInParent<PlayableDemoController>();if(controller)controller.HitTarget(); }
    }
}
