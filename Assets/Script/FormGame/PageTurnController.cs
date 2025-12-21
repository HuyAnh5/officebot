using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PageTurnController : MonoBehaviour
{
    [SerializeField] private RectTransform paperPanel;   // rect của PaperPanel
    [SerializeField] private Button foldCornerButton;    // FoldCornerButton
    [SerializeField] private CanvasGroup paperCanvasGroup; // optional

    private System.Action onFoldClicked;
    private bool armed;

    private void Awake()
    {
        foldCornerButton.gameObject.SetActive(false);
        foldCornerButton.onClick.AddListener(() =>
        {
            if (!armed) return;
            armed = false;
            onFoldClicked?.Invoke();
        });
    }

    public void HideFold()
    {
        foldCornerButton.gameObject.SetActive(false);
        armed = false;
        onFoldClicked = null;
    }

    public void ShowFold(System.Action onClick)
    {
        onFoldClicked = onClick;
        foldCornerButton.gameObject.SetActive(true);
        armed = true;
        StartCoroutine(FoldPop());
    }

    IEnumerator FoldPop()
    {
        var rt = foldCornerButton.transform as RectTransform;
        Vector3 a = Vector3.one * 0.85f;
        Vector3 b = Vector3.one;

        rt.localScale = a;
        float t = 0f;
        while (t < 0.12f)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(a, b, t / 0.12f);
            yield return null;
        }
        rt.localScale = b;
    }

    public IEnumerator TurnPageAnim()
    {
        // slide left đơn giản; bạn có thể đổi sang “page curl” sau
        Vector2 start = paperPanel.anchoredPosition;
        Vector2 end = start + new Vector2(-1600f, 0f);

        float t = 0f;
        while (t < 0.18f)
        {
            t += Time.unscaledDeltaTime;
            float k = t / 0.18f;
            paperPanel.anchoredPosition = Vector2.Lerp(start, end, k);
            if (paperCanvasGroup) paperCanvasGroup.alpha = Mathf.Lerp(1f, 0f, k);
            yield return null;
        }

        // reset vị trí để render trang mới
        paperPanel.anchoredPosition = start;
        if (paperCanvasGroup) paperCanvasGroup.alpha = 1f;
    }
}
