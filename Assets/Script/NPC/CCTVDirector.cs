// CCTVDirector.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CCTVDirector : MonoBehaviour
{
    [Header("Refs")]
    public CCTVSceneRefs refs;

    [Header("Plans (set in Inspector)")]
    public List<CCTVLevelPlan> levelPlans = new List<CCTVLevelPlan>();

    [Header("Test")]
    public bool autoPlayFirstPlanOnStart = false;

    [Header("Debug")]
    public bool debugLogs = true;

    private void Log(string msg)
    {
        if (debugLogs) Debug.Log($"[CCTVDirector] {msg}", this);
    }

    private void Start()
    {
        Log($"Start() autoPlay={autoPlayFirstPlanOnStart} plans={(levelPlans != null ? levelPlans.Count : 0)}");
        if (autoPlayFirstPlanOnStart && levelPlans != null && levelPlans.Count > 0)
            PlayLevel(levelPlans[0].levelId);
    }

    public void PlayLevel(string levelId)
    {
        if (refs == null)
        {
            Debug.LogWarning("[CCTVDirector] Missing refs.", this);
            return;
        }

        refs.RebuildCache();

        StopAllCoroutines();

        Log($"PlayLevel('{levelId}')");

        var plan = FindPlan(levelId);
        if (plan == null)
        {
            Debug.LogWarning($"[CCTVDirector] No plan for levelId='{levelId}'.", this);
            return;
        }

        Log($"Plan found: actorPlans={(plan.actorPlans != null ? plan.actorPlans.Count : 0)}");
        StartCoroutine(CoRunPlan(plan));
    }

    public void EndLevel(bool sendAllToExit = true)
    {
        Log($"EndLevel(sendAllToExit={sendAllToExit})");
        StopAllCoroutines();

        if (!sendAllToExit || refs == null) return;

        var exitL = refs.GetAnchor("Exit_Left");
        var exitR = refs.GetAnchor("Exit_Right");

        foreach (var b in refs.actors)
        {
            if (b == null || b.controller == null) continue;

            b.controller.Cancel();

            Transform exit = ChooseExit(exitL, exitR, b.controller.transform.position);
            Log($"Force exit actorId='{b.actorId}' -> {(exit ? exit.name : "null")}");
            b.controller.Run(b.controller.CoDespawnAt(exit));
        }
    }

    private IEnumerator CoRunPlan(CCTVLevelPlan plan)
    {
        var running = new List<Coroutine>();

        if (plan.actorPlans == null || plan.actorPlans.Count == 0)
        {
            Log("Plan has no actorPlans.");
            yield break;
        }

        foreach (var ap in plan.actorPlans)
        {
            if (ap == null || string.IsNullOrWhiteSpace(ap.actorId))
            {
                Log("Skipped null/empty actorPlan.");
                continue;
            }

            var actor = refs.GetActor(ap.actorId);
            if (actor == null)
            {
                Debug.LogWarning($"[CCTVDirector] Actor '{ap.actorId}' not found in refs. (Check CCTVSceneRefs actorId)", this);
                continue;
            }

            Log($"Start actorPlan: actorId='{ap.actorId}' commands={(ap.commands != null ? ap.commands.Count : 0)}");
            var co = StartCoroutine(CoRunActorPlan(actor, ap));
            running.Add(co);
        }

        for (int i = 0; i < running.Count; i++)
            yield return running[i];

        Log("Plan complete.");
    }

    private IEnumerator CoRunActorPlan(CCTVActorController actor, CCTVActorPlan ap)
    {
        if (ap.commands == null || ap.commands.Count == 0)
        {
            Log($"Actor '{ap.actorId}' has no commands.");
            yield break;
        }

        for (int i = 0; i < ap.commands.Count; i++)
        {
            var cmd = ap.commands[i];
            if (cmd == null) continue;

            Log($"Actor '{ap.actorId}' cmd[{i}]={cmd.type} target='{cmd.targetName}' wait={cmd.waitSeconds}");

            switch (cmd.type)
            {
                case CCTVCommandType.SpawnAtAnchor:
                    {
                        var a = refs.GetAnchor(cmd.targetName);
                        if (a == null) Debug.LogWarning($"[CCTVDirector] Anchor '{cmd.targetName}' not found.", this);
                        yield return actor.CoSpawnAt(a);
                        break;
                    }
                case CCTVCommandType.FollowPath:
                    {
                        var p = refs.GetPath(cmd.targetName);
                        if (p == null) Debug.LogWarning($"[CCTVDirector] Path '{cmd.targetName}' not found. (Name must match lane GameObject)", this);
                        yield return actor.CoFollowPath(p);
                        break;
                    }
                case CCTVCommandType.MoveToAnchor:
                    {
                        var a = refs.GetAnchor(cmd.targetName);
                        if (a == null) Debug.LogWarning($"[CCTVDirector] Anchor '{cmd.targetName}' not found.", this);
                        yield return actor.CoMoveTo(a);
                        break;
                    }
                case CCTVCommandType.Wait:
                    {
                        float t = Mathf.Max(0f, cmd.waitSeconds);
                        if (t > 0f) yield return new WaitForSeconds(t);
                        break;
                    }
                case CCTVCommandType.DespawnAtExit:
                    {
                        Transform exit = null;

                        if (!string.IsNullOrWhiteSpace(cmd.targetName))
                            exit = refs.GetAnchor(cmd.targetName);

                        if (exit == null)
                        {
                            var exitL = refs.GetAnchor("Exit_Left");
                            var exitR = refs.GetAnchor("Exit_Right");
                            exit = ChooseExit(exitL, exitR, actor.transform.position);
                        }

                        yield return actor.CoDespawnAt(exit);
                        break;
                    }
            }
        }

        Log($"Actor '{ap.actorId}' plan done.");
    }

    private CCTVLevelPlan FindPlan(string levelId)
    {
        if (levelPlans == null) return null;

        for (int i = 0; i < levelPlans.Count; i++)
        {
            var p = levelPlans[i];
            if (p != null && string.Equals(p.levelId, levelId, StringComparison.Ordinal))
                return p;
        }

        return null;
    }

    private static Transform ChooseExit(Transform exitL, Transform exitR, Vector3 actorPos)
    {
        if (exitL == null) return exitR;
        if (exitR == null) return exitL;

        return (Mathf.Abs(actorPos.x - exitL.position.x) <= Mathf.Abs(actorPos.x - exitR.position.x)) ? exitL : exitR;
    }
}

[Serializable]
public class CCTVLevelPlan
{
    public string levelId;                 // match your level/question id
    public List<CCTVActorPlan> actorPlans = new List<CCTVActorPlan>();
}

[Serializable]
public class CCTVActorPlan
{
    public string actorId;                 // must match CCTVSceneRefs actorId (e.g. "1" or "Minh")
    public List<CCTVCommand> commands = new List<CCTVCommand>();
}

public enum CCTVCommandType
{
    SpawnAtAnchor,
    FollowPath,
    MoveToAnchor,
    Wait,
    DespawnAtExit
}

[Serializable]
public class CCTVCommand
{
    public CCTVCommandType type;
    public string targetName;              // anchor name or lane object name
    public float waitSeconds;              // used when type == Wait
}
