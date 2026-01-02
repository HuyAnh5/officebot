// CCTVActorController.cs
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class CCTVActorController : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 2.0f;
    public float arriveDistance = 0.01f;

    [Header("Debug")]
    public bool debugLogs = true;

    private Coroutine current;

    private void Log(string msg)
    {
        if (debugLogs) Debug.Log($"[CCTVActor:{name}] {msg}", this);
    }

    public void Cancel()
    {
        if (current != null)
        {
            StopCoroutine(current);
            current = null;
            Log("Cancel()");
        }
    }

    public void Run(IEnumerator routine)
    {
        Cancel();
        current = StartCoroutine(routine);
    }

    public IEnumerator CoSpawnAt(Transform anchor)
    {
        if (anchor != null)
        {
            transform.position = anchor.position;
            Log($"SpawnAt '{anchor.name}' pos={anchor.position}");
        }
        else
        {
            Log("SpawnAt <null> (no move)");
        }

        gameObject.SetActive(true);
        yield break;
    }

    public IEnumerator CoMoveTo(Transform target)
    {
        if (target == null)
        {
            Log("MoveTo <null> (skip)");
            yield break;
        }

        Log($"MoveTo '{target.name}' pos={target.position}");
        yield return CoMoveToPosition(target.position);
    }

    public IEnumerator CoFollowPath(WaypointPath path)
    {
        if (path == null)
        {
            Log("FollowPath <null> (skip)");
            yield break;
        }

        var pts = path.Points;
        if (pts == null || pts.Count == 0)
        {
            Log($"FollowPath '{path.gameObject.name}' has 0 points (skip)");
            yield break;
        }

        Log($"FollowPath '{path.gameObject.name}' points={pts.Count}");

        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            if (p == null) continue;

            Log($" -> WP[{i}] '{p.name}' pos={p.position}");
            yield return CoMoveToPosition(p.position);
        }
    }

    public IEnumerator CoDespawnAt(Transform exit)
    {
        if (exit != null)
        {
            Log($"DespawnAt '{exit.name}' pos={exit.position}");
            yield return CoMoveTo(exit);
        }
        else
        {
            Log("DespawnAt <null> (no move, just hide)");
        }

        gameObject.SetActive(false);
        Log("SetActive(false)");
    }

    private IEnumerator CoMoveToPosition(Vector3 targetPos)
    {
        int safety = 0;

        while (true)
        {
            var pos = transform.position;
            float d = Vector3.Distance(pos, targetPos);

            if (d <= arriveDistance)
            {
                transform.position = targetPos;
                Log($"Arrived pos={targetPos}");
                break;
            }

            transform.position = Vector3.MoveTowards(pos, targetPos, moveSpeed * Time.deltaTime);

            // safety to avoid infinite loop if arriveDistance too small / NaN
            safety++;
            if (safety > 200000)
            {
                Debug.LogWarning($"[CCTVActor:{name}] MoveToPosition safety break. target={targetPos} pos={transform.position}", this);
                break;
            }

            yield return null;
        }
    }
}
