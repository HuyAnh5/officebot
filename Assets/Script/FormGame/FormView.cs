using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class FormView : MonoBehaviour
{
    [Header("Text (single)")]
    public TMP_Text headerText; // ONE TMP for header (title + id + issuedBy + time)
    public TMP_Text bodyText;   // ONE TMP for body (order/scene/situation/meta...)

    [Header("Select (dynamic options)")]
    public GameObject selectGroup;
    public Transform optionsContainer;
    public CheckboxUI optionRowPrefab;

    [Header("Security")]
    public GameObject securityGroup;
    public CheckboxUI flagTampered;

    public GameObject securityDetailsGroup; // hidden until FLAG
    public Transform securityDetailsContainer;
    public CheckboxUI securityDetailRowPrefab;

    [Header("Compliance")]
    public GameObject complianceGroup;
    public CheckboxUI complianceBox;

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
                    // Clear detail ticks when turning FLAG off
                    for (int i = 0; i < spawnedSecurityDetails.Count; i++)
                        spawnedSecurityDetails[i].SetOn(false, notify: false);
                }
                else
                {
                    // Rebuild details when turning FLAG on
                    RenderSecurityDetails(currentSecurityIds);
                }
            };
        }
    }

    // Backward-compatible overload (2 args)
    public void Render(LevelData level, string displayedOrder)
    {
        Render(level, displayedOrder, null);
    }

    // Signature used by LevelManager
    public void Render(LevelData level, string displayedOrder, string[] securityDetailsToShow)
    {
        // -------- Header (ONE TMP) --------
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

        // -------- Body (ONE TMP) --------
        if (bodyText != null)
        {
            bodyText.text = BuildBody(level, displayedOrder);
        }

        // -------- Options (dynamic) --------
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

            // If requireSingleOption => radio behavior
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

        // -------- Security --------
        // Current behavior (kept for compatibility with your existing LevelManager):
        // show security when level.canBeTampered OR when any security reason has been unlocked.
        bool hasUnlockedSecurity = SecurityProgression.GetUnlockedIds().Length > 0;
        bool showSecurity = level.canBeTampered || hasUnlockedSecurity;

        if (securityGroup != null) securityGroup.SetActive(showSecurity);

        if (showSecurity && flagTampered != null)
            flagTampered.SetOn(false, notify: false);

        currentSecurityIds = securityDetailsToShow ?? new string[0];

        if (securityDetailsGroup != null) securityDetailsGroup.SetActive(false);
        ClearSecurityDetails();

        // -------- Compliance --------
        if (complianceGroup != null) complianceGroup.SetActive(level.hasComplianceCheck);

        if (level.hasComplianceCheck && complianceBox != null)
        {
            // id + label same is ok
            complianceBox.Set(level.complianceLabel, level.complianceLabel);
            complianceBox.SetOn(false, notify: false);
        }
    }

    private string BuildBody(LevelData level, string displayedOrder)
    {
        var sb = new StringBuilder();

        // ORDER (can be empty)
        if (!string.IsNullOrWhiteSpace(displayedOrder))
        {
            sb.AppendLine($"<b>ORDER:</b> {displayedOrder}");
            sb.AppendLine();
        }

        // SCENE
        if (!string.IsNullOrWhiteSpace(level.scene))
        {
            sb.AppendLine($"<b>SCENE:</b> {level.scene}");
            sb.AppendLine();
        }

        // Optional rail info
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

        // Situation lines
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

        // Meta (optional)
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

    public HashSet<string> GetSelectedSecurityDetailIds()
    {
        var set = new HashSet<string>();
        for (int i = 0; i < spawnedSecurityDetails.Count; i++)
            if (spawnedSecurityDetails[i].IsOn) set.Add(spawnedSecurityDetails[i].Id);
        return set;
    }
}
