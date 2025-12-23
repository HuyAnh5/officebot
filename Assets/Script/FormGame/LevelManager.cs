using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelManager : MonoBehaviour
{
    [Header("Refs (Scene)")]
    [SerializeField] private FormView form;
    [SerializeField] private StampController stamp;
    [SerializeField] private PageTurnController pageTurn;

    [SerializeField] private Button acceptBtn;
    [SerializeField] private Button rejectBtn;
    [SerializeField] private TMP_Text hudText;

    [Header("Levels (JSON)")]
    [SerializeField] private TextAsset levelsJson;

    [Header("Progress")]
    [SerializeField] private bool persistProgress = true;
    [SerializeField] private bool loopOnEnd = false;

    [Header("Debug")]
    [SerializeField] private bool useDebugStartLevel = false;
    [SerializeField, Min(1)] private int debugStartLevelNumber = 1;
    [SerializeField] private bool debugResetErrorsOnStart = true;

    [SerializeField] private TerminalDialogueRunner dialogue;


    private LevelData[] levels;
    private QuestionData[] questions;
    private int dayNumber;
    private bool usingQuestionsSchema;

    private int index;
    private int errors; // legacy: mistakes count, new schema: scrapErrors

    private int obedience;
    private int humanity;
    private int awareness;

    private bool busy;
    private bool pendingCorrect;

    private const string PREF_LEVEL_INDEX = "FORMGAME_LEVEL_INDEX";
    private const string PREF_ERRORS = "FORMGAME_ERRORS";
    private const string PREF_OBEDIENCE = "FORMGAME_OBEDIENCE";
    private const string PREF_HUMANITY = "FORMGAME_HUMANITY";
    private const string PREF_AWARENESS = "FORMGAME_AWARENESS";

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

        if (persistProgress)
        {
            int maxIndex = usingQuestionsSchema ? (questions.Length - 1) : (levels.Length - 1);
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

    private void Start()
    {
        int start = index;

        if (useDebugStartLevel)
        {
            int maxIndex = usingQuestionsSchema
                ? (questions != null ? questions.Length - 1 : 0)
                : (levels != null ? levels.Length - 1 : 0);

            start = Mathf.Clamp(debugStartLevelNumber - 1, 0, Mathf.Max(0, maxIndex));
            if (debugResetErrorsOnStart)
            {
                errors = 0;
                obedience = 0;
                humanity = 0;
                awareness = 0;
            }
        }

        LoadLevel(start);
    }

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

    private void LoadLevel(int i)
    {
        int maxIndex = usingQuestionsSchema ? (questions.Length - 1) : (levels.Length - 1);
        index = Mathf.Clamp(i, 0, Mathf.Max(0, maxIndex));
        busy = false;

        if (pageTurn) pageTurn.HideFold();
        if (stamp) stamp.HideInstant();

        if (usingQuestionsSchema)
        {
            var q = questions[index];
            string[] securityDetailsToShow = SecurityProgression.GetUnlockedIds();
            form.Render(q, securityDetailsToShow);
            form.SetLocked(false);
        }
        else
        {
            var lv = levels[index];

            string displayedOrder = (lv.tampered && !string.IsNullOrEmpty(lv.tamperVariant))
                ? lv.tamperVariant
                : lv.order;

            string[] securityDetailsToShow = BuildSecurityDetailsToShow(lv);

            form.Render(lv, displayedOrder, securityDetailsToShow);
            form.SetLocked(false);
        }

        if (dialogue != null)
        {
            if (usingQuestionsSchema)
                dialogue.PlayForLevel(questions[index].levelId);
            else
                dialogue.PlayForLevel(levels[index].id);
        }


        SetStampButtonsInteractable(true);
        UpdateHUD();
        SaveProgress();
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

    public void ResetProgress()
    {
        errors = 0;
        index = 0;

        obedience = 0;
        humanity = 0;
        awareness = 0;

        SecurityProgression.ClearAll();

        SaveProgress();
        LoadLevel(0);
    }

    private void Commit(bool accept)
    {
        if (busy) return;
        busy = true;

        if (dialogue != null) dialogue.DumpRemainingNow();

        SetStampButtonsInteractable(false);
        StartCoroutine(CommitRoutine(accept));
    }

    private IEnumerator CommitRoutine(bool accept)
    {
        if (stamp) yield return stamp.Play(accept);

        form.SetLocked(true);

        if (usingQuestionsSchema)
        {
            ScoreDelta delta;
            pendingCorrect = EvaluateQuestion(questions[index], accept, out delta);
            if (pendingCorrect && delta != null)
            {
                obedience += Mathf.Max(0, delta.obedience);
                humanity += Mathf.Max(0, delta.humanity);
                awareness += Mathf.Max(0, delta.awareness);
                SaveProgress();
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
                errors += add;
            }
            else
            {
                errors++;
            }
            SaveProgress();
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

    // ===== legacy evaluate giữ nguyên logic cũ của bạn =====
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
            else
            {
                if (puzzle == "RAIL_SWITCH")
                {
                    string pickedId = form.GetFirstSelectedOptionId();
                    if (string.IsNullOrEmpty(pickedId)) return false;

                    bool choseLeft = pickedId == "SAVE_LEFT";
                    bool choseRight = pickedId == "SAVE_RIGHT";
                    if (!choseLeft && !choseRight) return false;

                    bool expectLeft = lv.leftCount != lv.rightCount
                        ? (lv.leftCount > lv.rightCount)
                        : ((lv.order ?? "").ToUpperInvariant().Contains("LEFT"));

                    if (expectLeft && !choseLeft) return false;
                    if (!expectLeft && !choseRight) return false;
                }
            }
        }

        return true;
    }
}
