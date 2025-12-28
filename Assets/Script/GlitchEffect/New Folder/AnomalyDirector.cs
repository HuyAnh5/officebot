using System;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

using URandom = UnityEngine.Random;

public class AnomalyDirector : MonoBehaviour
{
    private const string ID_DISPLAY = "DISPLAY_GLITCH";
    private const string ID_ANSWER = "ANSWER_OVERRIDE";

    // L?u variant c?a DISPLAY_GLITCH ?? persist cho t?i khi report clear
    private const string PREF_DISPLAY_VARIANT = "PERSIST_VARIANT_DISPLAY_GLITCH";

    public enum DisplayVariant
    {
        PulseGap = 0,       // có kho?ng tr?ng
        ContinuousSoft = 1, // nh?, g?n nh? liên t?c
        SurgeBurst = 2      // bùng phát m?nh
    }

    [Serializable]
    public class DisplayVariantProfile
    {
        public DisplayVariant variant;

        [Header("Intensity / Audio multipliers")]
        [Range(0f, 3f)] public float intensityMul = 1f;
        [Range(0f, 3f)] public float audioMul = 1f;

        [Header("Severity")]
        [Range(0f, 1f)] public float startSeverity01 = 0.15f;
        [Range(0f, 1f)] public float maxSeverity01 = 1.0f;
        [Tooltip("severity t?ng theo phút khi ?ang active")]
        [Min(0f)] public float severityRampPerMinute = 0.45f;

        [Header("Pulse (sec) - dùng cho PulseGap / SurgeBurst")]
        public Vector2 pulseOnSeconds = new Vector2(0.10f, 0.22f);
        public Vector2 pulseOffSeconds = new Vector2(0.55f, 1.10f);

        [Header("Continuous (sec) - dùng cho ContinuousSoft")]
        [Tooltip("ContinuousSoft v?n có th? “nh?p nh?”, set off r?t nh?")]
        public bool continuousMode = false;

        [Header("SFX")]
        public bool playBurstOnPulseStart = false;
    }

    [Header("Refs")]
    [SerializeField] private TechGlitchController techGlitch;
    [SerializeField] private TechGlitchConfig techGlitchConfig;

    [Header("DISPLAY_GLITCH Variants (3 profiles)")]
    [SerializeField] private DisplayVariantProfile[] displayVariants = new DisplayVariantProfile[3];

    [Header("Spawn (Observation Duty style)")]
    [SerializeField] private bool enableRandomSpawn = true;
    [SerializeField] private float spawnStartDelay = 6f;
    [SerializeField] private float spawnCheckInterval = 12f;
    [Range(0f, 1f)] public float baseChancePerCheck = 0.06f;
    [Min(0f)] public float chanceRampPerMinute = 0.00f;
    [Tooltip("Random spawn ch?n variant ng?u nhiên (n?u false s? dùng PulseGap).")]
    [SerializeField] private bool randomPickVariant = true;

    [Header("Spawn Position (pos01)")]
    [SerializeField] private Vector2 randomPosXRange = new Vector2(0.15f, 0.85f);
    [SerializeField] private Vector2 randomPosYRange = new Vector2(0.65f, 0.95f);

    [Header("ANSWER_OVERRIDE (Local UI)")]
    [SerializeField] private bool enableAnswerOverrideDebugKey = true;
    [SerializeField] private Vector2 uiBurstIntervalSeconds = new Vector2(0.35f, 0.80f);
    [SerializeField, Min(0.02f)] private float uiBurstDuration = 0.18f;
    [SerializeField, Min(0.05f)] private float uiTargetRefreshInterval = 0.5f;

    [Header("Debug Hotkeys")]
    [SerializeField] private bool enableDebugHotkeys = true; // 1-2-3 display variants, 4 answer override pulse

    // runtime
    private float elapsed;
    private float nextSpawnCheck;

    private bool displayPhaseOn = true;
    private float displayPhaseTimer = 0f;
    private bool displayJustTurnedOn = false;

    private float nextUiBurst = 0f;
    private float nextUiRefresh = 0f;

    private readonly List<UILocalGlitchPulse> overwrittenTargets = new List<UILocalGlitchPulse>();

    private void Awake()
    {
        elapsed = 0f;
        nextSpawnCheck = spawnCheckInterval;

        // ??m b?o không “vào là max”
        if (techGlitch != null)
            techGlitch.ClearCue();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        elapsed += dt;

        if (enableDebugHotkeys)
            HandleDebugHotkeys();

        if (enableRandomSpawn)
            TickRandomSpawn(dt);

        TickDisplayGlitch(dt);
        TickAnswerOverride(dt);
    }

    // =========================
    // DEBUG HOTKEYS (Input System)
    // =========================
    private void HandleDebugHotkeys()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;

        if (WasDigitPressed(kb, 1)) ForceDisplayVariant(DisplayVariant.PulseGap);
        if (WasDigitPressed(kb, 2)) ForceDisplayVariant(DisplayVariant.ContinuousSoft);
        if (WasDigitPressed(kb, 3)) ForceDisplayVariant(DisplayVariant.SurgeBurst);

        if (enableAnswerOverrideDebugKey && WasDigitPressed(kb, 4))
        {
            // Debug only: pulse 1 l?n các targets ?ang overwritten (không overwrite l?i text)
            RefreshOverwrittenTargets();
            PulseOverwrittenOnce();
        }
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static bool WasDigitPressed(Keyboard kb, int digit)
    {
        Key dk = digit switch
        {
            1 => Key.Digit1,
            2 => Key.Digit2,
            3 => Key.Digit3,
            4 => Key.Digit4,
            5 => Key.Digit5,
            6 => Key.Digit6,
            7 => Key.Digit7,
            8 => Key.Digit8,
            9 => Key.Digit9,
            _ => Key.Digit1
        };

        Key nk = digit switch
        {
            1 => Key.Numpad1,
            2 => Key.Numpad2,
            3 => Key.Numpad3,
            4 => Key.Numpad4,
            5 => Key.Numpad5,
            6 => Key.Numpad6,
            7 => Key.Numpad7,
            8 => Key.Numpad8,
            9 => Key.Numpad9,
            _ => Key.Numpad1
        };

        return kb[dk].wasPressedThisFrame || kb[nk].wasPressedThisFrame;
    }
#endif

    private void ForceDisplayVariant(DisplayVariant v)
    {
        int vi = (int)v;
        PlayerPrefs.SetInt(PREF_DISPLAY_VARIANT, vi);
        PlayerPrefs.Save();

        // toggle: n?u ?ang active và ?úng variant -> clear, ng??c l?i -> force spawn
        if (PersistentAnomalyStore.IsActive(ID_DISPLAY))
        {
            PersistentAnomalyStore.Clear(ID_DISPLAY);
            techGlitch?.ClearCue();
            return;
        }

        var profile = GetVariantProfile(v);
        if (profile == null) profile = GetVariantProfile(DisplayVariant.PulseGap);

        PersistentAnomalyStore.SetActive(ID_DISPLAY, true);
        PersistentAnomalyStore.SetSeverity01(ID_DISPLAY, profile.startSeverity01);

        Vector2 pos = RandomPos01();
        PersistentAnomalyStore.SetPos01(ID_DISPLAY, pos);

        // reset phase
        displayPhaseOn = true;
        displayPhaseTimer = URandom.Range(profile.pulseOnSeconds.x, profile.pulseOnSeconds.y);
        displayJustTurnedOn = true;
    }

    // =========================
    // RANDOM SPAWN (DISPLAY_GLITCH only)
    // =========================
    private void TickRandomSpawn(float dt)
    {
        if (elapsed < spawnStartDelay) return;
        if (PersistentAnomalyStore.IsActive(ID_DISPLAY)) return;

        nextSpawnCheck -= dt;
        if (nextSpawnCheck > 0f) return;
        nextSpawnCheck = Mathf.Max(0.1f, spawnCheckInterval);

        float minutes = elapsed / 60f;
        float chance = Mathf.Clamp01(baseChancePerCheck + chanceRampPerMinute * minutes);

        if (URandom.value < chance)
        {
            var v = randomPickVariant ? (DisplayVariant)URandom.Range(0, 3) : DisplayVariant.PulseGap;
            PlayerPrefs.SetInt(PREF_DISPLAY_VARIANT, (int)v);
            PlayerPrefs.Save();

            var profile = GetVariantProfile(v);
            if (profile == null) profile = GetVariantProfile(DisplayVariant.PulseGap);

            PersistentAnomalyStore.SetActive(ID_DISPLAY, true);
            PersistentAnomalyStore.SetSeverity01(ID_DISPLAY, profile.startSeverity01);
            PersistentAnomalyStore.SetPos01(ID_DISPLAY, RandomPos01());

            displayPhaseOn = true;
            displayPhaseTimer = URandom.Range(profile.pulseOnSeconds.x, profile.pulseOnSeconds.y);
            displayJustTurnedOn = true;
        }
    }

    // =========================
    // DISPLAY_GLITCH runtime
    // =========================
    private void TickDisplayGlitch(float dt)
    {
        if (techGlitch == null) return;

        if (!PersistentAnomalyStore.IsActive(ID_DISPLAY))
        {
            // không spam ClearCue m?i frame; techGlitch t? gi? 0 n?u ?ã clear
            return;
        }

        DisplayVariant v = (DisplayVariant)PlayerPrefs.GetInt(PREF_DISPLAY_VARIANT, 0);
        var profile = GetVariantProfile(v);
        if (profile == null) profile = GetVariantProfile(DisplayVariant.PulseGap);

        // ramp severity
        float sev = PersistentAnomalyStore.GetSeverity01(ID_DISPLAY, profile.startSeverity01);
        sev += (profile.severityRampPerMinute / 60f) * dt;
        sev = Mathf.Clamp(sev, 0f, profile.maxSeverity01);
        PersistentAnomalyStore.SetSeverity01(ID_DISPLAY, sev);

        // pulse phase (ho?c continuous)
        if (!profile.continuousMode)
        {
            displayPhaseTimer -= dt;
            if (displayPhaseTimer <= 0f)
            {
                displayPhaseOn = !displayPhaseOn;
                displayPhaseTimer = displayPhaseOn
                    ? URandom.Range(profile.pulseOnSeconds.x, profile.pulseOnSeconds.y)
                    : URandom.Range(profile.pulseOffSeconds.x, profile.pulseOffSeconds.y);

                if (displayPhaseOn) displayJustTurnedOn = true;
            }
        }
        else
        {
            displayPhaseOn = true; // continuous
        }

        // lookup cue base from config (DISPLAY_GLITCH)
        TechCueProfile cue = FindCue(ID_DISPLAY);
        float baseIntensity = cue != null ? cue.intensity : 0.6f;
        float baseAudio = cue != null ? cue.audioMultiplier : 1f;
        Vector2 pos = PersistentAnomalyStore.GetPos01(ID_DISPLAY, cue != null ? cue.screenPos01 : new Vector2(0.5f, 0.9f));

        float intensity = displayPhaseOn ? Mathf.Clamp01(sev * baseIntensity * profile.intensityMul) : 0f;
        float audioMul = baseAudio * profile.audioMul * Mathf.Lerp(1f, 1.35f, sev);

        techGlitch.ApplyCue(intensity, pos, audioMul, playBurst: displayJustTurnedOn && profile.playBurstOnPulseStart);
        displayJustTurnedOn = false;
    }

    // =========================
    // ANSWER_OVERRIDE runtime (Local UI)
    // =========================
    private void TickAnswerOverride(float dt)
    {
        // ANSWER_OVERRIDE ???c b?t b?i JSON/FormView (SetActive + MarkOverwritten).
        if (!PersistentAnomalyStore.IsActive(ID_ANSWER))
            return;

        nextUiRefresh -= dt;
        if (nextUiRefresh <= 0f)
        {
            nextUiRefresh = uiTargetRefreshInterval;
            RefreshOverwrittenTargets();
        }

        nextUiBurst -= dt;
        if (nextUiBurst <= 0f)
        {
            nextUiBurst = URandom.Range(uiBurstIntervalSeconds.x, uiBurstIntervalSeconds.y);
            PulseOverwrittenOnce();
        }
    }

    private void PulseOverwrittenOnce()
    {
        if (overwrittenTargets.Count == 0) return;

        for (int i = overwrittenTargets.Count - 1; i >= 0; i--)
        {
            var p = overwrittenTargets[i];
            if (p == null || !p.IsOverwritten)
            {
                overwrittenTargets.RemoveAt(i);
                continue;
            }
            p.PlayOneBurst(uiBurstDuration);
        }
    }

    private void RefreshOverwrittenTargets()
    {
        overwrittenTargets.Clear();

#if UNITY_2023_1_OR_NEWER
        var all = UnityEngine.Object.FindObjectsByType<UILocalGlitchPulse>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var all = UnityEngine.Object.FindObjectsOfType<UILocalGlitchPulse>(true);
#endif
        for (int i = 0; i < all.Length; i++)
        {
            var p = all[i];
            if (p != null && p.IsOverwritten)
                overwrittenTargets.Add(p);
        }
    }

    private Vector2 RandomPos01()
    {
        float x = URandom.Range(randomPosXRange.x, randomPosXRange.y);
        float y = URandom.Range(randomPosYRange.x, randomPosYRange.y);
        return new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
    }

    private DisplayVariantProfile GetVariantProfile(DisplayVariant v)
    {
        if (displayVariants == null) return null;
        for (int i = 0; i < displayVariants.Length; i++)
        {
            var p = displayVariants[i];
            if (p != null && p.variant == v) return p;
        }
        return null;
    }

    private TechCueProfile FindCue(string anomalyId)
    {
        if (techGlitchConfig == null || techGlitchConfig.cues == null) return null;

        for (int i = 0; i < techGlitchConfig.cues.Length; i++)
        {
            var c = techGlitchConfig.cues[i];
            if (c == null) continue;
            if (string.Equals(c.anomalyId, anomalyId, StringComparison.OrdinalIgnoreCase))
                return c;
        }
        return null;
    }
}
