using System.Collections;
using TMPro;
using UnityEngine;

public class UILocalGlitchPulse : MonoBehaviour
{
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private RectTransform targetRT;

    [Header("Pulse")]
    [SerializeField] private float maxAlphaDrop = 0.55f;
    [SerializeField] private float maxPosJitter = 6f;
    [SerializeField] private float maxRotJitter = 2.5f;
    [SerializeField] private float tickInterval = 0.02f;

    [Header("Scramble")]
    [SerializeField] private bool scrambleText = true;
    [SerializeField] private string scrambleChars = "•▲□#@$%&";
    [SerializeField, Range(0f, 1f)] private float scrambleRatio = 0.25f;

    [Header("State")]
    [Tooltip("Chỉ những target bị đánh dấu Overwritten mới được phép glitch")]
    [SerializeField] private bool overwritten;

    private float snapshotAlpha;
    private Vector2 snapshotAnchoredPos;
    private Quaternion snapshotRot;
    private string snapshotText;

    private Coroutine co;

    public bool IsOverwritten => overwritten;

    private void Awake()
    {
        if (targetText == null) targetText = GetComponentInChildren<TMP_Text>(true);
        if (targetRT == null) targetRT = (targetText != null) ? targetText.rectTransform : GetComponent<RectTransform>();

        CaptureSnapshot();
    }

    private void OnEnable()
    {
        CaptureSnapshot();
        ResetVisualImmediate();
    }

    private void CaptureSnapshot()
    {
        if (targetText != null)
        {
            snapshotAlpha = targetText.alpha;
            snapshotText = targetText.text;
        }
        if (targetRT != null)
        {
            snapshotAnchoredPos = targetRT.anchoredPosition;
            snapshotRot = targetRT.localRotation;
        }
    }

    public void SetOverwritten(bool on)
    {
        overwritten = on;

        // QUAN TRỌNG: lúc bật overwritten, recapture để snapshotText = label mới
        CaptureSnapshot();

        if (!overwritten)
            ResetVisualImmediate();
    }

    public void PlayOneBurst(float duration)
    {
        if (!overwritten) return;

        if (co != null) StopCoroutine(co);
        co = StartCoroutine(Burst(duration));
    }

    public void ResetVisualImmediate()
    {
        if (co != null)
        {
            StopCoroutine(co);
            co = null;
        }

        if (targetText != null)
        {
            targetText.alpha = snapshotAlpha;
            if (!string.IsNullOrEmpty(snapshotText))
                targetText.text = snapshotText;
        }

        if (targetRT != null)
        {
            targetRT.anchoredPosition = snapshotAnchoredPos;
            targetRT.localRotation = snapshotRot;
        }
    }

    private IEnumerator Burst(float duration)
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            if (targetText != null)
            {
                targetText.alpha = Mathf.Max(0f, snapshotAlpha - Random.Range(0f, maxAlphaDrop));

                if (scrambleText && !string.IsNullOrEmpty(snapshotText))
                    targetText.text = Scramble(snapshotText);
            }

            if (targetRT != null)
            {
                targetRT.anchoredPosition = snapshotAnchoredPos + Random.insideUnitCircle * maxPosJitter;
                targetRT.localRotation = snapshotRot * Quaternion.Euler(0f, 0f, Random.Range(-maxRotJitter, maxRotJitter));
            }

            float wait = Mathf.Max(0.005f, tickInterval);
            yield return new WaitForSecondsRealtime(wait);
        }

        ResetVisualImmediate();
        co = null;
    }

    private string Scramble(string s)
    {
        if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(scrambleChars)) return s;

        char[] arr = s.ToCharArray();
        int n = Mathf.CeilToInt(arr.Length * scrambleRatio);

        for (int i = 0; i < n; i++)
        {
            int idx = Random.Range(0, arr.Length);
            arr[idx] = scrambleChars[Random.Range(0, scrambleChars.Length)];
        }

        return new string(arr);
    }
}
