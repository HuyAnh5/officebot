using System.Collections;
using UnityEngine;

/// <summary>
/// ANSWER_OVERRIDE: level-local local UI glitch.
/// Driver này CHỈ pulse những UILocalGlitchPulse đã được mark Overwritten.
/// Không điều khiển TechGlitchController (để không đè DISPLAY_GLITCH persistent).
/// </summary>
public class AnswerOverrideBurstDriver : MonoBehaviour
{
    [Header("Optional root (chỉ scan trong root này nếu set)")]
    [SerializeField] private Transform targetRoot;

    [Header("Burst timing (unscaled)")]
    [SerializeField] private Vector2 onDuration = new Vector2(0.12f, 0.25f);
    [SerializeField] private Vector2 offDuration = new Vector2(1.2f, 2.8f);

    [Header("Local UI pulse")]
    [SerializeField] private float localPulseDuration = 0.18f;
    [SerializeField] private bool refreshTargetsOnStart = true;

    private Coroutine co;
    private bool running;
    private UILocalGlitchPulse[] cachedTargets;

    // Backwards compatible API
    public void StartBurst(float intensity01, Vector2 screenPos01, float audioMultiplier) => StartBurst();

    public void StartBurst()
    {
        if (refreshTargetsOnStart || cachedTargets == null || cachedTargets.Length == 0)
            RefreshTargets();

        if (running) return;
        running = true;
        co = StartCoroutine(Run());
    }

    public void StopBurst()
    {
        running = false;
        if (co != null)
        {
            StopCoroutine(co);
            co = null;
        }
    }

    public void RefreshTargets()
    {
        if (targetRoot != null)
            cachedTargets = targetRoot.GetComponentsInChildren<UILocalGlitchPulse>(true);
        else
            cachedTargets = FindObjectsByType<UILocalGlitchPulse>(FindObjectsSortMode.None);
    }

    private IEnumerator Run()
    {
        while (running)
        {
            PulseAllOverwrittenTargets();

            yield return new WaitForSecondsRealtime(Random.Range(onDuration.x, onDuration.y));
            yield return new WaitForSecondsRealtime(Random.Range(offDuration.x, offDuration.y));
        }
    }

    private void PulseAllOverwrittenTargets()
    {
        if (cachedTargets == null || cachedTargets.Length == 0) return;

        for (int i = 0; i < cachedTargets.Length; i++)
        {
            var t = cachedTargets[i];
            if (t == null || !t.isActiveAndEnabled) continue;
            if (!t.IsOverwritten) continue;

            t.PlayOneBurst(localPulseDuration);
        }
    }

}
