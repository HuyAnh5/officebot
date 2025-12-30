using System;
using TMPro;
using UnityEngine;

public class DayRulesPanel : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private TextAsset rulesJson;

    [Header("UI")]
    [SerializeField] private TMP_Text rulesText;

    [Header("Titles")]
    [SerializeField] private string globalTitle = "Global rules";
    [SerializeField] private string todayTitle = "Today Rule";

    [Header("Debug")]
    [SerializeField] private bool setOnStart = true;
    [SerializeField, Min(1)] private int debugDay = 1;

    private DayRulesFile data;

    [Serializable]
    private class DayRulesFile
    {
        public string[] globalRules;
        public DayBlock[] days;
    }

    [Serializable]
    private class DayBlock
    {
        public int day;
        public string[] todayRules;
    }

    private void Awake()
    {
        Load();
    }

    private void OnEnable()
    {
        if (setOnStart)
            SetDay(debugDay);
    }

    private void Start()
    {
        if (setOnStart)
            SetDay(debugDay);
    }


    public void Load()
    {
        if (rulesJson == null)
        {
            Debug.LogError("[DayRulesPanel] rulesJson is null.");
            return;
        }

        Debug.Log($"[DayRulesPanel] rulesJson length = {rulesJson.text?.Length ?? 0}");
        data = JsonUtility.FromJson<DayRulesFile>(rulesJson.text);

        if (data == null)
            Debug.LogError("[DayRulesPanel] Failed to parse rules JSON.");
    }


    public void SetDay(int dayNumber)
    {
        if (rulesText == null) return;

        if (data == null)
            Load();

        if (data == null)
        {
            rulesText.text = "";
            return;
        }

        var sb = new System.Text.StringBuilder();

        // Global
        sb.AppendLine($"<b>{globalTitle}</b>");
        AppendNumbered(sb, data.globalRules);
        sb.AppendLine();

        // Today
        sb.AppendLine($"<b>{todayTitle}</b>");
        var dayBlock = FindDay(dayNumber);
        AppendNumbered(sb, dayBlock != null ? dayBlock.todayRules : Array.Empty<string>());

        rulesText.text = sb.ToString().TrimEnd();
    }

    private DayBlock FindDay(int dayNumber)
    {
        if (data.days == null) return null;
        for (int i = 0; i < data.days.Length; i++)
        {
            if (data.days[i] != null && data.days[i].day == dayNumber)
                return data.days[i];
        }
        return null;
    }

    private static void AppendNumbered(System.Text.StringBuilder sb, string[] lines)
    {
        if (lines == null || lines.Length == 0)
        {
            sb.AppendLine("1) (none)");
            return;
        }

        int n = 1;
        for (int i = 0; i < lines.Length; i++)
        {
            var s = lines[i];
            if (string.IsNullOrWhiteSpace(s)) continue;
            sb.AppendLine($"{n}) {s.Trim()}");
            n++;
        }

        if (n == 1)
            sb.AppendLine("1) (none)");
    }
}
