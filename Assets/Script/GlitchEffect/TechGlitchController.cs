using UnityEngine;
using UnityEngine.Rendering;
using URPGlitch;

public class TechGlitchController : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private TechGlitchConfig config;

    [Header("URP Volume")]
    [SerializeField] private Volume volume;

    [Header("Audio")]
    [SerializeField] private AudioSource loopSource;   // loop noise
    [SerializeField] private AudioSource burstSource;  // one-shot crackle (optional)

    [Header("UI Marker (optional)")]
    [SerializeField] private RectTransform markerRT;   // on overlay canvas
    [SerializeField] private CanvasGroup markerCG;

    [Header("Debug (read-only)")]
    [SerializeField] private float debug_currentIntensity;
    [SerializeField] private float debug_targetIntensity;

    private AnalogGlitchVolume analog;
    private DigitalGlitchVolume digital;

    private float currentIntensity;
    private float targetIntensity;

    private Vector2 currentPos01 = new Vector2(0.5f, 0.9f);
    private Vector2 targetPos01 = new Vector2(0.5f, 0.9f);

    private float currentAudioMult = 1f;
    private float targetAudioMult = 1f;

    private bool isOn;

    private void Awake()
    {
        CacheOverrides();
        HardResetImmediate(); // QUAN TRỌNG: tránh “vào là max”
    }

    private void CacheOverrides()
    {
        if (volume != null && volume.profile != null)
        {
            volume.profile.TryGet(out analog);
            volume.profile.TryGet(out digital);
        }
    }

    /// <summary>Áp cue (intensity + vị trí + audio). playBurst dùng cho crackle một phát.</summary>
    public void ApplyCue(float intensity01, Vector2 screenPos01, float audioMultiplier, bool playBurst = false)
    {
        targetIntensity = Mathf.Clamp01(intensity01);
        targetPos01 = new Vector2(Mathf.Clamp01(screenPos01.x), Mathf.Clamp01(screenPos01.y));
        targetAudioMult = Mathf.Max(0f, audioMultiplier);

        debug_targetIntensity = targetIntensity;

        SetEnabled(targetIntensity > 0.001f);

        if (playBurst && burstSource != null && burstSource.clip != null)
        {
            float vol = config != null ? config.baseBurstVolume : 0.3f;
            burstSource.PlayOneShot(burstSource.clip, vol);
        }
    }

    /// <summary>Tắt cue ngay lập tức (và kéo toàn bộ param về 0 để chắc chắn hết glitch).</summary>
    public void ClearCue()
    {
        targetIntensity = 0f;
        targetAudioMult = 1f;
        debug_targetIntensity = 0f;

        HardResetImmediate();
    }

    private void HardResetImmediate()
    {
        // reset state
        currentIntensity = 0f;
        currentAudioMult = 1f;
        currentPos01 = targetPos01;
        debug_currentIntensity = 0f;

        // QUAN TRỌNG: kéo param về 0 ngay (đừng chỉ tắt active)
        ApplyToVolume(0f);

        // tắt components + marker + audio
        SetEnabled(false);

        if (loopSource != null)
        {
            if (loopSource.isPlaying) loopSource.Stop();
            loopSource.volume = 0f;
            loopSource.panStereo = 0f;
        }

        if (markerCG != null) markerCG.alpha = 0f;
        if (markerRT != null) markerRT.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (config == null) return;

        // nếu đang off hoàn toàn, giữ param = 0 để không “dính” giá trị cũ
        if (!isOn && targetIntensity <= 0.001f)
        {
            ApplyToVolume(0f);
            return;
        }

        // smoothing
        currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, Time.unscaledDeltaTime * config.intensityLerpSpeed);
        currentPos01 = Vector2.Lerp(currentPos01, targetPos01, Time.unscaledDeltaTime * config.intensityLerpSpeed);
        currentAudioMult = Mathf.Lerp(currentAudioMult, targetAudioMult, Time.unscaledDeltaTime * config.intensityLerpSpeed);

        debug_currentIntensity = currentIntensity;
        debug_targetIntensity = targetIntensity;

        // bật/tắt theo intensity hiện tại (để hết hẳn khi gần 0)
        SetEnabled(currentIntensity > 0.001f);

        // apply params
        ApplyToVolume(currentIntensity);

        // marker position
        if (markerRT != null)
        {
            markerRT.anchorMin = markerRT.anchorMax = currentPos01;
            markerRT.anchoredPosition = Vector2.zero;
        }
        if (markerCG != null)
        {
            markerCG.alpha = Mathf.Clamp01(currentIntensity);
        }

        // audio: pan theo X, volume theo intensity
        if (loopSource != null)
        {
            float pan = Mathf.Lerp(-config.maxStereoPan, config.maxStereoPan, currentPos01.x);
            float vol = config.baseLoopVolume * currentIntensity * currentAudioMult;

            loopSource.panStereo = pan;
            loopSource.volume = Mathf.Clamp01(vol);

            if (isOn)
            {
                if (!loopSource.isPlaying && loopSource.clip != null) loopSource.Play();
            }
            else
            {
                if (loopSource.isPlaying) loopSource.Stop();
                loopSource.volume = 0f;
            }
        }
    }

    private void ApplyToVolume(float intensity01)
    {
        float i = Mathf.Clamp01(intensity01);

        if (analog != null)
        {
            float scanMax = config != null ? config.analogScanLineJitterMax : 0f;
            float vjMax = config != null ? config.analogVerticalJumpMax : 0f;
            float hsMax = config != null ? config.analogHorizontalShakeMax : 0f;
            float cdMax = config != null ? config.analogColorDriftMax : 0f;

            analog.scanLineJitter.value = scanMax * i;
            analog.verticalJump.value = vjMax * i;
            analog.horizontalShake.value = hsMax * i;
            analog.colorDrift.value = cdMax * i;
        }

        if (digital != null)
        {
            float diMax = config != null ? config.digitalIntensityMax : 0f;
            digital.intensity.value = diMax * i;
        }
    }

    private void SetEnabled(bool on)
    {
        if (isOn == on) return;
        isOn = on;

        if (analog != null) analog.active = on;
        if (digital != null) digital.active = on;

        if (markerRT != null) markerRT.gameObject.SetActive(on);
        if (markerCG != null && !on) markerCG.alpha = 0f;
    }
}
