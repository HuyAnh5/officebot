using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AnomalyReportUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform checkboxList;      // CheckboxList
    [SerializeField] private CheckboxUI checkboxPrefab;   // prefab CheckboxUI
    [SerializeField] private Button reportButton;         // ReportButton

    [Header("Layout (spawned rows)")]
    [SerializeField] private float checkboxPreferredHeight = 44f;
    [SerializeField] private float checkboxPreferredWidth = 0f;   // 0 = không ép width

    [Header("Label Style (override in ReportUI only)")]
    [SerializeField] private float labelFontSize = 18f;
    [SerializeField] private bool disableAutoSize = true;

    private readonly List<CheckboxUI> spawned = new();

    private readonly (string id, string label)[] items =
    {
        ("FORM_ERROR",      "FORM ERROR (PAPER)"),
        ("DISPLAY_GLITCH",  "DISPLAY GLITCH (VISUAL / PERCEPTION)"),
        ("ANSWER_OVERRIDE", "ANSWER OVERRIDE (UI / GAMEPLAY)"),
        ("TERMINAL_ANOMALY","TERMINAL ANOMALY (CHAT / COMMAND)")
    };

    private void Awake()
    {
        Build();

        if (reportButton != null)
            reportButton.onClick.AddListener(OnReportClicked);
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

            // Override font size (chỉ trong report panel)
            ApplyLabelStyle(inst.transform);

            inst.OnChanged += OnCheckboxChanged;
            spawned.Add(inst);
        }
    }

    private void ApplyLabelStyle(Transform root)
    {
        // Ưu tiên TMP label có tên chứa "label" hoặc "text"
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
        {
            if (spawned[i] != who)
                spawned[i].SetOn(false, notify: false);
        }
    }

    public string GetSelectedId()
    {
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i].IsOn) return spawned[i].Id;
        return "";
    }

    private void OnReportClicked()
    {
        string selected = GetSelectedId();
        Debug.Log($"[ANOMALY REPORT] selected={selected}");

        // TODO: gọi sang LevelManager của bạn
        // levelManager.ReportAnomaly(selected);
    }
}
