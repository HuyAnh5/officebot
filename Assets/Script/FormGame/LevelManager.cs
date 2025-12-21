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
    [SerializeField] private bool persistProgress = true; // PlayerPrefs
    [SerializeField] private bool loopOnEnd = false;

    [Header("Debug")]
    [SerializeField] private bool useDebugStartLevel = false;
    [SerializeField, Min(1)] private int debugStartLevelNumber = 1; // 1-based
    [SerializeField] private bool debugResetErrorsOnStart = true;


    private LevelData[] levels;
    private int index;
    private int errors;

    private bool busy;
    private bool pendingCorrect;

    private const string PREF_LEVEL_INDEX = "FORMGAME_LEVEL_INDEX";
    private const string PREF_ERRORS = "FORMGAME_ERRORS";

    private void Awake()
    {
        if (levelsJson == null)
        {
            Debug.LogError("[LevelManager] levelsJson is null.");
            enabled = false;
            return;
        }

        // Load progression (security reasons unlocked)
        SecurityProgression.Load();

        // Load levels
        var list = JsonUtility.FromJson<LevelList>(levelsJson.text);
        levels = list != null ? list.levels : null;

        if (levels == null || levels.Length == 0)
        {
            Debug.LogError("[LevelManager] No levels found in JSON.");
            enabled = false;
            return;
        }

        if (acceptBtn) acceptBtn.onClick.AddListener(() => Commit(true));
        if (rejectBtn) rejectBtn.onClick.AddListener(() => Commit(false));

        if (persistProgress)
        {
            index = Mathf.Clamp(PlayerPrefs.GetInt(PREF_LEVEL_INDEX, 0), 0, levels.Length - 1);
            errors = Mathf.Max(0, PlayerPrefs.GetInt(PREF_ERRORS, 0));
        }
        else
        {
            index = 0;
            errors = 0;
        }
    }

    private void Start()
    {
        int start = index;

        if (useDebugStartLevel)
        {
            start = Mathf.Clamp(debugStartLevelNumber - 1, 0, levels.Length - 1);
            if (debugResetErrorsOnStart) errors = 0;
        }

        LoadLevel(start);
    }


    private void LoadLevel(int i)
    {
        index = Mathf.Clamp(i, 0, levels.Length - 1);
        busy = false;

        if (pageTurn) pageTurn.HideFold();
        if (stamp) stamp.HideInstant();

        var lv = levels[index];

        // Show tamper text if level is marked tampered and has tamperVariant
        string displayedOrder = (lv.tampered && !string.IsNullOrEmpty(lv.tamperVariant))
            ? lv.tamperVariant
            : lv.order;

        // Security details shown only after FLAG is checked:
        // - If level explicitly specifies available details -> use those
        // - Otherwise use globally unlocked details
        string[] securityDetailsToShow = BuildSecurityDetailsToShow(lv);

        // NOTE: This requires FormView.Render(LevelData, string, string[]) signature
        form.Render(lv, displayedOrder, securityDetailsToShow);
        form.SetLocked(false);

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

        // Không tiết lộ đúng/sai trong run
        hudText.text = $"Level: {index + 1}/{levels.Length}";
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
        PlayerPrefs.Save();
    }

    public void ResetProgress()
    {
        errors = 0;
        index = 0;

        SecurityProgression.ClearAll();

        SaveProgress();
        LoadLevel(0);
    }



    private void Commit(bool accept)
    {
        if (busy) return;
        busy = true;

        SetStampButtonsInteractable(false);
        StartCoroutine(CommitRoutine(accept));
    }

    private IEnumerator CommitRoutine(bool accept)
    {
        if (stamp) yield return stamp.Play(accept);

        form.SetLocked(true);
        pendingCorrect = Evaluate(levels[index], accept);

        // Only after stamp -> show fold
        if (pageTurn != null)
            pageTurn.ShowFold(() => StartCoroutine(AfterFoldClicked()));
        else
            StartCoroutine(AfterFoldClicked());
    }

    private IEnumerator AfterFoldClicked()
    {
        if (pageTurn) pageTurn.HideFold();
        if (pageTurn) yield return pageTurn.TurnPageAnim();

        // No retry: just record the mistake silently and move on
        if (!pendingCorrect)
        {
            errors++;
            SaveProgress();
        }

        // Progression should follow page order (not correctness)
        var lv = levels[index];
        SecurityProgression.UnlockMany(lv.introduceSecurityDetails);

        int next = index + 1;

        if (next >= levels.Length)
        {
            if (loopOnEnd)
                next = 0;
            else
                next = levels.Length - 1; // TODO: replace with Ending screen later
        }

        LoadLevel(next);
    }


    private bool Evaluate(LevelData lv, bool accept)
    {
        string puzzle = (lv.puzzleType ?? "").Trim().ToUpperInvariant();

        // ===== 1) Decide expected stamp (JSON override first) =====
        bool hasExpectedStamp = !string.IsNullOrEmpty(lv.expectedStamp);
        bool expectedAccept;

        if (hasExpectedStamp)
        {
            expectedAccept = string.Equals(lv.expectedStamp, "ACCEPT", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            // Backward-compatible default behavior (your current rules)
            if (lv.hasComplianceCheck)
            {
                expectedAccept = true; // old behavior: compliance must be ACCEPT
            }
            else if (puzzle == "RULE_PAGE" || puzzle == "RULE_SET" || puzzle == "NOTICE")
            {
                bool issuerIsAI = string.Equals(lv.issuerType, "AI", StringComparison.OrdinalIgnoreCase);
                expectedAccept = !issuerIsAI;
            }
            else if (lv.canBeTampered && lv.tampered)
            {
                expectedAccept = false;
            }
            else
            {
                expectedAccept = true;
            }
        }

        if (accept != expectedAccept) return false;

        // ===== 2) Compliance checkbox (pure JSON) =====
        if (lv.hasComplianceCheck)
        {
            if (form.complianceBox == null) return false;
            return form.complianceBox.IsOn == lv.complianceMustBeOn;
        }

        // ===== 3) FLAG requirement (JSON override via flagRule) =====
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
                // AUTO (backward compatible):
                // - RULE_PAGE + issuer AI => FLAG ON
                // - tampered => FLAG ON
                // - clean => FLAG OFF
                bool issuerIsAI = string.Equals(lv.issuerType, "AI", StringComparison.OrdinalIgnoreCase);
                if ((puzzle == "RULE_PAGE" || puzzle == "RULE_SET" || puzzle == "NOTICE") && issuerIsAI)
                    requireFlag = true;
                else
                    requireFlag = lv.tampered;
            }

            if (enforce && form.flagTampered.IsOn != requireFlag) return false;
        }

        // ===== 4) Required security detail reasons (pure JSON) =====
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

        // ===== 5) Option count rules (pure JSON) =====
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

            // ===== 6) Expected option (JSON override first) =====
            if (!string.IsNullOrEmpty(lv.expectedOptionId))
            {
                if (selCount != 1) return false;

                string pickedId = form.GetFirstSelectedOptionId();
                if (!string.Equals(pickedId, lv.expectedOptionId, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            else
            {
                // Backward-compatible default rail scoring ONLY when expectedOptionId is not provided
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
