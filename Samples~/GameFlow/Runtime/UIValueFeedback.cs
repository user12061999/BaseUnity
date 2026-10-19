using BaseUnity.Core;
using BaseUnity.Features;
using UnityEngine;
using UnityEngine.UI;
namespace BaseUnity.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class UIValueFeedback : MonoBehaviour
    {
        private UIFeedbackService service;
        private Selectable selectable;
        private Slider.SliderEvent sliderEvent;
        private Toggle.ToggleEvent toggleEvent;
        private Dropdown.DropdownEvent dropdownEvent;
        private void OnEnable() => Bind();
        public void Bind()
        {
            var owner = GameBootstrap.Instance; if (owner && owner.IsReady) owner.Services.TryGet(out service);
            selectable = GetComponent<Selectable>();
            var slider = GetComponent<Slider>(); if (slider && !ReferenceEquals(sliderEvent, slider.onValueChanged)) { sliderEvent?.RemoveListener(FloatChanged); sliderEvent = slider.onValueChanged; sliderEvent.AddListener(FloatChanged); }
            var toggle = GetComponent<Toggle>(); if (toggle && !ReferenceEquals(toggleEvent, toggle.onValueChanged)) { toggleEvent?.RemoveListener(BoolChanged); toggleEvent = toggle.onValueChanged; toggleEvent.AddListener(BoolChanged); }
            var dropdown = GetComponent<Dropdown>(); if (dropdown && !ReferenceEquals(dropdownEvent, dropdown.onValueChanged)) { dropdownEvent?.RemoveListener(IntChanged); dropdownEvent = dropdown.onValueChanged; dropdownEvent.AddListener(IntChanged); }
        }
        private void Changed() { if (isActiveAndEnabled && selectable && selectable.IsInteractable()) service?.Play(UICue.Change); }
        private void FloatChanged(float value) => Changed();
        private void BoolChanged(bool value) => Changed();
        private void IntChanged(int value) => Changed();
        private void OnDestroy() { sliderEvent?.RemoveListener(FloatChanged); toggleEvent?.RemoveListener(BoolChanged); dropdownEvent?.RemoveListener(IntChanged); }
    }
}
