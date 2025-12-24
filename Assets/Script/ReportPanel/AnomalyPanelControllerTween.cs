using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class AnomalyPanelControllerTween : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform leftColumn;
    [SerializeField] private RectTransform terminalPanel;
    [SerializeField] private RectTransform anomalyPanel; // chính object này
    [SerializeField] private GameObject body;            // Body
    [SerializeField] private Button arrowButton;         // ArrowButton
    [SerializeField] private RectTransform arrowIcon;    // ArrowIcon (hoặc chính ArrowButton)

    [Header("Sizes")]
    [SerializeField] private float terminalOpenHeight = 550f;   // khi panel mở
    [SerializeField] private float collapsedHandleHeight = 56f; // khi panel đóng

    [Header("Tween")]
    [SerializeField] private float duration = 1f;
    [SerializeField] private Ease ease = Ease.InOutSine;

    [SerializeField] private TerminalView terminalView; // kéo object có TerminalView component vào đây



    private bool isOpen;
    private Tween tTerminal, tPanel, tArrow;

    private void Awake()
    {
        if (arrowButton != null)
            arrowButton.onClick.AddListener(Toggle);
    }

    public void SetDay(int day)
    {
        bool show = day >= 2;
        gameObject.SetActive(show);

        if (!show)
        {
            // Day1: terminal full
            SetHeight(terminalPanel, GetFullHeight());
            return;
        }

        SetOpen(false, instant: true);
    }

    public void Toggle() => SetOpen(!isOpen, instant: false);

    public void SetOpen(bool open, bool instant)
    {
        isOpen = open;
        KillTweens();

        float fullH = GetFullHeight();
        float targetTerminalH = open ? terminalOpenHeight : fullH;
        float targetPanelH = open ? Mathf.Max(collapsedHandleHeight, fullH - terminalOpenHeight) : collapsedHandleHeight;
        float targetRot = open ? 180f : 0f;

        void FollowBottom()
        {
            terminalView?.ForceScrollToBottom();
        }

        if (instant)
        {
            SetHeight(terminalPanel, targetTerminalH);
            SetHeight(anomalyPanel, targetPanelH);

            if (arrowIcon != null) arrowIcon.localEulerAngles = new Vector3(0, 0, targetRot);
            if (body != null) body.SetActive(open);

            FollowBottom();
            return;
        }

        if (open && body != null) body.SetActive(true);

        float startTerminalH = terminalPanel.rect.height;
        float startPanelH = anomalyPanel.rect.height;

        // ✅ trong lúc tween: luôn giữ terminal ở đáy
        terminalView?.SetForceFollowBottom(true);
        FollowBottom();

        var seq = DOTween.Sequence().SetUpdate(true).SetEase(ease);

        tTerminal = DOTween.To(() => startTerminalH, v =>
        {
            startTerminalH = v;
            SetHeight(terminalPanel, v);
        }, targetTerminalH, duration).SetEase(ease).SetUpdate(true);

        tPanel = DOTween.To(() => startPanelH, v =>
        {
            startPanelH = v;
            SetHeight(anomalyPanel, v);
        }, targetPanelH, duration).SetEase(ease).SetUpdate(true);

        seq.Join(tTerminal);
        seq.Join(tPanel);

        if (arrowIcon != null)
        {
            tArrow = arrowIcon.DOLocalRotate(new Vector3(0, 0, targetRot), duration)
                              .SetEase(ease).SetUpdate(true);
            seq.Join(tArrow);
        }

        // ✅ mỗi frame trong lúc panel đang co/giãn -> ép scroll về đáy
        seq.OnUpdate(FollowBottom);

        seq.OnComplete(() =>
        {
            if (!open && body != null) body.SetActive(false);

            FollowBottom();

            // ✅ kết thúc tween -> tắt force follow
            terminalView?.SetForceFollowBottom(false);
        });
    }


    private float GetFullHeight()
    {
        return leftColumn != null ? leftColumn.rect.height : 1080f;
    }

    private static void SetHeight(RectTransform rt, float h)
    {
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
    }

    private void KillTweens()
    {
        tTerminal?.Kill();
        tPanel?.Kill();
        tArrow?.Kill();
    }
}
    