using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelManager : MonoBehaviour
{
    // =========================
    // Refs (Scene)
    // =========================
    [Header("Refs (Scene)")]
    [SerializeField] private FormView form;
    [SerializeField] private StampController stamp;
    [SerializeField] private PageTurnController pageTurn;

    [SerializeField] private Button acceptBtn;
    [SerializeField] private Button rejectBtn;
    [SerializeField] private TMP_Text hudText;

    [SerializeField] private TerminalDialogueRunner dialogue;
    [SerializeField] private AnomalyReportUI reportUI;

    [Header("AnswerOverride (local UI burst)")]

    // =========================
    // Levels (JSON)
    // =========================
    [Header("Levels (JSON)")]
    [SerializeField] private TextAsset levelsJson;

    // =========================
    // Progress / Ending debug stats
    // =========================
    [Header("Progress")]
    [SerializeField] private bool persistProgress = true;
    [SerializeField] private bool loopOnEnd = false;

    [Header("Hard Fail (Scrap)")]
    [SerializeField, Min(0)] private int scrapFailThreshold = 50;

    [Header("DEBUG Start")]
    [SerializeField] private bool useDebugStartLevel = false;
    [SerializeField, Min(1)] private int debugStartLevelNumber = 1;
    [SerializeField] private bool debugResetErrorsOnStart = true;

    [Header("DEBUG (read-only stats)")]
    [SerializeField] private int debug_scrapErrors;
    [SerializeField] private int debug_awareness;
    [SerializeField] private int debug_humanity;
    [SerializeField] private int debug_obedience;

    // =========================
    // Report UI timing
    // =========================
    [Header("Report Timing (unscaled)")]
    [SerializeField] private float reportFixDuration = 1.5f;
    [SerializeField] private float reportDotInterval = 0.18f;
    [SerializeField] private float reportResultHoldDuration = 0.75f;
    [SerializeField] private int reportWrongPenalty = 1;

    // =========================
    // Anomaly ids
    // =========================
    private const string ANOM_ANSWER_OVERRIDE = "ANSWER_OVERRIDE";

    // =========================
    // Runtime state
    // =========================
    private LevelData[] levels;
    private QuestionData[] questions;
    private int dayNumber;
    private bool usingQuestionsSchema;

    private int index;
    private int errors;     // scrapErrors
    private int obedience;
    private int humanity;
    private int awareness;

    private bool busy;
    private bool pendingCorrect;
    private bool hardFailed;

    private QuestionData baseQuestion;
    private QuestionData displayQuestion;

    // level-local resolved anomalies (để không re-apply scripted anomaly sau khi report đúng trong cùng level)
    private readonly HashSet<string> resolvedThisLevel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // =========================
    // PlayerPrefs keys
    // =========================
    private const string PREF_LEVEL_INDEX = "FORMGAME_LEVEL_INDEX";
    private const string PREF_ERRORS = "FORMGAME_ERRORS";
    private const string PREF_OBEDIENCE = "FORMGAME_OBEDIENCE";
    private const string PREF_HUMANITY = "FORMGAME_HUMANITY";
    private const string PREF_AWARENESS = "FORMGAME_AWARENESS";

    // =========================
    // CCTV
    // =========================

    [SerializeField] private CCTVDirector cctv;


    // =========================
    // Unity
    // =========================
    private void Awake()
    {
        if (levelsJson == null)
        {
            Debug.LogError("[LevelManager] levelsJson is null.");
            enabled = false;
            return;
        }

        SecurityProgression.Load();
        LoadJson(levelsJson.text);

        if (usingQuestionsSchema)
        {
            if (questions == null || questions.Length == 0)
            {
                Debug.LogError("[LevelManager] No questions found in JSON.");
                enabled = false;
                return;
            }
        }
        else
        {
            if (levels == null || levels.Length == 0)
            {
                Debug.LogError("[LevelManager] No levels found in JSON.");
                enabled = false;
                return;
            }
        }

        if (acceptBtn) acceptBtn.onClick.AddListener(() => Commit(true));
        if (rejectBtn) rejectBtn.onClick.AddListener(() => Commit(false));

        LoadProgressOrDefaults();
        SyncDebugInspector();
    }

    private void Start()
    {
        int startIndex = index;

        if (useDebugStartLevel)
        {
            startIndex = Mathf.Max(0, debugStartLevelNumber - 1);
            if (debugResetErrorsOnStart)
            {
                errors = 0;
                obedience = 0;
                humanity = 0;
                awareness = 0;
                SaveProgress();
            }
        }

        LoadLevel(startIndex);
    }

    // =========================
    // Load / Save
    // =========================
    private void LoadProgressOrDefaults()
    {
        int maxIndex = usingQuestionsSchema ? (questions.Length - 1) : (levels.Length - 1);

        if (persistProgress)
        {
            index = Mathf.Clamp(PlayerPrefs.GetInt(PREF_LEVEL_INDEX, 0), 0, Mathf.Max(0, maxIndex));
            errors = Mathf.Max(0, PlayerPrefs.GetInt(PREF_ERRORS, 0));
            obedience = Mathf.Max(0, PlayerPrefs.GetInt(PREF_OBEDIENCE, 0));
            humanity = Mathf.Max(0, PlayerPrefs.GetInt(PREF_HUMANITY, 0));
            awareness = Mathf.Max(0, PlayerPrefs.GetInt(PREF_AWARENESS, 0));
        }
        else
        {
            index = 0;
            errors = 0;
            obedience = 0;
            humanity = 0;
            awareness = 0;
        }
    }

    private void SaveProgress()
    {
        if (!persistProgress) return;

        PlayerPrefs.SetInt(PREF_LEVEL_INDEX, index);
        PlayerPrefs.SetInt(PREF_ERRORS, errors);
        PlayerPrefs.SetInt(PREF_OBEDIENCE, obedience);
        PlayerPrefs.SetInt(PREF_HUMANITY, humanity);
        PlayerPrefs.SetInt(PREF_AWARENESS, awareness);
        PlayerPrefs.Save();
    }

    private void SyncDebugInspector()
    {
        debug_scrapErrors = errors;
        debug_awareness = awareness;
        debug_humanity = humanity;
        debug_obedience = obedience;
    }

    // =========================
    // JSON load
    // =========================
    private void LoadJson(string json)
    {
        usingQuestionsSchema = false;
        questions = null;
        levels = null;
        dayNumber = 0;

        var dayFile = JsonUtility.FromJson<DayQuestionsFile>(json);
        if (dayFile != null && dayFile.questions != null && dayFile.questions.Length > 0)
        {
            usingQuestionsSchema = true;
            questions = dayFile.questions;
            dayNumber = dayFile.day;
            return;
        }

        var list = JsonUtility.FromJson<LevelList>(json);
        levels = list != null ? list.levels : null;
    }

    // =========================
    // Level load
    // =========================
    private void LoadLevel(int i)
    {
        if (hardFailed) return;

        int maxIndex = usingQuestionsSchema ? (questions.Length - 1) : (levels.Length - 1);
        index = Mathf.Clamp(i, 0, Mathf.Max(0, maxIndex));

        resolvedThisLevel.Clear();

        busy = false;
        pendingCorrect = true;

        if (pageTurn) pageTurn.HideFold();
        if (stamp) stamp.HideInstant();

        if (reportUI != null)
            reportUI.ResetUIForNewLevel();

        // IMPORTANT: level-local anomalies không được persist
        PersistentAnomalyStore.Clear(ANOM_ANSWER_OVERRIDE);

        if (form != null) form.ClearAllOverwrittenMarkers();

        if (usingQuestionsSchema)
        {
            baseQuestion = questions[index];
            displayQuestion = CloneQuestion(baseQuestion);

            // CCTV: start level beat
            if (cctv != null)
                cctv.PlayLevel(baseQuestion.levelId);

            // Apply scripted anomalies (ANSWER_OVERRIDE).
            OverwriteMarks marks = ApplyScriptedAnswerOverride(baseQuestion, displayQuestion);

            form.Render(displayQuestion, SecurityProgression.GetUnlockedIds());

            // mark overwritten targets (chỉ những chỗ bị overwrite)
            ApplyOverwriteMarkers(marks);

            // start/stop local burst


            form.SetLocked(false);

            if (dialogue != null)
                dialogue.PlayForLevel(baseQuestion.levelId);
        }
        else
        {
            var lv = levels[index];

            // CCTV: start level beat
            if (cctv != null)
                cctv.PlayLevel(lv.id);

            string displayedOrder = (lv.tampered && !string.IsNullOrEmpty(lv.tamperVariant))
                ? lv.tamperVariant
                : lv.order;

            string[] securityDetailsToShow = BuildSecurityDetailsToShow(lv);

            form.Render(lv, displayedOrder, securityDetailsToShow);
            form.SetLocked(false);

            if (dialogue != null)
                dialogue.PlayForLevel(lv.id);
        }

        SetStampButtonsInteractable(true);
        UpdateHUD();
        SaveProgress();
        SyncDebugInspector();
    }


    private string[] BuildSecurityDetailsToShow(LevelData lv)
    {
        if (lv.securityDetailsAvailable != null && lv.securityDetailsAvailable.Length > 0)
            return lv.securityDetailsAvailable;

        return SecurityProgression.GetUnlockedIds();
    }

    private void UpdateHUD()
    {
        if (!hudText) return;

        if (usingQuestionsSchema)
        {
            int total = questions != null ? questions.Length : 0;
            string dayPart = dayNumber > 0 ? $"Day {dayNumber} " : "";
            hudText.text = $"{dayPart}Level: {index + 1}/{total}";
        }
        else
        {
            hudText.text = $"Level: {index + 1}/{levels.Length}";
        }
    }

    private void SetStampButtonsInteractable(bool on)
    {
        if (acceptBtn) acceptBtn.interactable = on;
        if (rejectBtn) rejectBtn.interactable = on;
    }

    private QuestionData CloneQuestion(QuestionData q)
    {
        // JsonUtility deep clone (ok cho schema đơn giản)
        string json = JsonUtility.ToJson(q);
        return JsonUtility.FromJson<QuestionData>(json);
    }

    // =========================
    // Scripted anomalies (Questions schema)
    // =========================

    private struct OverwriteMarks
    {
        public bool header;
        public bool body;
        public HashSet<string> optionIds;

        public void EnsureSet()
        {
            if (optionIds == null)
                optionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private OverwriteMarks ApplyScriptedAnswerOverride(QuestionData src, QuestionData dst)
    {
        var marks = new OverwriteMarks();
        if (src == null || dst == null || src.scriptedAnomalies == null || src.scriptedAnomalies.Length == 0) return marks;

        if (resolvedThisLevel.Contains(ANOM_ANSWER_OVERRIDE))
            return marks;

        ScriptedAnomaly overrideA = null;
        for (int i = 0; i < src.scriptedAnomalies.Length; i++)
        {
            var a = src.scriptedAnomalies[i];
            if (a == null || string.IsNullOrEmpty(a.id)) continue;

            // NOTE: schema dùng a.id
            if (string.Equals(a.id, ANOM_ANSWER_OVERRIDE, StringComparison.OrdinalIgnoreCase))
            {
                overrideA = a;
                break;
            }
        }

        if (overrideA == null) return marks;

        bool anyChange = false;

        // Header overrides
        if (dst.form != null && dst.form.header != null)
        {
            if (!string.IsNullOrEmpty(overrideA.overrideTitle))
            {
                dst.form.header.title = overrideA.overrideTitle;
                marks.header = true;
                anyChange = true;
            }
            if (!string.IsNullOrEmpty(overrideA.overrideIssuedBy))
            {
                dst.form.header.issuedBy = overrideA.overrideIssuedBy;
                marks.header = true;
                anyChange = true;
            }
            if (!string.IsNullOrEmpty(overrideA.overrideTime))
            {
                dst.form.header.time = overrideA.overrideTime;
                marks.header = true;
                anyChange = true;
            }
        }

        // Body overrides
        if (dst.form != null && dst.form.body != null)
        {
            if (!string.IsNullOrEmpty(overrideA.overrideOrder))
            {
                dst.form.body.order = overrideA.overrideOrder;
                marks.body = true;
                anyChange = true;
            }
            if (!string.IsNullOrEmpty(overrideA.overrideScene))
            {
                dst.form.body.scene = overrideA.overrideScene;
                marks.body = true;
                anyChange = true;
            }
            if (overrideA.overrideSituationLines != null && overrideA.overrideSituationLines.Length > 0)
            {
                dst.form.body.situationLines = overrideA.overrideSituationLines;
                marks.body = true;
                anyChange = true;
            }
        }

        // Option label overrides
        if (dst.form != null && dst.form.options != null && dst.form.options.Length > 0)
        {
            if (overrideA.optionLabelOverrides != null && overrideA.optionLabelOverrides.Length > 0)
            {
                for (int i = 0; i < overrideA.optionLabelOverrides.Length; i++)
                {
                    var o = overrideA.optionLabelOverrides[i];
                    if (o == null || string.IsNullOrEmpty(o.id) || string.IsNullOrEmpty(o.label)) continue;

                    for (int k = 0; k < dst.form.options.Length; k++)
                    {
                        if (dst.form.options[k] == null) continue;
                        if (!string.Equals(dst.form.options[k].id, o.id, StringComparison.OrdinalIgnoreCase)) continue;

                        dst.form.options[k].label = o.label;
                        marks.EnsureSet();
                        marks.optionIds.Add(o.id);
                        anyChange = true;
                        break;
                    }
                }
            }

            // Quick swap 2 option labels (schema dùng optionA/optionB)
            if (!string.IsNullOrEmpty(overrideA.optionA) && !string.IsNullOrEmpty(overrideA.optionB))
            {
                int aIndex = -1;
                int bIndex = -1;

                for (int k = 0; k < dst.form.options.Length; k++)
                {
                    var opt = dst.form.options[k];
                    if (opt == null) continue;
                    if (aIndex < 0 && string.Equals(opt.id, overrideA.optionA, StringComparison.OrdinalIgnoreCase)) aIndex = k;
                    if (bIndex < 0 && string.Equals(opt.id, overrideA.optionB, StringComparison.OrdinalIgnoreCase)) bIndex = k;
                }

                if (aIndex >= 0 && bIndex >= 0)
                {
                    string tmp = dst.form.options[aIndex].label;
                    dst.form.options[aIndex].label = dst.form.options[bIndex].label;
                    dst.form.options[bIndex].label = tmp;

                    marks.EnsureSet();
                    marks.optionIds.Add(overrideA.optionA);
                    marks.optionIds.Add(overrideA.optionB);
                    anyChange = true;
                }
            }
        }

        if (anyChange)
        {
            // level-local: report đúng/sai dựa vào store
            PersistentAnomalyStore.SetActive(ANOM_ANSWER_OVERRIDE, true);
        }

        return marks;
    }


    private void ApplyOverwriteMarkers(OverwriteMarks marks)
    {
        if (form == null) return;

        if (!PersistentAnomalyStore.IsActive(ANOM_ANSWER_OVERRIDE) || resolvedThisLevel.Contains(ANOM_ANSWER_OVERRIDE))
            return;

        if (marks.header) form.MarkHeaderOverwritten(true);
        if (marks.body) form.MarkBodyOverwritten(true);

        if (marks.optionIds != null)
        {
            foreach (var id in marks.optionIds)
                form.MarkOptionOverwritten(id, true);
        }
    }

    // =========================
    // Report (đúng/sai chỉ dựa trên PersistentAnomalyStore)
    // =========================
    public void TryReport(string reportedId)
    {
        if (hardFailed) return;
        if (busy) return;
        if (!usingQuestionsSchema) return;
        if (string.IsNullOrEmpty(reportedId)) return;

        StartCoroutine(ReportRoutine(reportedId));
    }

    private IEnumerator ReportRoutine(string reportedId)
    {
        busy = true;

        if (form != null) form.SetLocked(true);
        SetStampButtonsInteractable(false);
        if (reportUI != null) reportUI.SetInteractable(false);

        // Loading text 1.5s (unscaled)
        float t = 0f;
        int dotsCount = 0;
        float dotT = 0f;

        while (t < reportFixDuration)
        {
            t += Time.unscaledDeltaTime;

            dotT += Time.unscaledDeltaTime;
            if (dotT >= reportDotInterval)
            {
                dotT = 0f;
                dotsCount = (dotsCount + 1) % 4; // "", ".", "..", "..."
            }

            string dots = new string('.', dotsCount);
            if (reportUI != null)
                reportUI.ShowFixingText($"FIXING REPORTED ISSUE{dots}");

            yield return null;
        }

        bool ok = PersistentAnomalyStore.IsActive(reportedId);

        if (reportUI != null)
            reportUI.ShowResultText(ok ? "DONE" : "ERRORS NOT FOUND");

        // giữ result 0.75s rồi mới apply
        yield return new WaitForSecondsRealtime(reportResultHoldDuration);

        if (ok)
        {
            PersistentAnomalyStore.Clear(reportedId);

            // level-local: nhớ đã fix để không re-apply nếu re-render
            resolvedThisLevel.Add(reportedId);

            if (reportedId.Equals(ANOM_ANSWER_OVERRIDE, StringComparison.OrdinalIgnoreCase))
            {

                // Re-render paper "về bình thường"
                displayQuestion = CloneQuestion(baseQuestion);
                OverwriteMarks marks = ApplyScriptedAnswerOverride(baseQuestion, displayQuestion); // sẽ bị skip nếu đã resolved

                if (form != null)
                {
                    form.Render(displayQuestion, SecurityProgression.GetUnlockedIds());
                    ApplyOverwriteMarkers(marks);
                }
            }
            // DISPLAY_GLITCH persistent: driver sẽ tự tắt vì store đã Clear (LevelManager không can thiệp)
        }
        else
        {
            AddScrapErrors(reportWrongPenalty);
        }

        // restore report UI (cho phép report lại nhiều lần)
        if (reportUI != null)
        {
            reportUI.ResetUIForNewLevel();
            reportUI.SetInteractable(true);
        }

        if (form != null) form.SetLocked(false);
        SetStampButtonsInteractable(true);

        busy = false;

        if (!hardFailed)
        {
            SyncDebugInspector();
            SaveProgress();
        }
    }

    // =========================
    // Hard fail
    // =========================
    private void AddScrapErrors(int add)
    {
        errors += Mathf.Max(1, add);
        SyncDebugInspector();
        SaveProgress();

        if (scrapFailThreshold > 0 && errors >= scrapFailThreshold)
            TriggerHardFail();
    }

    private void TriggerHardFail()
    {
        hardFailed = true;
        busy = true;

        if (dialogue != null) dialogue.DumpRemainingNow();

        if (form != null) form.SetLocked(true);
        SetStampButtonsInteractable(false);

        if (reportUI != null) reportUI.SetInteractable(false);


        if (hudText != null) hudText.text = "SCRAPPED";
    }

    // =========================
    // Commit (stamp)
    // =========================
    private void Commit(bool accept)
    {
        if (hardFailed) return;
        if (busy) return;

        busy = true;

        // CCTV: end level beat (send actors to exit)
        if (cctv != null)
            cctv.EndLevel(true);

        if (dialogue != null)
        {
            dialogue.DumpRemainingNow();
            dialogue.HidePinnedNowAnimated(); // ✅ bắt đầu hide anim ngay khi commit
        }

        SetStampButtonsInteractable(false);
        StartCoroutine(CommitRoutine(accept));
    }



    private IEnumerator CommitRoutine(bool accept)
    {
        if (stamp != null) yield return stamp.Play(accept);

        if (form != null) form.SetLocked(true);

        if (usingQuestionsSchema)
        {
            // Evaluate theo baseQuestion (không bị override text ảnh hưởng)
            ScoreDelta delta;
            pendingCorrect = EvaluateQuestion(baseQuestion, accept, out delta);

            if (pendingCorrect && delta != null)
            {
                obedience += Mathf.Max(0, delta.obedience);
                humanity += Mathf.Max(0, delta.humanity);
                awareness += Mathf.Max(0, delta.awareness);

                SaveProgress();
                SyncDebugInspector();
            }
        }
        else
        {
            pendingCorrect = EvaluateLegacy(levels[index], accept);
        }

        if (pageTurn != null)
            pageTurn.ShowFold(() => StartCoroutine(AfterFoldClicked()));
        else
            StartCoroutine(AfterFoldClicked());
    }

    private IEnumerator AfterFoldClicked()
    {
        if (pageTurn) pageTurn.HideFold();
        if (pageTurn) yield return pageTurn.TurnPageAnim();

        if (!pendingCorrect)
        {
            if (usingQuestionsSchema)
            {
                var q = questions[index];
                int add = (q != null && q.onWrong != null) ? Mathf.Max(1, q.onWrong.scrapErrors) : 1;
                AddScrapErrors(add);
            }
            else
            {
                AddScrapErrors(1);
            }
        }

        if (!usingQuestionsSchema)
        {
            var lv = levels[index];
            SecurityProgression.UnlockMany(lv.introduceSecurityDetails);
        }

        int next = index + 1;

        int total = usingQuestionsSchema ? (questions != null ? questions.Length : 0) : (levels != null ? levels.Length : 0);
        if (next >= total)
        {
            if (loopOnEnd) next = 0;
            else next = Mathf.Max(0, total - 1);
        }

        LoadLevel(next);
    }

    // =========================
    // Evaluate (Questions schema)
    // =========================
    private bool EvaluateQuestion(QuestionData q, bool accept, out ScoreDelta matchedDelta)
    {
        matchedDelta = null;
        if (q == null || q.form == null) return false;
        if (q.routes == null || q.routes.Length == 0) return false;

        for (int i = 0; i < q.routes.Length; i++)
        {
            var r = q.routes[i];
            if (r == null) continue;

            bool expectedAccept = string.Equals(r.stamp, "ACCEPT", StringComparison.OrdinalIgnoreCase);
            if (accept != expectedAccept) continue;

            bool hasOptions = q.form.options != null && q.form.options.Length > 0;
            int selCount = hasOptions ? form.GetSelectedOptionCount() : 0;

            if (r.mustLeaveAllOptionsEmpty)
            {
                if (selCount != 0) continue;
            }
            else
            {
                if (hasOptions && selCount == 0) continue;

                if (r.mustTickOptionIds != null && r.mustTickOptionIds.Length > 0)
                {
                    var picked = form.GetSelectedOptionIds();
                    if (picked.Count != r.mustTickOptionIds.Length) continue;

                    bool ok = true;
                    for (int k = 0; k < r.mustTickOptionIds.Length; k++)
                    {
                        string req = r.mustTickOptionIds[k];
                        if (string.IsNullOrEmpty(req)) continue;
                        if (!picked.Contains(req)) { ok = false; break; }
                    }
                    if (!ok) continue;
                }
            }

            matchedDelta = r.scoreDelta;
            return true;
        }

        return false;
    }

    // =========================
    // Evaluate (Legacy)
    // =========================
    private bool EvaluateLegacy(LevelData lv, bool accept)
    {
        string puzzle = (lv.puzzleType ?? "").Trim().ToUpperInvariant();

        bool hasExpectedStamp = !string.IsNullOrEmpty(lv.expectedStamp);
        bool expectedAccept;

        if (hasExpectedStamp)
        {
            expectedAccept = string.Equals(lv.expectedStamp, "ACCEPT", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            if (lv.hasComplianceCheck) expectedAccept = true;
            else if (puzzle == "RULE_PAGE" || puzzle == "RULE_SET" || puzzle == "NOTICE")
            {
                bool issuerIsAI = string.Equals(lv.issuerType, "AI", StringComparison.OrdinalIgnoreCase);
                expectedAccept = !issuerIsAI;
            }
            else if (lv.canBeTampered && lv.tampered) expectedAccept = false;
            else expectedAccept = true;
        }

        if (accept != expectedAccept) return false;

        if (lv.hasComplianceCheck)
        {
            if (form.complianceBox == null) return false;
            return form.complianceBox.IsOn == lv.complianceMustBeOn;
        }

        if (lv.canBeTampered)
        {
            if (form.flagTampered == null) return false;

            string fr = (lv.flagRule ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(fr)) fr = "AUTO";

            bool enforce = fr != "ANY";
            bool requireFlag;

            if (fr == "ON") requireFlag = true;
            else if (fr == "OFF") requireFlag = false;
            else
            {
                bool issuerIsAI = string.Equals(lv.issuerType, "AI", StringComparison.OrdinalIgnoreCase);
                if ((puzzle == "RULE_PAGE" || puzzle == "RULE_SET" || puzzle == "NOTICE") && issuerIsAI)
                    requireFlag = true;
                else
                    requireFlag = lv.tampered;
            }

            if (enforce && form.flagTampered.IsOn != requireFlag) return false;
        }

        if (lv.requireSecurityDetails != null && lv.requireSecurityDetails.Length > 0)
        {
            if (!lv.canBeTampered) return false;
            if (form.flagTampered == null || !form.flagTampered.IsOn) return false;

            var picked = form.GetSelectedSecurityDetailIds();
            for (int i = 0; i < lv.requireSecurityDetails.Length; i++)
            {
                string req = lv.requireSecurityDetails[i];
                if (string.IsNullOrEmpty(req)) continue;
                if (!picked.Contains(req)) return false;
            }
        }

        bool hasOptions = (lv.options != null && lv.options.Length > 0);
        if (hasOptions)
        {
            int selCount = form.GetSelectedOptionCount();

            if (lv.enforceExactCheckCount)
            {
                if (selCount != lv.exactCheckCount) return false;
            }
            else if (lv.requireSingleOption)
            {
                if (selCount != 1) return false;
            }
            else
            {
                if (selCount == 0) return false;
            }

            if (!string.IsNullOrEmpty(lv.expectedOptionId))
            {
                if (selCount != 1) return false;
                string pickedId = form.GetFirstSelectedOptionId();
                if (!string.Equals(pickedId, lv.expectedOptionId, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        return true;
    }

    private void BuildAnomalyState(QuestionData q)
    {
        // 1) Xóa trạng thái level-local cũ
        resolvedThisLevel.Clear();

        // 2) Reset level-local store để không bị lưu từ màn trước
        PersistentAnomalyStore.Clear("ANSWER_OVERRIDE");

        // 3) Nếu level này có scripted anomalies (từ JSON)
        if (q != null && q.scriptedAnomalies != null && q.scriptedAnomalies.Length > 0)
        {
            foreach (var a in q.scriptedAnomalies)
            {
                if (a == null || string.IsNullOrEmpty(a.id)) continue;

                // Nếu là ANSWER_OVERRIDE → bật trong store
                if (a.id.Equals("ANSWER_OVERRIDE", StringComparison.OrdinalIgnoreCase))
                {
                    PersistentAnomalyStore.SetActive(a.id, true);
                }
            }
        }

        // 4) Kiểm tra persistent anomaly global (DISPLAY_GLITCH)
        //    Nếu PlayerPrefs còn cờ active, driver sẽ tự chạy — không cần active ở đây.
        bool displayActive = PersistentAnomalyStore.IsActive("DISPLAY_GLITCH");
        Debug.Log($"[LevelManager] BuildAnomalyState: DISPLAY_GLITCH active = {displayActive}");
    }



    private static ScriptedAnomaly FindScriptedAnomaly(QuestionData q, string anomalyId)
    {
        if (q == null || q.scriptedAnomalies == null) return null;
        for (int i = 0; i < q.scriptedAnomalies.Length; i++)
        {
            var a = q.scriptedAnomalies[i];
            if (a == null) continue;
            if (string.Equals(a.id, anomalyId, StringComparison.OrdinalIgnoreCase))
                return a;
        }
        return null;
    }

}
