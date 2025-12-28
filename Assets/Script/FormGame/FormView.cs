using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FormView : MonoBehaviour
{
    [Header("Text (single)")]
    public TMP_Text headerText;
    public TMP_Text bodyText;

    [Header("Select (dynamic options)")]
    public GameObject selectGroup;
    public Transform optionsContainer;
    public CheckboxUI optionRowPrefab;

    [Header("Security")]
    public GameObject securityGroup;
    public CheckboxUI flagTampered;

    public GameObject securityDetailsGroup;
    public Transform securityDetailsContainer;
    public CheckboxUI securityDetailRowPrefab;

    [Header("Compliance")]
    public GameObject complianceGroup;
    public CheckboxUI complianceBox;

    [Header("AnswerOverride markers (optional)")]
    public UILocalGlitchPulse headerGlitch;
    public UILocalGlitchPulse bodyGlitch;


    private readonly List<CheckboxUI> spawnedOptions = new List<CheckboxUI>();
    private readonly List<CheckboxUI> spawnedSecurityDetails = new List<CheckboxUI>();

    private string[] currentSecurityIds = new string[0];

    private void Awake()
    {
        if (flagTampered != null)
        {
            flagTampered.OnChanged += (_, isOn) =>
            {
                if (securityDetailsGroup != null)
                    securityDetailsGroup.SetActive(isOn);

                if (!isOn)
                {
                    for (int i = 0; i < spawnedSecurityDetails.Count; i++)
                        spawnedSecurityDetails[i].SetOn(false, notify: false);
                }
                else
                {
                    RenderSecurityDetails(currentSecurityIds);
                    StartCoroutine(FixLayoutRoutine());
                }
            };
        }
    }

    // Legacy (2 args)
    public void Render(LevelData level, string displayedOrder)
    {
        Render(level, displayedOrder, null);
    }

    // ==============================
    // New schema render: QuestionData
    // ==============================
    public void Render(QuestionData q, string[] securityDetailsToShow = null)
    {
        if (q == null || q.form == null)
        {
            Debug.LogError("[FormView] Render(QuestionData) received null.");
            return;
        }

        var h = q.form.header;
        var b = q.form.body;

        // Header
        if (headerText != null)
        {
            var sb = new StringBuilder();

            if (h != null && !string.IsNullOrEmpty(h.title))
                sb.AppendLine($"<b><size=130%>{h.title}</size></b>");

            if (!string.IsNullOrEmpty(q.levelId))
                sb.AppendLine($"Form ID: {q.levelId}");

            if (h != null && !string.IsNullOrEmpty(h.issuedBy))
                sb.AppendLine($"Issued by: {h.issuedBy}");

            if (h != null && !string.IsNullOrEmpty(h.time))
                sb.AppendLine($"Time: {h.time}");

            headerText.text = sb.ToString().TrimEnd();
        }

        // Body
        if (bodyText != null)
        {
            string displayedOrder = (b != null) ? b.order : "";
            bodyText.text = BuildBody(b, displayedOrder);
        }

        // Options
        ClearOptions();

        bool hasOptions = q.form.options != null && q.form.options.Length > 0;
        if (selectGroup != null) selectGroup.SetActive(hasOptions);

        if (hasOptions && optionsContainer != null && optionRowPrefab != null)
        {
            for (int i = 0; i < q.form.options.Length; i++)
            {
                var opt = q.form.options[i];
                var row = Instantiate(optionRowPrefab, optionsContainer);
                row.Set(opt.id, opt.label);
                row.SetOn(false, notify: false);
                spawnedOptions.Add(row);
            }

            // Heuristic: nếu mọi route chỉ yêu cầu 0/1 option -> coi như radio
            bool useRadio = ShouldUseRadio(q);
            if (useRadio)
            {
                for (int i = 0; i < spawnedOptions.Count; i++)
                {
                    var row = spawnedOptions[i];
                    row.OnChanged += (who, isOn) =>
                    {
                        if (!isOn) return;
                        for (int k = 0; k < spawnedOptions.Count; k++)
                        {
                            if (spawnedOptions[k] != who)
                                spawnedOptions[k].SetOn(false, notify: false);
                        }
                    };
                }
            }
        }

        // Security
        bool hasUnlockedSecurity = SecurityProgression.GetUnlockedIds().Length > 0;
        bool showSecurity = hasUnlockedSecurity;

        if (securityGroup != null) securityGroup.SetActive(showSecurity);

        if (showSecurity && flagTampered != null)
            flagTampered.SetOn(false, notify: false);

        currentSecurityIds = securityDetailsToShow ?? new string[0];

        if (securityDetailsGroup != null) securityDetailsGroup.SetActive(false);
        ClearSecurityDetails();

        // Compliance (schema mới: tắt)
        if (complianceGroup != null) complianceGroup.SetActive(false);

        if (gameObject.activeInHierarchy)
            StartCoroutine(FixLayoutRoutine());
    }

    // Legacy (3 args)
    public void Render(LevelData level, string displayedOrder, string[] securityDetailsToShow)
    {
        if (headerText != null)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(level.title))
                sb.AppendLine($"<b><size=130%>{level.title}</size></b>");

            if (!string.IsNullOrEmpty(level.id))
                sb.AppendLine($"Form ID: {level.id}");

            if (!string.IsNullOrEmpty(level.issuedBy))
                sb.AppendLine($"Issued by: {level.issuedBy}");

            if (!string.IsNullOrEmpty(level.time))
                sb.AppendLine($"Time: {level.time}");

            headerText.text = sb.ToString().TrimEnd();
        }

        if (bodyText != null)
        {
            bodyText.text = BuildBody(level, displayedOrder);
        }

        ClearOptions();

        bool hasOptions = level.options != null && level.options.Length > 0;
        if (selectGroup != null) selectGroup.SetActive(hasOptions);

        if (hasOptions && optionsContainer != null && optionRowPrefab != null)
        {
            for (int i = 0; i < level.options.Length; i++)
            {
                var opt = level.options[i];
                var row = Instantiate(optionRowPrefab, optionsContainer);
                row.Set(opt.id, opt.label);
                row.SetOn(false, notify: false);
                spawnedOptions.Add(row);
            }

            if (level.requireSingleOption)
            {
                for (int i = 0; i < spawnedOptions.Count; i++)
                {
                    var row = spawnedOptions[i];
                    row.OnChanged += (who, isOn) =>
                    {
                        if (!isOn) return;
                        for (int k = 0; k < spawnedOptions.Count; k++)
                        {
                            if (spawnedOptions[k] != who)
                                spawnedOptions[k].SetOn(false, notify: false);
                        }
                    };
                }
            }
        }

        bool hasUnlockedSecurity = SecurityProgression.GetUnlockedIds().Length > 0;
        bool showSecurity = level.canBeTampered || hasUnlockedSecurity;

        if (securityGroup != null) securityGroup.SetActive(showSecurity);

        if (showSecurity && flagTampered != null)
            flagTampered.SetOn(false, notify: false);

        currentSecurityIds = securityDetailsToShow ?? new string[0];

        if (securityDetailsGroup != null) securityDetailsGroup.SetActive(false);
        ClearSecurityDetails();

        if (complianceGroup != null) complianceGroup.SetActive(level.hasComplianceCheck);

        if (level.hasComplianceCheck && complianceBox != null)
        {
            complianceBox.Set(level.complianceLabel, level.complianceLabel);
            complianceBox.SetOn(false, notify: false);
        }

        if (gameObject.activeInHierarchy)
            StartCoroutine(FixLayoutRoutine());
    }

    private bool ShouldUseRadio(QuestionData q)
    {
        if (q == null || q.form == null || q.form.options == null) return false;
        if (q.form.options.Length <= 1) return false;

        int maxRequired = 0;
        if (q.routes != null)
        {
            for (int i = 0; i < q.routes.Length; i++)
            {
                var r = q.routes[i];
                if (r == null) continue;
                int len = (r.mustTickOptionIds != null) ? r.mustTickOptionIds.Length : 0;
                if (len > maxRequired) maxRequired = len;
            }
        }
        return maxRequired <= 1;
    }

    private IEnumerator FixLayoutRoutine()
    {
        yield return new WaitForEndOfFrame();

        if (bodyText) bodyText.ForceMeshUpdate();
        if (headerText) headerText.ForceMeshUpdate();

        var fitters = GetComponentsInChildren<ContentSizeFitter>(true);

        foreach (var fitter in fitters) fitter.enabled = false;
        Canvas.ForceUpdateCanvases();
        foreach (var fitter in fitters) fitter.enabled = true;

        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());

        if (optionsContainer)
            LayoutRebuilder.ForceRebuildLayoutImmediate(optionsContainer.GetComponent<RectTransform>());
    }


    private string BuildBody(LevelData level, string displayedOrder)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(displayedOrder))
        {
            sb.AppendLine($"<b>ORDER:</b> {displayedOrder}");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(level.scene))
        {
            sb.AppendLine($"<b>SCENE:</b> {level.scene}");
            sb.AppendLine();
        }

        bool hasRail = !string.IsNullOrWhiteSpace(level.leftLabel) || !string.IsNullOrWhiteSpace(level.rightLabel);
        if (hasRail)
        {
            sb.AppendLine("<b>TRACKS:</b>");
            if (!string.IsNullOrWhiteSpace(level.leftLabel))
                sb.AppendLine($"• Left:  [{level.leftCount}] {level.leftLabel}");
            if (!string.IsNullOrWhiteSpace(level.rightLabel))
                sb.AppendLine($"• Right: [{level.rightCount}] {level.rightLabel}");
            sb.AppendLine();
        }

        if (level.situationLines != null && level.situationLines.Length > 0)
        {
            sb.AppendLine("<b>SITUATION:</b>");
            for (int i = 0; i < level.situationLines.Length; i++)
            {
                var line = level.situationLines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                sb.AppendLine($"• {line}");
            }
            sb.AppendLine();
        }

        bool hasMeta = !string.IsNullOrWhiteSpace(level.target) ||
                       !string.IsNullOrWhiteSpace(level.risk) ||
                       !string.IsNullOrWhiteSpace(level.self);

        if (hasMeta)
        {
            sb.AppendLine("<b>META:</b>");
            if (!string.IsNullOrWhiteSpace(level.target)) sb.AppendLine($"• TARGET: {level.target}");
            if (!string.IsNullOrWhiteSpace(level.risk)) sb.AppendLine($"• RISK: {level.risk}");
            if (!string.IsNullOrWhiteSpace(level.self)) sb.AppendLine($"• SELF: {level.self}");
        }

        return sb.ToString().TrimEnd();
    }

    private string BuildBody(QuestionBody body, string displayedOrder)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(displayedOrder))
        {
            sb.AppendLine($"<b>ORDER:</b> {displayedOrder}");
            sb.AppendLine();
        }

        if (body != null && !string.IsNullOrWhiteSpace(body.scene))
        {
            sb.AppendLine($"<b>SCENE:</b> {body.scene}");
            sb.AppendLine();
        }

        if (body != null && body.situationLines != null && body.situationLines.Length > 0)
        {
            sb.AppendLine("<b>SITUATION:</b>");
            for (int i = 0; i < body.situationLines.Length; i++)
            {
                var line = body.situationLines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                sb.AppendLine($"• {line}");
            }
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private void RenderSecurityDetails(string[] ids)
    {
        ClearSecurityDetails();

        if (ids == null || ids.Length == 0) return;
        if (securityDetailsContainer == null || securityDetailRowPrefab == null) return;

        for (int i = 0; i < ids.Length; i++)
        {
            string id = ids[i];
            if (string.IsNullOrWhiteSpace(id)) continue;

            var row = Instantiate(securityDetailRowPrefab, securityDetailsContainer);
            row.Set(id, SecurityCatalog.LabelFor(id));
            row.SetOn(false, notify: false);
            spawnedSecurityDetails.Add(row);
        }
    }

    private void ClearOptions()
    {
        for (int i = 0; i < spawnedOptions.Count; i++)
            if (spawnedOptions[i] != null) Destroy(spawnedOptions[i].gameObject);
        spawnedOptions.Clear();
    }

    private void ClearSecurityDetails()
    {
        for (int i = 0; i < spawnedSecurityDetails.Count; i++)
            if (spawnedSecurityDetails[i] != null) Destroy(spawnedSecurityDetails[i].gameObject);
        spawnedSecurityDetails.Clear();
    }

    public void SetLocked(bool locked)
    {
        for (int i = 0; i < spawnedOptions.Count; i++)
            spawnedOptions[i].SetLocked(locked);

        if (securityGroup != null && securityGroup.activeSelf && flagTampered != null)
            flagTampered.SetLocked(locked);

        for (int i = 0; i < spawnedSecurityDetails.Count; i++)
            spawnedSecurityDetails[i].SetLocked(locked);

        if (complianceGroup != null && complianceGroup.activeSelf && complianceBox != null)
            complianceBox.SetLocked(locked);
    }

    // ====== Methods LevelManager expects ======

    public int GetSelectedOptionCount()
    {
        int c = 0;
        for (int i = 0; i < spawnedOptions.Count; i++)
            if (spawnedOptions[i].IsOn) c++;
        return c;
    }

    public string GetFirstSelectedOptionId()
    {
        for (int i = 0; i < spawnedOptions.Count; i++)
            if (spawnedOptions[i].IsOn) return spawnedOptions[i].Id;
        return "";
    }

    public HashSet<string> GetSelectedOptionIds()
    {
        var set = new HashSet<string>();
        for (int i = 0; i < spawnedOptions.Count; i++)
            if (spawnedOptions[i].IsOn) set.Add(spawnedOptions[i].Id);
        return set;
    }

    public HashSet<string> GetSelectedSecurityDetailIds()
    {
        var set = new HashSet<string>();
        for (int i = 0; i < spawnedSecurityDetails.Count; i++)
            if (spawnedSecurityDetails[i].IsOn) set.Add(spawnedSecurityDetails[i].Id);
        return set;
    }

    public void SetOptionLabel(string optionId, string newLabel)
    {
        if (string.IsNullOrEmpty(optionId)) return;
        for (int i = 0; i < spawnedOptions.Count; i++)
        {
            var row = spawnedOptions[i];
            if (row == null) continue;
            if (row.Id == optionId)
            {
                row.SetLabel(newLabel);
                return;
            }
        }
    }

    public void SetOptionLabels(Dictionary<string, string> labelsById)
    {
        if (labelsById == null) return;
        for (int i = 0; i < spawnedOptions.Count; i++)
        {
            var row = spawnedOptions[i];
            if (row == null) continue;
            if (labelsById.TryGetValue(row.Id, out var label))
                row.SetLabel(label);
        }
    }


    public void ClearAllOverwrittenMarkers()
    {
        MarkHeaderOverwritten(false);
        MarkBodyOverwritten(false);

        for (int i = 0; i < spawnedOptions.Count; i++)
        {
            var row = spawnedOptions[i];
            if (row == null) continue;

            var pulse = row.GetComponentInChildren<UILocalGlitchPulse>(true);
            if (pulse != null) pulse.SetOverwritten(false);
        }
    }

    public void MarkHeaderOverwritten(bool on)
    {
        if (headerGlitch != null)
        {
            headerGlitch.SetOverwritten(on);
            return;
        }

        var pulse = headerText != null ? headerText.GetComponentInChildren<UILocalGlitchPulse>(true) : null;
        if (pulse != null) pulse.SetOverwritten(on);
    }

    public void MarkBodyOverwritten(bool on)
    {
        if (bodyGlitch != null)
        {
            bodyGlitch.SetOverwritten(on);
            return;
        }

        var pulse = bodyText != null ? bodyText.GetComponentInChildren<UILocalGlitchPulse>(true) : null;
        if (pulse != null) pulse.SetOverwritten(on);
    }

    public void MarkOptionOverwritten(string optionId, bool on)
    {
        if (string.IsNullOrEmpty(optionId)) return;

        for (int i = 0; i < spawnedOptions.Count; i++)
        {
            var row = spawnedOptions[i];
            if (row == null) continue;
            if (!string.Equals(row.Id, optionId, StringComparison.OrdinalIgnoreCase)) continue;

            var pulse = row.GetComponentInChildren<UILocalGlitchPulse>(true);
            if (pulse != null) pulse.SetOverwritten(on);
            return;
        }
    }



}
