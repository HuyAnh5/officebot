using UnityEngine;

/// <summary>
/// Observation Duty-style: th?nh tho?ng spawn DISPLAY_GLITCH global persistent.
/// - SetActive("DISPLAY_GLITCH", true) ?? PersistentDisplayGlitchDriver t? ch?y.
/// - Không ph? thu?c LevelManager.
/// </summary>
public class DisplayGlitchSpawner : MonoBehaviour
{
    [Header("Anomaly id")]
    [SerializeField] private string anomalyId = "DISPLAY_GLITCH";

    [Header("Spawn")]
    [SerializeField] private bool enableSpawn = true;
    [SerializeField] private float startDelay = 10f;
    [SerializeField] private float checkInterval = 15f;
    [SerializeField, Range(0f, 1f)] private float baseChancePerCheck = 0.05f;
    [SerializeField] private float chanceRampPerMinute = 0.02f;

    [Header("Initial values")]
    [SerializeField] private Vector2 basePos01 = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 posJitter01 = new Vector2(0.25f, 0.18f);
    [SerializeField, Range(0f, 1f)] private float startSeverity01 = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool debugForceSpawnOnStart = false;
    [SerializeField] private KeyCode debugForceKey = KeyCode.F6;

    private float elapsed;
    private float nextCheck;

    private void Start()
    {
        elapsed = 0f;
        nextCheck = checkInterval;

        if (debugForceSpawnOnStart)
            ForceSpawnNow();
    }

    private void Update()
    {
        if (!enableSpawn) return;

        elapsed += Time.unscaledDeltaTime;
        nextCheck -= Time.unscaledDeltaTime;

        if (Input.GetKeyDown(debugForceKey))
            ForceSpawnNow();

        if (elapsed < startDelay) return;
        if (PersistentAnomalyStore.IsActive(anomalyId)) return;

        if (nextCheck > 0f) return;
        nextCheck = Mathf.Max(0.25f, checkInterval);

        float chance = baseChancePerCheck + (elapsed / 60f) * chanceRampPerMinute;
        chance = Mathf.Clamp01(chance);

        if (Random.value <= chance)
            ForceSpawnNow();
    }

    [ContextMenu("Force Spawn Now")]
    public void ForceSpawnNow()
    {
        // quan tr?ng: tránh l?y severity c? t? PlayerPrefs => Clear r?i set l?i
        PersistentAnomalyStore.Clear(anomalyId);

        Vector2 pos = basePos01;
        pos.x += Random.Range(-posJitter01.x, posJitter01.x);
        pos.y += Random.Range(-posJitter01.y, posJitter01.y);
        pos.x = Mathf.Clamp01(pos.x);
        pos.y = Mathf.Clamp01(pos.y);

        PersistentAnomalyStore.SetPos01(anomalyId, pos);
        PersistentAnomalyStore.SetSeverity01(anomalyId, startSeverity01);
        PersistentAnomalyStore.SetActive(anomalyId, true);
    }
}
