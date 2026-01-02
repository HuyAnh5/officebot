using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AnomalyReportUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform checkboxList;      // Parent chứa các checkbox spawn
    [SerializeField] private CheckboxUI checkboxPrefab;   // prefab CheckboxUI
    [SerializeField] private Button reportButton;         // ReportButton

    [Header("Status TMP (loading/result)")]
    [SerializeField] private TMP_Text statusText;         // StatusTMP (có thể đặt ở bất kỳ đâu)

    [Header("Hook")]
    [SerializeField] private LevelManager levelManager;

    [Header("Layout (spawned rows)")]
    [SerializeField] private float checkboxPreferredHeight = 44f;
    [SerializeField] private float checkboxPreferredWidth = 0f;   // 0 = không ép width

    [Header("Label Style (override in ReportUI only)")]
    [SerializeField] private float labelFontSize = 18f;
    [SerializeField] private bool disableAutoSize = true;

    private readonly List<CheckboxUI> spawned = new();

    private readonly (string id, string label)[] items =
    {
        ("FORM_ERROR",      "FORM ERROR"),
        ("DISPLAY_GLITCH",  "DISPLAY GLITCH"),
        ("ANSWER_OVERRIDE", "ANSWER OVERRIDE"),
        ("TERMINAL_ANOMALY","TERMINAL ANOMALY"),
        ("MIMIC", "MIMIC")

    };

    private RectTransform _layoutRoot;

    private void Awake()
    {
        _layoutRoot = GetComponent<RectTransform>();

        Build();

        if (reportButton != null)
            reportButton.onClick.AddListener(OnReportClicked);

        ResetUIForNewLevel();
    }

    public void Build()
    {
        if (checkboxList == null || checkboxPrefab == null) return;

        // Clear cũ
        for (int i = checkboxList.childCount - 1; i >= 0; i--)
            Destroy(checkboxList.GetChild(i).gameObject);

        spawned.Clear();

        for (int i = 0; i < items.Length; i++)
        {
            var inst = Instantiate(checkboxPrefab, checkboxList);
            inst.name = $"Anomaly_{items[i].id}";
            inst.Set(items[i].id, items[i].label);
            inst.SetOn(false, notify: false);

            // Layout row (width/height)
            var le = inst.GetComponent<LayoutElement>();
            if (le == null) le = inst.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = checkboxPreferredHeight;
            if (checkboxPreferredWidth > 0f) le.preferredWidth = checkboxPreferredWidth;

            ApplyLabelStyle(inst.transform);

            inst.OnChanged += OnCheckboxChanged;
            spawned.Add(inst);
        }

        // Status TMP: đảm bảo có kích thước layout (để khỏi bị "cao = 0")
        if (statusText != null)
        {
            var le = statusText.GetComponent<LayoutElement>();
            if (le == null) le = statusText.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 44f;
        }

        RebuildLayout();
    }

    private void ApplyLabelStyle(Transform root)
    {
        TMP_Text best = null;
        var texts = root.GetComponentsInChildren<TMP_Text>(true);
        foreach (var t in texts)
        {
            var n = t.name.ToLowerInvariant();
            if (n.Contains("label") || n.Contains("text"))
            {
                best = t;
                break;
            }
        }
        if (best == null && texts.Length > 0) best = texts[0];
        if (best == null) return;

        if (disableAutoSize) best.enableAutoSizing = false;
        best.fontSize = labelFontSize;
    }

    private void OnCheckboxChanged(CheckboxUI who, bool on)
    {
        if (!on) return;

        // chỉ cho tick 1 cái
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != who)
                spawned[i].SetOn(false, notify: false);
    }

    public string GetSelectedId()
    {
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i].IsOn) return spawned[i].Id;
        return "";
    }

    // ====== UI STATES ======

    public void ResetUIForNewLevel()
    {
        // show controls
        SetCheckboxesVisible(true);
        if (reportButton) reportButton.gameObject.SetActive(true);

        // hide status
        if (statusText) statusText.gameObject.SetActive(false);

        // clear selection
        for (int i = 0; i < spawned.Count; i++)
            spawned[i].SetOn(false, notify: false);

        SetInteractable(true);
        RebuildLayout();
    }

    public void ShowFixingText(string msg)
    {
        // hide controls (KHÔNG tắt checkboxList để tránh ẩn luôn status nếu status nằm trong list)
        SetCheckboxesVisible(false);
        if (reportButton) reportButton.gameObject.SetActive(false);

        if (statusText)
        {
            statusText.gameObject.SetActive(true);
            statusText.text = msg;
        }

        RebuildLayout();
    }

    public void ShowResultText(string msg)
    {
        if (statusText)
        {
            statusText.gameObject.SetActive(true);
            statusText.text = msg;
        }
        RebuildLayout();
    }

    private void SetCheckboxesVisible(bool on)
    {
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null)
                spawned[i].gameObject.SetActive(on);
    }

    public void SetInteractable(bool on)
    {
        if (reportButton) reportButton.interactable = on;
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null)
                spawned[i].SetLocked(!on);
    }

    private void RebuildLayout()
    {
        Canvas.ForceUpdateCanvases();
        if (_layoutRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(_layoutRoot);
    }

    // ====== Events ======
    private void OnReportClicked()
    {
        string selected = GetSelectedId();
        if (levelManager != null)
            levelManager.TryReport(selected);
    }
}
