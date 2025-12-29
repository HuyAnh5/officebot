using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;


public class TerminalView : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TMP_Text terminalText;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Cursor (RECOMMENDED: separate TMP so it doesn't affect wrapping)")]
    [SerializeField] private TMP_Text cursorText;
    [SerializeField] private string cursorChar = "|";
    [SerializeField] private float cursorBlinkInterval = 0.45f;
    [SerializeField] private bool blinkWhileTyping = true;
    [SerializeField] private bool showCursorWhenEmpty = true;
    [SerializeField] private float cursorXOffset = 2f;
    [SerializeField] private float cursorYOffset = 0f;

    [Header("Follow / Lock")]
    [SerializeField] private bool lockScrollInputWhilePlayback = true;
    [SerializeField] private GameObject scrollInputBlocker;

    [Header("Typewriter")]
    [SerializeField] private float charsPerSecond = 28f;

    [Header("Punctuation Pause (seconds)")]
    [SerializeField] private float pauseComma = 0.06f;
    [SerializeField] private float pausePeriod = 0.18f;
    [SerializeField] private float pauseQuestion = 0.22f;
    [SerializeField] private float pauseExclaim = 0.20f;
    [SerializeField] private float pauseColon = 0.10f;

    // "..." will be shown dot-by-dot using pauseEllipsisDot between each dot.
    // pauseEllipsis is used AFTER the 3rd dot (also used for unicode '…').
    [SerializeField] private float pauseEllipsisDot = 1.0f;
    [SerializeField] private float pauseEllipsis = 0.0f;

    [Header("Pinned Terminal Order (overlay)")]
    [SerializeField] private RectTransform pinnedRoot;     // panel trắng overlay
    [SerializeField] private TMP_Text pinnedText;          // TMP trong panel
    [SerializeField] private RectTransform pinnedBorder;   // Image viền trắng (RectTransform)

    [Header("Pinned Anim")]
    [SerializeField] private float pinnedSlideOffsetY = 60f;
    [SerializeField] private float pinnedShowDur = 0.25f;
    [SerializeField] private float pinnedHideDur = 0.20f;

    [Header("Pinned Timing")]
    [SerializeField] private float pinnedAutoHideDefaultSec = 10f; // chỉnh được
    [SerializeField] private float pinnedCharsPerSecond = 28f;     // giống terminal


    private Vector2 pinnedShownPos;
    private bool pinnedPosCached;
    private Sequence pinnedSeq;
    private Tween pinnedBorderTween;
    private Tween pinnedAutoHideTween;
    private Coroutine pinnedTypeCo;

    public float PinnedHideDuration => pinnedHideDur;
    public bool IsPinnedVisible => pinnedRoot != null && pinnedRoot.gameObject.activeSelf;




    private readonly StringBuilder history = new StringBuilder();

    // Active line is stored as FULL text (prefix + message). We reveal it using maxVisibleCharacters.
    private string activeFullLine = string.Empty;
    private int activeVisibleCount = 0;
    private int activePrefixLength = 0;

    private string renderedCache = string.Empty;
    private bool textDirty = true;

    private Coroutine blinkCo;
    private Coroutine typeCo;

    private bool playbackActive;
    private bool cursorOn = true;
    private bool isTyping;
    private bool waitingDuringTyping;

    private bool forceFollowBottom;

    public bool IsTyping => isTyping;

    // What is currently visible in the active line (prefix + typed so far)
    public string ActiveLineText
    {
        get
        {
            if (string.IsNullOrEmpty(activeFullLine)) return string.Empty;
            int n = Mathf.Clamp(activeVisibleCount, 0, activeFullLine.Length);
            return activeFullLine.Substring(0, n);
        }
    }

    public bool HasActiveLine => !string.IsNullOrEmpty(activeFullLine);

    private void OnEnable()
    {
        blinkCo = StartCoroutine(CursorBlinkLoop());
        RefreshAll();
        HidePinnedOrderImmediate();
    }

    private void OnDisable()
    {
        if (blinkCo != null) StopCoroutine(blinkCo);
        if (typeCo != null) StopCoroutine(typeCo);
    }

    public void SetPlaybackActive(bool active)
    {
        playbackActive = active;

        if (lockScrollInputWhilePlayback && scrollInputBlocker != null)
            scrollInputBlocker.SetActive(active);

        // Không khóa blink theo playback nữa.
        // Cursor sẽ blink hay không do CursorBlinkLoop quyết định (dựa trên waiting/typing).
        cursorOn = true;
        ApplyVisibilityAndCursor();
    }

    private void CachePinnedPos()
    {
        if (pinnedPosCached) return;
        if (pinnedRoot == null) return;
        pinnedShownPos = pinnedRoot.anchoredPosition;
        pinnedPosCached = true;
    }

    private void KillPinned()
    {
        pinnedSeq?.Kill();
        pinnedBorderTween?.Kill();
        pinnedAutoHideTween?.Kill();
        pinnedSeq = null;
        pinnedBorderTween = null;
        pinnedAutoHideTween = null;

        if (pinnedTypeCo != null)
        {
            StopCoroutine(pinnedTypeCo);
            pinnedTypeCo = null;
        }
    }

    public void HidePinnedOrderImmediate()
    {
        CachePinnedPos();
        KillPinned();

        if (pinnedText != null)
        {
            pinnedText.text = "";
            pinnedText.maxVisibleCharacters = int.MaxValue;
        }

        if (pinnedRoot != null)
            pinnedRoot.gameObject.SetActive(false);
    }


    public void ShowPinnedOrder(string text, float autoHideSec = -1f)
    {
        CachePinnedPos();
        if (pinnedRoot == null || pinnedText == null) return;

        KillPinned();
        pinnedRoot.gameObject.SetActive(true);

        float offY = GetPinnedOffY();
        pinnedRoot.anchoredPosition = pinnedShownPos + new Vector2(0f, offY);

        pinnedText.text = text ?? "";
        pinnedText.maxVisibleCharacters = 0;

        if (pinnedBorder != null)
            pinnedBorder.localScale = Vector3.one;

        float hold = (autoHideSec > 0f) ? autoHideSec : pinnedAutoHideDefaultSec;

        pinnedSeq = DOTween.Sequence().SetUpdate(true);
        pinnedSeq.Append(pinnedRoot.DOAnchorPos(pinnedShownPos, pinnedShowDur).SetEase(Ease.OutCubic));
        pinnedSeq.AppendCallback(() =>
        {
            pinnedTypeCo = StartCoroutine(PinnedTypeRoutine());

            if (pinnedBorder != null)
                pinnedBorderTween = pinnedBorder.DOScaleX(0f, hold).SetEase(Ease.Linear).SetUpdate(true);

            pinnedAutoHideTween = DOVirtual.DelayedCall(hold, HidePinnedOrderAnimated, ignoreTimeScale: true);
        });
    }


    public void HidePinnedOrderAnimated()
    {
        CachePinnedPos();
        if (pinnedRoot == null) return;
        if (!pinnedRoot.gameObject.activeSelf) return; // đã tắt rồi thì thôi

        KillPinned();

        float offY = GetPinnedOffY();
        Vector2 hiddenPos = pinnedShownPos + new Vector2(0f, offY);

        pinnedSeq = DOTween.Sequence().SetUpdate(true);
        pinnedSeq.Append(pinnedRoot.DOAnchorPos(hiddenPos, pinnedHideDur).SetEase(Ease.InCubic));
        pinnedSeq.OnComplete(() =>
        {
            if (pinnedText != null)
            {
                pinnedText.text = "";
                pinnedText.maxVisibleCharacters = int.MaxValue;
            }
            pinnedRoot.gameObject.SetActive(false);
        });
    }


    private IEnumerator PinnedTypeRoutine()
    {
        if (pinnedText == null) yield break;

        // cập nhật mesh để biết characterCount
        pinnedText.ForceMeshUpdate();

        int total = pinnedText.textInfo.characterCount;
        if (total <= 0)
        {
            pinnedText.maxVisibleCharacters = int.MaxValue;
            yield break;
        }

        float cps = Mathf.Max(1f, pinnedCharsPerSecond > 0 ? pinnedCharsPerSecond : charsPerSecond);
        float delay = 1f / cps;

        int visible = 0;
        while (visible < total)
        {
            visible++;
            pinnedText.maxVisibleCharacters = visible;
            yield return new WaitForSecondsRealtime(delay);
        }
    }

    public void SetForceFollowBottom(bool on)
    {
        forceFollowBottom = on;
        if (on) ForceScrollToBottom();
    }

    public void EnsureCursorVisible()
    {
        ApplyVisibilityAndCursor();
    }

    public void BeginLine(string prefix)
    {
        StopTypingInternal();

        // Commit previous active line as a finished line
        CommitActiveLineToHistory();

        activeFullLine = prefix ?? string.Empty;
        activePrefixLength = activeFullLine.Length;
        activeVisibleCount = activePrefixLength;

        MarkDirty();
        RefreshAll();
    }

    public void TypeMessage(string message)
    {
        StopTypingInternal();

        string prefix = (activePrefixLength > 0 && activeFullLine.Length >= activePrefixLength)
            ? activeFullLine.Substring(0, activePrefixLength)
            : string.Empty;

        activeFullLine = prefix + (message ?? string.Empty);
        activeVisibleCount = Mathf.Clamp(activeVisibleCount, 0, activePrefixLength);

        MarkDirty();
        RefreshAll();

        typeCo = StartCoroutine(TypeRoutine());
    }

    public void ForceCompleteActiveLine(string fullLine)
    {
        StopTypingInternal();

        activeFullLine = fullLine ?? string.Empty;
        activePrefixLength = 0;
        activeVisibleCount = activeFullLine.Length;

        MarkDirty();
        RefreshAll();
    }

    public void CommitActiveLineToHistory()
    {
        if (string.IsNullOrEmpty(activeFullLine)) return;

        int n = Mathf.Clamp(activeVisibleCount, 0, activeFullLine.Length);
        if (n > 0)
        {
            history.Append(activeFullLine.Substring(0, n));
            history.Append('\n');
        }

        activeFullLine = string.Empty;
        activeVisibleCount = 0;
        activePrefixLength = 0;

        MarkDirty();
        RefreshAll();
    }

    public void AppendLinesInstantKeepingLastActive(string[] fullLines)
    {
        StopTypingInternal();

        if (fullLines == null || fullLines.Length == 0)
        {
            RefreshAll();
            return;
        }

        CommitActiveLineToHistory();

        for (int i = 0; i < fullLines.Length - 1; i++)
        {
            history.Append(fullLines[i] ?? string.Empty);
            history.Append('\n');
        }

        activeFullLine = fullLines[fullLines.Length - 1] ?? string.Empty;
        activeVisibleCount = activeFullLine.Length;
        activePrefixLength = 0;

        MarkDirty();
        RefreshAll();
    }

    private IEnumerator TypeRoutine()
    {
        isTyping = true;
        waitingDuringTyping = false;

        float baseDelay = 1f / Mathf.Max(1f, charsPerSecond);

        while (activeVisibleCount < activeFullLine.Length)
        {
            int i = activeVisibleCount;

            // Khi chuẩn bị reveal ký tự -> coi là "đang gõ", không blink
            waitingDuringTyping = false;

            // ASCII ellipsis "..." => dot, WAIT, dot, WAIT, dot, WAIT-after
            //if (IsAsciiEllipsisAt(activeFullLine, i))
            //{
            //    // dot 1
            //    activeVisibleCount += 1;
            //    ApplyVisibilityAndCursor();

            //    waitingDuringTyping = true; // đang chờ giữa các dấu chấm => blink
            //    yield return new WaitForSeconds(pauseEllipsisDot);

            //    // dot 2
            //    waitingDuringTyping = false;
            //    activeVisibleCount += 1;
            //    ApplyVisibilityAndCursor();

            //    waitingDuringTyping = true;
            //    yield return new WaitForSeconds(pauseEllipsisDot);

            //    // dot 3
            //    waitingDuringTyping = false;
            //    activeVisibleCount += 1;
            //    ApplyVisibilityAndCursor();

            //    waitingDuringTyping = true;
            //    yield return new WaitForSeconds(pauseEllipsis);

            //    continue;
            //}

            char c = activeFullLine[i];
            activeVisibleCount += 1;
            ApplyVisibilityAndCursor();

            float extra = GetPunctuationPause(c);

            if (extra > 0f)
            {
                // pause do dấu câu => coi là WAITING => blink
                waitingDuringTyping = true;
                yield return new WaitForSeconds(baseDelay + extra);
            }
            else
            {
                // khoảng delay gõ bình thường => coi là đang gõ => không blink
                waitingDuringTyping = false;
                yield return new WaitForSeconds(baseDelay);
            }
        }

        isTyping = false;
        waitingDuringTyping = false;
        typeCo = null;
        ApplyVisibilityAndCursor();
    }


    private static bool IsAsciiEllipsisAt(string s, int index)
    {
        if (string.IsNullOrEmpty(s)) return false;
        if (index < 0 || index + 2 >= s.Length) return false;
        return s[index] == '.' && s[index + 1] == '.' && s[index + 2] == '.';
    }

    private float GetPunctuationPause(char c)
    {
        return c switch
        {
            ',' => pauseComma,
            '.' => pausePeriod,
            '?' => pauseQuestion,
            '!' => pauseExclaim,
            ':' => pauseColon,
            '…' => pauseEllipsis, // unicode ellipsis
            _ => 0f
        };
    }

    private IEnumerator CursorBlinkLoop()
    {
        while (true)
        {
            // Blink khi:
            // - không typing (startDelaySec / gap / idle / hold), hoặc
            // - đang typing nhưng đang "waiting" vì dấu câu/ellipsis
            bool shouldBlink = (!isTyping) || waitingDuringTyping;

            if (shouldBlink)
            {
                cursorOn = !cursorOn;
            }
            else
            {
                // Đang reveal ký tự => giữ cursor solid
                if (!cursorOn) cursorOn = true;
            }

            ApplyVisibilityAndCursor();
            yield return new WaitForSeconds(cursorBlinkInterval);
        }
    }



    private void StopTypingInternal()
    {
        if (typeCo != null)
        {
            StopCoroutine(typeCo);
            typeCo = null;
        }
        isTyping = false;
        waitingDuringTyping = false;
    }


    private void MarkDirty()
    {
        textDirty = true;
    }

    private void RefreshAll()
    {
        RebuildTextIfNeeded();
        ApplyVisibilityAndCursor();

        if (playbackActive || forceFollowBottom)
            FollowDuringPlayback();
    }

    private void RebuildTextIfNeeded()
    {
        if (!textDirty) return;
        if (terminalText == null) return;

        var sb = new StringBuilder(history.Length + activeFullLine.Length);
        sb.Append(history);
        sb.Append(activeFullLine);

        renderedCache = sb.ToString();
        terminalText.text = renderedCache;

        textDirty = false;

        // Ensure TMP generates geometry for cursor placement
        terminalText.ForceMeshUpdate();
    }

    private void ApplyVisibilityAndCursor()
    {
        if (terminalText == null) return;

        RebuildTextIfNeeded();

        int fullLen = renderedCache.Length;
        int visibleTotal = fullLen;

        if (!string.IsNullOrEmpty(activeFullLine))
        {
            // history is always fully visible; active line is partially visible
            visibleTotal = Mathf.Clamp(history.Length + activeVisibleCount, 0, fullLen);
        }

        terminalText.maxVisibleCharacters = visibleTotal;

        UpdateCursor(visibleTotal);

        if (playbackActive || forceFollowBottom)
            FollowDuringPlayback();
    }

    private void UpdateCursor(int visibleTotalChars)
    {
        if (cursorText == null)
        {
            // Fallback: nếu không có cursorText riêng thì bỏ qua (tránh wrap nhảy)
            return;
        }

        bool show = showCursorWhenEmpty || visibleTotalChars > 0;
        cursorText.gameObject.SetActive(show);
        if (!show) return;

        cursorText.text = cursorOn ? cursorChar : " ";

        // Đảm bảo cursor luôn nằm trên cùng (không bị che)
        cursorText.rectTransform.SetAsLastSibling();

        // Update text geometry
        terminalText.ForceMeshUpdate();

        var ti = terminalText.textInfo;
        if (ti == null || ti.characterCount == 0)
        {
            cursorText.rectTransform.anchoredPosition = Vector2.zero;
            return;
        }

        int target = Mathf.Clamp(visibleTotalChars - 1, 0, ti.characterCount - 1);

        // Lùi lại tới ký tự thật sự visible (bỏ qua newline/space)
        int idx = target;
        while (idx > 0 && !ti.characterInfo[idx].isVisible)
            idx--;

        var ch = ti.characterInfo[idx];

        // Dùng xAdvance để đặt con trỏ "sau" ký tự cuối (ổn định hơn bottomRight)
        Vector3 localInText = new Vector3(ch.xAdvance + cursorXOffset, ch.descender + cursorYOffset, 0f);

        // Convert local-in-terminalText -> screen -> local-in-parent (đúng cả Overlay/Camera canvas)
        RectTransform parentRT = cursorText.rectTransform.parent as RectTransform;
        if (parentRT == null)
            parentRT = cursorText.rectTransform.root as RectTransform;

        Canvas canvas = terminalText.canvas;
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        Vector3 world = terminalText.rectTransform.TransformPoint(localInText);
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRT, screen, cam, out Vector2 localPoint))
        {
            // Lưu ý: CursorTMP nên để anchors ở giữa (0.5,0.5) như ảnh hiện tại để localPoint khớp.
            cursorText.rectTransform.anchoredPosition = localPoint;
        }
    }


    // ✅ Fix LV1: nếu content ngắn hơn viewport -> pin TOP, còn dài -> pin BOTTOM
    private void FollowDuringPlayback()
    {
        if (scrollRect == null) return;
        if (scrollRect.content == null) return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

        if (forceFollowBottom)
        {
            scrollRect.verticalNormalizedPosition = 0f;
            return;
        }

        float contentH = scrollRect.content.rect.height;
        float viewH = scrollRect.viewport != null ? scrollRect.viewport.rect.height
                                                  : ((RectTransform)scrollRect.transform).rect.height;

        bool contentFits = contentH <= viewH + 1f;
        scrollRect.verticalNormalizedPosition = contentFits ? 1f : 0f;
    }

    public void ForceScrollToBottom()
    {
        if (scrollRect == null || scrollRect.content == null) return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

        scrollRect.verticalNormalizedPosition = 0f;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
    }

    // giữ API cũ nếu file khác đang gọi
    public void SetForceBottomOverride(bool on)
    {
        if (on) ForceScrollToBottom();
    }

    private float GetPinnedOffY()
    {
        if (pinnedRoot == null) return pinnedSlideOffsetY;
        // đảm bảo trượt ra khỏi màn: ít nhất = chiều cao panel + margin
        return Mathf.Max(pinnedSlideOffsetY, pinnedRoot.rect.height + 20f);
    }

}
