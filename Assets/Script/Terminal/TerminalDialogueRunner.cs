using System;
using System.Collections;
using UnityEngine;

public class TerminalDialogueRunner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TerminalView terminal;
    [SerializeField] private TextAsset dialogueJson;

    [Header("Timing (fallback nếu JSON thiếu)")]
    [SerializeField] private float defaultStartDelayMin = 0f;
    [SerializeField] private float defaultStartDelayMax = 0.8f;
    [SerializeField] private float postLineHoldMin = 0.15f;
    [SerializeField] private float postLineHoldMax = 0.35f;
    [SerializeField] private float interLineGapMin = 0.35f;
    [SerializeField] private float interLineGapMax = 0.9f;

    private DialogueFile file;

    private Coroutine playCo;
    private Coroutine idleCo;

    private Segment currentSegment;
    private int currentLineIndex = -1; // line đang xử lý; -1 = chưa bắt đầu line nào
    private string currentPrefix = "";
    private string currentText = "";

    bool orderShown = false;

    private void Awake()
    {
        if (dialogueJson != null && !string.IsNullOrEmpty(dialogueJson.text))
            file = JsonUtility.FromJson<DialogueFile>(dialogueJson.text);
    }

    public void PlayForLevel(string levelId)
    {
        if (terminal == null || file == null || file.segments == null) return;

        StopAllCoroutinesSafe();
        terminal.HidePinnedOrderImmediate();

        orderShown = false; // ✅ reset mỗi level

        currentSegment = FindSegment(levelId);
        currentLineIndex = -1;
        currentPrefix = "";
        currentText = "";

        if (currentSegment == null)
        {
            terminal.EnsureCursorVisible();
            return;
        }

        playCo = StartCoroutine(PlaySegmentRoutine(currentSegment));

        // ❌ bỏ dòng này (API cũ, không liên quan pinned mới)
        // terminal.HidePinnedOrder();
    }


    public void DumpRemainingNow()
    {
        if (terminal == null || currentSegment == null) return;

        StopAllCoroutinesSafe();

        // Nếu đang typing thì ép hoàn tất line hiện tại
        if (terminal.IsTyping && currentLineIndex >= 0)
            terminal.ForceCompleteActiveLine(currentPrefix + currentText);

        int from = (currentLineIndex < 0) ? 0 : Mathf.Clamp(currentLineIndex + 1, 0, currentSegment.lines.Length);
        string[] remain = BuildRemainingFullLines(currentSegment, from);

        if (remain.Length > 0)
        {
            // AppendLines... sẽ tự commit active line hiện tại 1 lần, rồi dump phần còn lại
            terminal.AppendLinesInstantKeepingLastActive(remain);
        }
        // Nếu remain rỗng: giữ nguyên active line => `|` vẫn nhấp nháy ở cuối câu cuối

        terminal.SetPlaybackActive(false);

        currentSegment = null;
        currentLineIndex = -1;

        // không start idle nag nữa vì level đã commit xong (sắp chuyển level)
    }

    private IEnumerator PlaySegmentRoutine(Segment seg)
    {
        terminal.SetPlaybackActive(true);

        // Segment cooldown: chỉ “đợi”, không tạo prefix mới
        float startDelay = seg.startDelaySec != null
            ? seg.startDelaySec.Random()
            : UnityEngine.Random.Range(defaultStartDelayMin, defaultStartDelayMax);

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        for (int i = 0; i < seg.lines.Length; i++)
        {
            currentLineIndex = i;

            var line = seg.lines[i];
            currentPrefix = BuildPrefix(line);
            currentText = line.text;

            // ✅ Prefix chỉ xuất hiện đúng lúc line bắt đầu (sau delay/gap)
            terminal.BeginLine(currentPrefix);
            terminal.TypeMessage(currentText);

            // chờ gõ xong thật
            yield return new WaitUntil(() => !terminal.IsTyping);

            // giữ ở cuối câu (cursor nhấp nháy ở đây)
            yield return new WaitForSeconds(UnityEngine.Random.Range(postLineHoldMin, postLineHoldMax));

            if (!orderShown && seg.terminalOrder != null && seg.terminalOrder.showAfterLineIndex >= 0 && i == seg.terminalOrder.showAfterLineIndex)
            {
                terminal.ShowPinnedOrder(seg.terminalOrder.text, seg.terminalOrder.autoHideAfterSec);
                orderShown = true;
            }
            // chờ giữa 2 câu, vẫn đứng ở cuối câu hiện tại (không tạo prefix mới)
            if (i < seg.lines.Length - 1)
                yield return new WaitForSeconds(UnityEngine.Random.Range(interLineGapMin, interLineGapMax));
        }



        // Segment kết thúc: giữ cursor ở cuối câu cuối (không commit thêm, không tạo prefix mới)
        terminal.SetPlaybackActive(false);

        if (!orderShown && seg.terminalOrder != null && seg.terminalOrder.showAfterLineIndex < 0)
        {
            terminal.ShowPinnedOrder(seg.terminalOrder.text, seg.terminalOrder.autoHideAfterSec);
            orderShown = true;
        }

        // ✅ start idle nag sau khi segment xong, nếu JSON có idleNag
        if (file.idleNag != null && file.idleNag.pool != null && file.idleNag.pool.Length > 0)
        {
            idleCo = StartCoroutine(IdleNagLoop());
        }

        playCo = null;

    }

    private IEnumerator IdleNagLoop()
    {
        float first = file.idleNag.startAfterLastLineSec != null ? file.idleNag.startAfterLastLineSec.Random() : 18f;
        yield return new WaitForSeconds(first);

        var pool = file.idleNag.pool;
        int[] bag = BuildShuffledBag(pool.Length);
        int bagIndex = 0;

        while (true)
        {
            if (bagIndex >= bag.Length)
            {
                bag = BuildShuffledBag(pool.Length);
                bagIndex = 0;
            }

            int pick = bag[bagIndex++];
            var idle = pool[pick];

            // ✅ Bật follow khi idle nag chuẩn bị in line
            terminal.SetPlaybackActive(true);

            string prefix = BuildIdlePrefix();
            terminal.BeginLine(prefix);
            terminal.TypeMessage(idle.text);

            yield return new WaitUntil(() => !terminal.IsTyping);

            // ✅ In xong thì thả scroll cho player
            terminal.SetPlaybackActive(false);

            float repeat = file.idleNag.repeatEverySec != null ? file.idleNag.repeatEverySec.Random() : 18f;
            yield return new WaitForSeconds(repeat);
        }
    }


    private int[] BuildShuffledBag(int n)
    {
        int[] arr = new int[n];
        for (int i = 0; i < n; i++) arr[i] = i;

        // Fisher–Yates
        for (int i = n - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
        return arr;
    }

    private void StopAllCoroutinesSafe()
    {
        if (playCo != null) StopCoroutine(playCo);
        if (idleCo != null) StopCoroutine(idleCo);
        playCo = null;
        idleCo = null;
    }

    private Segment FindSegment(string levelId)
    {
        if (string.IsNullOrWhiteSpace(levelId)) return null;
        string key = levelId.Trim();

        for (int i = 0; i < file.segments.Length; i++)
        {
            var s = file.segments[i];
            if (s == null) continue;
            if (string.Equals((s.levelId ?? "").Trim(), key, StringComparison.OrdinalIgnoreCase))
                return s;
        }
        return null;
    }

    private string[] BuildRemainingFullLines(Segment seg, int fromIndex)
    {
        int n = seg.lines.Length - fromIndex;
        if (n <= 0) return Array.Empty<string>();

        var arr = new string[n];
        for (int i = 0; i < n; i++)
        {
            var l = seg.lines[fromIndex + i];
            arr[i] = BuildPrefix(l) + l.text;
        }
        return arr;
    }

    private string BuildPrefix(Line l)
    {
        string displayName = "???";
        if (file.presentation != null &&
            string.Equals(file.presentation.displayNameMode, "ANONYMOUS", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(file.presentation.anonymousName))
        {
            displayName = file.presentation.anonymousName;
        }

        return $"[{displayName}] > ";
    }

    private string BuildIdlePrefix()
    {
        string displayName = "???";
        if (file.presentation != null &&
            string.Equals(file.presentation.displayNameMode, "ANONYMOUS", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(file.presentation.anonymousName))
        {
            displayName = file.presentation.anonymousName;
        }

        // timestamp idle cố định để dễ đọc
        return $"[--:--:--] [{displayName}] > ";
    }

    public void HidePinnedNowAnimated()
    {
        if (terminal == null) return;
        if (!terminal.IsPinnedVisible) return;
        terminal.HidePinnedOrderAnimated();
    }

    public IEnumerator HidePinnedBeforeLevelChange()
    {
        if (terminal == null) yield break;

        if (!terminal.IsPinnedVisible)
        {
            terminal.HidePinnedOrderImmediate();
            yield break;
        }

        terminal.HidePinnedOrderAnimated();
        yield return new WaitForSecondsRealtime(terminal.PinnedHideDuration);
    }


    // ===== JSON classes (JsonUtility-friendly) =====
    [Serializable]
    private class DialogueFile
    {
        public int day;
        public Presentation presentation;
        public Segment[] segments;
        public IdleNag idleNag;
    }

    [Serializable]
    private class Presentation
    {
        public string displayNameMode;
        public string anonymousName;
    }

    [Serializable]
    private class Segment
    {
        public string levelId;
        public RangeSec startDelaySec;
        public Line[] lines;

        public TerminalOrder terminalOrder; // NEW
    }

    [Serializable]
    private class TerminalOrder
    {
        public string text;

        // Hiện sau khi chạy xong line index này (0-based). -1 = sau câu cuối (default)
        public int showAfterLineIndex = -1;

        // timeout overlay (giây). <=0 thì dùng mặc định trong TerminalView
        public float autoHideAfterSec = -1f;
    }


    [Serializable]
    private class Line
    {
        public string t;
        public string speaker;
        public string text;
    }

    [Serializable]
    private class IdleNag
    {
        public RangeSec startAfterLastLineSec;
        public RangeSec repeatEverySec;
        public IdleLine[] pool;
    }

    [Serializable]
    private class IdleLine
    {
        public string speaker;
        public string text;
    }

    [Serializable]
    private class RangeSec
    {
        public float min;
        public float max;
        public float Random() => UnityEngine.Random.Range(min, max);
    }
}
