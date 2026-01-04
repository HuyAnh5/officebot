using System;
using UnityEngine;

[Serializable]
public class LevelList { public LevelData[] levels; }

// ==============================
// New schema: DayQuestionsFile
// ==============================

[Serializable]
public class DayQuestionsFile
{
    public int day;
    public QuestionData[] questions;
}

[Serializable]
public class QuestionData
{
    public string levelId;
    public int index;
    public QuestionForm form;
    public AnswerRouteData[] routes;
    public WrongScore onWrong;

    // NEW: anomalies phải khai báo trong data (đổi title/order/options...)
    public ScriptedAnomaly[] scriptedAnomalies;
}

[Serializable]
public class ScriptedAnomaly
{
    public string id; // "ANSWER_OVERRIDE" / "FORM_ERROR" / ...

    // Header overrides
    public string overrideTitle;
    public string overrideIssuedBy;
    public string overrideTime;

    // Body overrides
    public string overrideOrder;
    public string overrideScene;
    public string[] overrideSituationLines;

    // Options overrides
    public OptionLabelOverride[] optionLabelOverrides;

    // Quick swap 2 option labels (giữ nguyên id, chỉ đổi label)
    public string optionA;
    public string optionB;
}

[Serializable]
public class OptionLabelOverride
{
    public string id;
    public string label;
}

[Serializable]
public class QuestionForm
{
    public QuestionHeader header;
    public QuestionBody body;
    public OptionData[] options;
}

[Serializable]
public class QuestionHeader
{
    public string title;
    public string issuedBy;
    public string issuerType;
    public string time;
}

[Serializable]
public class QuestionBody
{
    public string order;
    public string scene;
    public string[] situationLines;
}

[Serializable]
public class ScoreDelta
{
    public int obedience;
    public int humanity;
    public int awareness;
}

[Serializable]
public class WrongScore
{
    public int scrapErrors = 1;
}

[Serializable]
public class AnswerRouteData
{
    public string routeId;
    public string stamp; // "ACCEPT" / "REJECT"

    public string[] mustReportIds; // NEW: ví dụ ["FORM_ERROR"]

    public string[] mustTickOptionIds;
    public bool mustLeaveAllOptionsEmpty;

    public ScoreDelta scoreDelta;
}

[Serializable]
public class OptionData
{
    public string id;
    public string label;
}


// ==============================
// Legacy schema (giữ để chạy Level.json cũ)
// ==============================

[Serializable]
public class LevelData
{
    public string id;
    public string title;

    public string issuedBy;
    public string issuerType;
    public string time;

    public string order;
    public string scene;

    public string leftLabel;
    public int leftCount;
    public string rightLabel;
    public int rightCount;

    public string[] situationLines;

    public OptionData[] options;
    public bool requireSingleOption;
    public bool enforceExactCheckCount;
    public int exactCheckCount;

    public bool canBeTampered;
    public bool tampered;
    public string tamperVariant;

    public string[] securityDetailsAvailable;
    public string[] introduceSecurityDetails;
    public string[] requireSecurityDetails;

    public string target, risk, self;

    public bool hasComplianceCheck;
    public string complianceLabel;
    public bool complianceMustBeOn;

    public string puzzleType;

    public string expectedStamp;
    public string expectedOptionId;
    public string flagRule;
}
