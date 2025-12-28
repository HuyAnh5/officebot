using System.Collections;
using UnityEngine;

public class PersistentDisplayGlitchDriver : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TechGlitchController techGlitch;

    [Header("Persistent Id")]
    [SerializeField] private string anomalyId = "DISPLAY_GLITCH";

    [Header("Position (used for pan/marker)")]
    [SerializeField] private Vector2 defaultPos01 = new Vector2(0.5f, 0.5f);

    [Header("Severity (0-1)")]
    [SerializeField, Range(0f, 1f)] private float startSeverity = 0.12f;
    [SerializeField, Range(0f, 1f)] private float maxSeverity = 0.95f;
    [SerializeField] private float severityIncreasePerMinute = 0.06f;

    [Header("Pulse Timing (unscaled)")]
    [SerializeField] private Vector2 onDuration = new Vector2(0.12f, 0.28f);
    [SerializeField] private Vector2 offDuration = new Vector2(1.2f, 2.8f);

    [Header("Audio")]
    [SerializeField] private float audioMultiplier = 1f;

    private Coroutine co;
    private bool active;
    private float severity;
    private Vector2 pos01;

    private void OnEnable()
    {
        // không auto-start ở đây nữa; Update sẽ tự sync store->driver
        active = false;
        severity = startSeverity;
        pos01 = defaultPos01;
    }

    private void Update()
    {
        bool shouldBeActive = PersistentAnomalyStore.IsActive(anomalyId);

        // store vừa bật -> start
        if (!active && shouldBeActive)
        {
            active = true;
            pos01 = PersistentAnomalyStore.GetPos01(anomalyId, defaultPos01);
            severity = PersistentAnomalyStore.GetSeverity01(anomalyId, startSeverity);
            severity = Mathf.Clamp(severity, startSeverity, maxSeverity);
            StartLoop();
        }

        // store vừa tắt -> stop (report đúng / clear)
        if (active && !shouldBeActive)
        {
            StopLoop(clearCue: true);
            return;
        }

        if (!active) return;

        // optional: pos có thể update theo store (nếu spawner thay đổi pos)
        pos01 = PersistentAnomalyStore.GetPos01(anomalyId, pos01);

        // escalate severity theo thời gian (unscaled)
        float add = (severityIncreasePerMinute / 60f) * Time.unscaledDeltaTime;
        severity = Mathf.Clamp(severity + add, startSeverity, maxSeverity);
        PersistentAnomalyStore.SetSeverity01(anomalyId, severity);
    }

    public void ForceStart(Vector2? spawnPos01 = null, float? startSeverity01 = null)
    {
        Vector2 p = spawnPos01 ?? defaultPos01;
        float s = startSeverity01 ?? startSeverity;

        // tránh lấy severity cũ
        PersistentAnomalyStore.Clear(anomalyId);
        PersistentAnomalyStore.SetPos01(anomalyId, p);
        PersistentAnomalyStore.SetSeverity01(anomalyId, s);
        PersistentAnomalyStore.SetActive(anomalyId, true);
        // driver sẽ auto-start trong Update()
    }

    public void Resolve()
    {
        PersistentAnomalyStore.Clear(anomalyId);
        StopLoop(clearCue: true);
    }

    private void StartLoop()
    {
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(PulseRoutine());
    }

    private void StopLoop(bool clearCue)
    {
        active = false;

        if (co != null)
        {
            StopCoroutine(co);
            co = null;
        }

        if (techGlitch != null && clearCue)
            techGlitch.ClearCue();
    }

    private IEnumerator PulseRoutine()
    {
        while (active)
        {
            // ON
            if (techGlitch != null)
                techGlitch.ApplyCue(severity, pos01, audioMultiplier);

            yield return new WaitForSecondsRealtime(Random.Range(onDuration.x, onDuration.y));

            // OFF
            if (techGlitch != null)
                techGlitch.ClearCue();

            yield return new WaitForSecondsRealtime(Random.Range(offDuration.x, offDuration.y));
        }
    }
}
