using System;
using UnityEngine;

[CreateAssetMenu(menuName = "OfficeBot/Tech Glitch Config", fileName = "TechGlitchConfig")]
public class TechGlitchConfig : ScriptableObject
{
    [Header("Smoothing")]
    [Range(0.01f, 30f)] public float intensityLerpSpeed = 10f;

    [Header("Audio")]
    [Range(0f, 1f)] public float baseLoopVolume = 0.25f;
    [Range(0f, 1f)] public float baseBurstVolume = 0.35f;
    [Range(0f, 1f)] public float maxStereoPan = 0.85f; // 0..1

    [Header("URPGlitch Defaults (multiplied by intensity)")]
    [Range(0f, 1f)] public float analogScanLineJitterMax = 0.65f;
    [Range(0f, 1f)] public float analogVerticalJumpMax = 0.25f;
    [Range(0f, 1f)] public float analogHorizontalShakeMax = 0.15f;

    // URPGlitch Color Drift thường scale lớn (tới ~25)
    [Range(0f, 25f)] public float analogColorDriftMax = 6f;

    [Range(0f, 1f)] public float digitalIntensityMax = 0.55f;

    [Header("Per anomaly (priority + intensity + default screen pos)")]
    public TechCueProfile[] cues;
}

[Serializable]
public class TechCueProfile
{
    public string anomalyId;          // "DISPLAY_GLITCH", "ANSWER_OVERRIDE", ...
    public int priority;              // số lớn hơn = ưu tiên hơn
    [Range(0f, 1f)] public float intensity = 0.6f;
    public Vector2 screenPos01 = new Vector2(0.5f, 0.9f); // 0..1
    [Range(0f, 2f)] public float audioMultiplier = 1f;
}
