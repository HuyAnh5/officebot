// WaypointPath.cs
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

[DisallowMultipleComponent]
public class WaypointPath : MonoBehaviour
{
    [Header("Build")]
    [SerializeField] private bool autoRefresh = true;
    [SerializeField] private bool useChildOrderIfNoIndex = true;

    [NonSerialized] private List<Transform> points = new List<Transform>();

    public IReadOnlyList<Transform> Points
    {
        get
        {
            if ((points == null || points.Count == 0) && autoRefresh) Refresh();
            return points;
        }
    }

    private void Awake()
    {
        if (autoRefresh) Refresh();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && autoRefresh) Refresh();
    }
#endif

    [ContextMenu("Refresh Waypoints")]
    public void Refresh()
    {
        points ??= new List<Transform>();
        points.Clear();

        for (int i = 0; i < transform.childCount; i++)
        {
            var t = transform.GetChild(i);
            if (t != null) points.Add(t);
        }

        // Sort by numeric index in name (WP1_*, 01_*, etc). Fallback to sibling index.
        points.Sort((a, b) =>
        {
            int ia = ExtractIndex(a.name);
            int ib = ExtractIndex(b.name);

            if (ia >= 0 && ib >= 0) return ia.CompareTo(ib);
            if (ia >= 0) return -1;
            if (ib >= 0) return 1;

            if (useChildOrderIfNoIndex)
                return a.GetSiblingIndex().CompareTo(b.GetSiblingIndex());

            return string.CompareOrdinal(a.name, b.name);
        });
    }

    private static int ExtractIndex(string s)
    {
        // Finds first number group in the string.
        var m = Regex.Match(s, @"\d+");
        if (!m.Success) return -1;
        if (int.TryParse(m.Value, out int v)) return v;
        return -1;
    }
}
