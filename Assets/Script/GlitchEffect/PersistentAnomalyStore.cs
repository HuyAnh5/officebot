using UnityEngine;

/// <summary>
/// Giữ trạng thái anomaly global (ví dụ DISPLAY_GLITCH) qua nhiều level.
/// Lưu vị trí, active flag, và mức độ nhiễu (severity 0–1).
/// </summary>
public static class PersistentAnomalyStore
{
    private static string KActive(string id) => $"PERSIST_ACTIVE_{id}";
    private static string KPosX(string id) => $"PERSIST_POSX_{id}";
    private static string KPosY(string id) => $"PERSIST_POSY_{id}";
    private static string KSeverity(string id) => $"PERSIST_SEVERITY_{id}";

    public static bool IsActive(string id) => PlayerPrefs.GetInt(KActive(id), 0) == 1;

    public static void SetActive(string id, bool on)
    {
        PlayerPrefs.SetInt(KActive(id), on ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static Vector2 GetPos01(string id, Vector2 fallback)
    {
        float x = PlayerPrefs.GetFloat(KPosX(id), fallback.x);
        float y = PlayerPrefs.GetFloat(KPosY(id), fallback.y);
        return new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
    }

    public static void SetPos01(string id, Vector2 pos01)
    {
        PlayerPrefs.SetFloat(KPosX(id), Mathf.Clamp01(pos01.x));
        PlayerPrefs.SetFloat(KPosY(id), Mathf.Clamp01(pos01.y));
        PlayerPrefs.Save();
    }

    public static float GetSeverity01(string id, float fallback = 0f)
    {
        return PlayerPrefs.GetFloat(KSeverity(id), fallback);
    }

    public static void SetSeverity01(string id, float v01)
    {
        PlayerPrefs.SetFloat(KSeverity(id), Mathf.Clamp01(v01));
        PlayerPrefs.Save();
    }

    public static void Clear(string id)
    {
        PlayerPrefs.DeleteKey(KActive(id));
        PlayerPrefs.DeleteKey(KPosX(id));
        PlayerPrefs.DeleteKey(KPosY(id));
        PlayerPrefs.DeleteKey(KSeverity(id));
        PlayerPrefs.Save();
    }

}
