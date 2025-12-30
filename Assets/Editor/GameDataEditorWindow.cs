using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class GameDataEditorWindow : EditorWindow
{
    [Serializable]
    public class DayRulesFile
    {
        public List<string> globalRules = new List<string>();
        public List<DayBlock> days = new List<DayBlock>();
    }

    [Serializable]
    public class DayBlock
    {
        public int day = 1;
        public List<string> todayRules = new List<string>();
    }

    private TextAsset jsonAsset;
    private string assetPath;

    private DayRulesFile data;
    private Vector2 scroll;

    private readonly Dictionary<int, bool> dayFoldouts = new Dictionary<int, bool>();

    [MenuItem("Tools/Game Data Editor")]
    public static void Open()
    {
        GetWindow<GameDataEditorWindow>("Game Data Editor");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("JSON Source", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            var newAsset = (TextAsset)EditorGUILayout.ObjectField("Rules JSON", jsonAsset, typeof(TextAsset), false);
            if (newAsset != jsonAsset)
            {
                jsonAsset = newAsset;
                assetPath = jsonAsset != null ? AssetDatabase.GetAssetPath(jsonAsset) : null;
                data = null; // force reload
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Path", assetPath ?? "(none)");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = jsonAsset != null;

            if (GUILayout.Button("Load", GUILayout.Height(28)))
                LoadFromJsonAsset();

            if (GUILayout.Button("Save", GUILayout.Height(28)))
                SaveToJsonAsset();

            GUI.enabled = true;
        }

        EditorGUILayout.Space(8);

        if (jsonAsset == null)
        {
            EditorGUILayout.HelpBox("Assign a JSON TextAsset (e.g. Day1_rule.json), then press Load.", MessageType.Info);
            return;
        }

        if (data == null)
        {
            EditorGUILayout.HelpBox("Press Load to parse JSON into editable fields.", MessageType.Info);
            return;
        }

        DrawValidationWarnings();

        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawGlobalRules();
        EditorGUILayout.Space(10);
        DrawDays();

        EditorGUILayout.EndScrollView();
    }

    private void LoadFromJsonAsset()
    {
        assetPath = AssetDatabase.GetAssetPath(jsonAsset);
        if (string.IsNullOrEmpty(assetPath))
        {
            Debug.LogError("[GameDataEditorWindow] Could not resolve asset path.");
            return;
        }

        try
        {
            var json = File.ReadAllText(assetPath);
            data = JsonUtility.FromJson<DayRulesFile>(json);
            if (data == null) data = new DayRulesFile();

            if (data.globalRules == null) data.globalRules = new List<string>();
            if (data.days == null) data.days = new List<DayBlock>();
            for (int i = 0; i < data.days.Count; i++)
            {
                if (data.days[i] == null) data.days[i] = new DayBlock();
                if (data.days[i].todayRules == null) data.days[i].todayRules = new List<string>();
            }

            Repaint();
            ShowNotification(new GUIContent("Loaded."));
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameDataEditorWindow] Load failed: {e}");
        }
    }

    private void SaveToJsonAsset()
    {
        assetPath = AssetDatabase.GetAssetPath(jsonAsset);
        if (string.IsNullOrEmpty(assetPath))
        {
            Debug.LogError("[GameDataEditorWindow] Could not resolve asset path.");
            return;
        }

        try
        {
            // Ensure lists are non-null
            if (data == null) data = new DayRulesFile();
            if (data.globalRules == null) data.globalRules = new List<string>();
            if (data.days == null) data.days = new List<DayBlock>();
            for (int i = 0; i < data.days.Count; i++)
            {
                if (data.days[i] == null) data.days[i] = new DayBlock();
                if (data.days[i].todayRules == null) data.days[i].todayRules = new List<string>();
            }

            var json = JsonUtility.ToJson(data, true);
            File.WriteAllText(assetPath, json);

            AssetDatabase.Refresh();
            EditorUtility.SetDirty(jsonAsset);

            Repaint();
            ShowNotification(new GUIContent("Saved."));
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameDataEditorWindow] Save failed: {e}");
        }
    }

    private void DrawGlobalRules()
    {
        EditorGUILayout.LabelField("Global rules", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            if (data.globalRules.Count == 0)
                EditorGUILayout.HelpBox("No global rules.", MessageType.None);

            for (int i = 0; i < data.globalRules.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    data.globalRules[i] = EditorGUILayout.TextField($"{i + 1})", data.globalRules[i]);

                    if (GUILayout.Button("X", GUILayout.Width(24)))
                    {
                        data.globalRules.RemoveAt(i);
                        GUI.FocusControl(null);
                        break;
                    }
                }
            }

            if (GUILayout.Button("+ Add global rule"))
                data.globalRules.Add("");
        }
    }

    private void DrawDays()
    {
        EditorGUILayout.LabelField("Days", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Add day"))
                {
                    data.days.Add(new DayBlock { day = NextDayNumber() });
                    GUI.FocusControl(null);
                }

                if (GUILayout.Button("Sort by day"))
                {
                    data.days.Sort((a, b) => a.day.CompareTo(b.day));
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.Space(6);

            for (int i = 0; i < data.days.Count; i++)
            {
                var d = data.days[i];
                if (d == null) { data.days[i] = new DayBlock(); d = data.days[i]; }
                if (d.todayRules == null) d.todayRules = new List<string>();

                bool open = GetFoldout(d.day, defaultValue: true);

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        open = EditorGUILayout.Foldout(open, $"Day {d.day}", true);
                        SetFoldout(d.day, open);

                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("▲", GUILayout.Width(26)) && i > 0)
                        {
                            (data.days[i - 1], data.days[i]) = (data.days[i], data.days[i - 1]);
                            GUI.FocusControl(null);
                            break;
                        }

                        if (GUILayout.Button("▼", GUILayout.Width(26)) && i < data.days.Count - 1)
                        {
                            (data.days[i + 1], data.days[i]) = (data.days[i], data.days[i + 1]);
                            GUI.FocusControl(null);
                            break;
                        }

                        if (GUILayout.Button("X", GUILayout.Width(26)))
                        {
                            data.days.RemoveAt(i);
                            GUI.FocusControl(null);
                            break;
                        }
                    }

                    if (!open) continue;

                    d.day = EditorGUILayout.IntField("Day number", d.day);

                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Today Rule", EditorStyles.boldLabel);

                    for (int r = 0; r < d.todayRules.Count; r++)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            d.todayRules[r] = EditorGUILayout.TextField($"{r + 1})", d.todayRules[r]);

                            if (GUILayout.Button("X", GUILayout.Width(24)))
                            {
                                d.todayRules.RemoveAt(r);
                                GUI.FocusControl(null);
                                break;
                            }
                        }
                    }

                    if (GUILayout.Button("+ Add today rule"))
                        d.todayRules.Add("");
                }
            }
        }
    }

    private void DrawValidationWarnings()
    {
        // Duplicate day numbers warning
        var seen = new HashSet<int>();
        for (int i = 0; i < data.days.Count; i++)
        {
            var d = data.days[i];
            if (d == null) continue;
            if (!seen.Add(d.day))
            {
                EditorGUILayout.HelpBox("Duplicate day numbers detected. Each day should be unique.", MessageType.Warning);
                break;
            }
        }
    }

    private int NextDayNumber()
    {
        int max = 0;
        for (int i = 0; i < data.days.Count; i++)
        {
            if (data.days[i] != null)
                max = Mathf.Max(max, data.days[i].day);
        }
        return max + 1;
    }

    private bool GetFoldout(int day, bool defaultValue)
    {
        if (dayFoldouts.TryGetValue(day, out var v)) return v;
        dayFoldouts[day] = defaultValue;
        return defaultValue;
    }

    private void SetFoldout(int day, bool value)
    {
        dayFoldouts[day] = value;
    }
}
