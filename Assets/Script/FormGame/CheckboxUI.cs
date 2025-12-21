using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CheckboxUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image checkmark;
    [SerializeField] private TMP_Text label;

    public bool Locked { get; private set; }
    public bool IsOn { get; private set; }

    public string Id { get; private set; } = "";
    public string LabelText => label ? label.text : "";

    public event Action<CheckboxUI, bool> OnChanged;

    public void Set(string id, string labelText)
    {
        Id = id ?? "";
        if (label) label.text = labelText ?? "";
    }

    public void SetLabel(string text)
    {
        if (label) label.text = text ?? "";
    }

    public void SetOn(bool on, bool notify = true)
    {
        bool changed = IsOn != on;
        IsOn = on;

        Debug.Log($"[CheckboxUI] SetOn name={name} id={Id} -> {on} " +
                  $"checkmark={(checkmark ? checkmark.name : "NULL")} " +
                  $"sprite={(checkmark && checkmark.sprite ? checkmark.sprite.name : "NULL")}");

        if (checkmark) checkmark.enabled = on;

        if (changed && notify) OnChanged?.Invoke(this, IsOn);
    }


    public void Toggle() => SetOn(!IsOn);

    public void SetLocked(bool locked) => Locked = locked;

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"[CheckboxUI] Click name={name} id={Id} locked={Locked} isOn={IsOn} " +
                  $"raycast={eventData.pointerPressRaycast.gameObject.name}");

        if (Locked) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;
        Toggle();
    }

}
