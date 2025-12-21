using System;
using UnityEngine;

[Serializable]
public class LevelList { public LevelData[] levels; }

[Serializable]
public class OptionData
{
    public string id;     // ex: "SAVE_LEFT", "CALL_HUMAN"
    public string label;  // ex: "SAVE LEFT", "CALL HUMAN"
}

[Serializable]
public class LevelData
{
    public string id;
    public string title;

    // Issuer (rule page yêu cầu: HUMAN -> accept, AI -> flag+reject)
    public string issuedBy;   // ex: "Human Ops", "AI Ops"
    public string issuerType; // "HUMAN" or "AI"
    public string time;       // "09:14" (optional)

    // Order
    public string order;

    // SCENE: viết luôn trong 1 dòng "SCENE: ...."
    public string scene;      // ex: "A TRAIN IS APPROACHING. VISIBILITY LOW."

    // Optional rail switch (vẫn giữ để tương thích)
    public string leftLabel;
    public int leftCount;
    public string rightLabel;
    public int rightCount;

    // Situation lines (0..n)
    public string[] situationLines; // ex: ["DOOR STATUS: OPEN", "TIMER: 00:45"]

    // Select options (0..n)
    public OptionData[] options;
    public bool requireSingleOption;     // radio
    public bool enforceExactCheckCount;  // nếu true -> exactCheckCount
    public int exactCheckCount;

    // Security + tamper
    public bool canBeTampered;
    public bool tampered;
    public string tamperVariant;

    // Security details (IDs)
    // - securityDetailsAvailable: nếu có -> chỉ hiện các mục này khi FLAG bật
    // - nếu rỗng -> hiện theo progression (đã unlock)
    public string[] securityDetailsAvailable;

    // Những mục được "giới thiệu" sau khi hoàn thành level -> unlock cho các level sau
    public string[] introduceSecurityDetails;

    // Nếu muốn bắt buộc tick đúng các mục này (sau này)
    public string[] requireSecurityDetails;

    // Meta
    public string target, risk, self;

    // Tutorial compliance
    public bool hasComplianceCheck;
    public string complianceLabel;
    public bool complianceMustBeOn;

    // Puzzle type: "RAIL_SWITCH", "INCIDENT", "RULE_PAGE", ...
    public string puzzleType;

    // JSON-driven answer overrides (optional)
    // If empty/null -> fallback to current default logic.
    public string expectedStamp;     // "ACCEPT" or "REJECT"
    public string expectedOptionId;  // e.g. "SAVE_LEFT"
    public string flagRule;          // "AUTO" | "ON" | "OFF" | "ANY"


}
