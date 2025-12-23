using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TerminalView : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TMP_Text terminalText;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Follow / Lock")]
    [SerializeField] private bool lockScrollInputWhilePlayback = true;
    [SerializeField] private GameObject scrollInputBlocker; // Image phủ lên ScrollView (Raycast Target = true)

    [Header("Typewriter")]
    [SerializeField] private float charsPerSecond = 28f;

    [Header("Cursor")]
    [SerializeField] private string cursorChar = "|";
    [SerializeField] private float cursorBlinkInterval = 0.45f;
    [SerializeField] private bool blinkWhileTyping = true;
    [SerializeField] private bool showCursorWhenEmpty = true;

    [Header("Punctuation Pause (seconds)")]
    [SerializeField] private float pauseComma = 0.06f;
    [SerializeField] private float pausePeriod = 0.18f;
    [SerializeField] private float pauseQuestion = 0.22f;
    [SerializeField] private float pauseExclaim = 0.20f;
    [SerializeField] private float pauseColon = 0.10f;
    [SerializeField] private float pauseEllipsis = 0.25f;

    private readonly StringBuilder history = new StringBuilder();    // các dòng đã commit (có '\n')
    private readonly StringBuilder activeLine = new StringBuilder(); // dòng hiện tại (không có '\n')

    private Coroutine blinkCo;
    private Coroutine typeCo;

    private bool playbackActive;
    private bool cursorOn = true;
    private bool isTyping;

    public bool IsTyping => isTyping;
    public string ActiveLineText => activeLine.ToString();
    public bool HasActiveLine => activeLine.Length > 0;

    private void OnEnable()
    {
        blinkCo = StartCoroutine(CursorBlinkLoop());
        Refresh();
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

        // khi bắt đầu chạy, ép follow để player theo kịp
        if (active) FollowDuringPlayback();
    }

    public void EnsureCursorVisible()
    {
        Refresh();
    }

    public void BeginLine(string prefix)
    {
        StopTypingInternal();

        // commit dòng trước để dòng mới xuống hàng
        CommitActiveLineToHistory();

        activeLine.Clear();
        activeLine.Append(prefix);
        Refresh();
    }

    public void TypeMessage(string message)
    {
        StopTypingInternal();
        typeCo = StartCoroutine(TypeRoutine(message));
    }

    public void ForceCompleteActiveLine(string fullLine)
    {
        StopTypingInternal();
        activeLine.Clear();
        activeLine.Append(fullLine);
        Refresh();
    }

    public void CommitActiveLineToHistory()
    {
        if (activeLine.Length == 0) return;

        history.Append(activeLine);
        history.Append('\n');
        activeLine.Clear();
        Refresh();
    }

    /// <summary>
    /// Dump nhiều dòng, giữ dòng cuối làm active để cursor nhấp nháy ở cuối.
    /// </summary>
    public void AppendLinesInstantKeepingLastActive(string[] fullLines)
    {
        StopTypingInternal();

        if (fullLines == null || fullLines.Length == 0)
        {
            Refresh();
            return;
        }

        // commit dòng đang đứng (nếu có) để dump nằm phía dưới
        CommitActiveLineToHistory();

        for (int i = 0; i < fullLines.Length - 1; i++)
        {
            history.Append(fullLines[i]);
            history.Append('\n');
        }

        activeLine.Clear();
        activeLine.Append(fullLines[fullLines.Length - 1]);

        Refresh();
    }

    private IEnumerator TypeRoutine(string message)
    {
        isTyping = true;

        float baseDelay = 1f / Mathf.Max(1f, charsPerSecond);

        for (int i = 0; i < message.Length; i++)
        {
            char c = message[i];
            activeLine.Append(c);
            Refresh();

            float extra = GetPunctuationPause(c);
            yield return new WaitForSeconds(baseDelay + extra);
        }

        isTyping = false;
        typeCo = null;
        Refresh();
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
            '…' => pauseEllipsis,
            _ => 0f
        };
    }

    private IEnumerator CursorBlinkLoop()
    {
        while (true)
        {
            if (!isTyping || blinkWhileTyping)
            {
                cursorOn = !cursorOn;
                Refresh();
            }
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
    }

    private void Refresh()
    {
        if (terminalText == null) return;

        var sb = new StringBuilder();
        sb.Append(history);

        bool hasAnyLine = activeLine.Length > 0;

        if (hasAnyLine) sb.Append(activeLine);

        if (hasAnyLine || showCursorWhenEmpty)
            sb.Append(cursorOn ? cursorChar : " ");

        terminalText.text = sb.ToString();

        if (playbackActive)
            FollowDuringPlayback();
    }

    // ✅ Fix LV1: nếu content ngắn hơn viewport -> pin TOP, còn dài -> pin BOTTOM
    private void FollowDuringPlayback()
    {
        if (scrollRect == null) return;
        if (scrollRect.content == null) return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

        float contentH = scrollRect.content.rect.height;
        float viewH = scrollRect.viewport != null ? scrollRect.viewport.rect.height
                                                  : ((RectTransform)scrollRect.transform).rect.height;

        bool contentFits = contentH <= viewH + 1f;

        scrollRect.verticalNormalizedPosition = contentFits ? 1f : 0f;
    }
}
