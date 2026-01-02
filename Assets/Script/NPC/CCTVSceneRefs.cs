// CCTVSceneRefs.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CCTVSceneRefs : MonoBehaviour
{
    [Header("Roots")]
    public Transform anchorsRoot;
    public Transform pathsRoot;

    [Header("Actors")]
    public List<CCTVActorBinding> actors = new List<CCTVActorBinding>();

    private readonly Dictionary<string, Transform> anchorByName = new Dictionary<string, Transform>(StringComparer.Ordinal);
    private readonly Dictionary<string, WaypointPath> pathByName = new Dictionary<string, WaypointPath>(StringComparer.Ordinal);
    private readonly Dictionary<string, CCTVActorController> actorById = new Dictionary<string, CCTVActorController>(StringComparer.Ordinal);

    private void Awake()
    {
        RebuildCache();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying) RebuildCache();
    }
#endif

    [ContextMenu("Rebuild Cache")]
    public void RebuildCache()
    {
        anchorByName.Clear();
        pathByName.Clear();
        actorById.Clear();

        if (anchorsRoot != null)
        {
            foreach (var t in anchorsRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t == anchorsRoot) continue;
                if (!anchorByName.ContainsKey(t.name))
                    anchorByName.Add(t.name, t);
            }
        }

        if (pathsRoot != null)
        {
            foreach (var p in pathsRoot.GetComponentsInChildren<WaypointPath>(true))
            {
                if (p == null) continue;
                var key = p.gameObject.name;
                if (!pathByName.ContainsKey(key))
                    pathByName.Add(key, p);
            }
        }

        foreach (var b in actors)
        {
            if (b == null || string.IsNullOrWhiteSpace(b.actorId) || b.controller == null) continue;
            if (!actorById.ContainsKey(b.actorId))
                actorById.Add(b.actorId, b.controller);
        }

        Debug.Log($"[CCTVSceneRefs] anchors={anchorByName.Count} paths={pathByName.Count} actors={actorById.Count}");
    }

    public Transform GetAnchor(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        anchorByName.TryGetValue(name, out var t);
        return t;
    }

    public WaypointPath GetPath(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        pathByName.TryGetValue(name, out var p);
        return p;
    }

    public CCTVActorController GetActor(string actorId)
    {
        if (string.IsNullOrEmpty(actorId)) return null;
        actorById.TryGetValue(actorId, out var a);
        return a;
    }
}

[Serializable]
public class CCTVActorBinding
{
    public string actorId;               // e.g. "Minh", "Lan", "Khoa", "Thao"
    public CCTVActorController controller;
}
