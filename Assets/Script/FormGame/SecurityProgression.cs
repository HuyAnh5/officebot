using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class SecurityProgression
{
    // Unlocked "tamper reasons" (e.g. FONT_GLITCH, MORSE, ...)
    private const string KEY = "SECURITY_UNLOCKS";

    // Unlocked "FLAG: TAMPERED ORDER" mechanic (show SecurityGroup permanently after first introduction)
    private const string KEY_FLAG = "SECURITY_FLAG_UNLOCKED";

    private static readonly HashSet<string> unlocked = new HashSet<string>();
    private static bool flagUnlocked;

    public static void Load()
    {
        unlocked.Clear();

        // Load FLAG unlock
        flagUnlocked = PlayerPrefs.GetInt(KEY_FLAG, 0) == 1;

        // Load reasons
        string raw = PlayerPrefs.GetString(KEY, "");
        if (string.IsNullOrEmpty(raw)) return;

        foreach (var s in raw.Split(';'))
        {
            var t = s.Trim();
            if (!string.IsNullOrEmpty(t)) unlocked.Add(t);
        }
    }

    public static void Save()
    {
        // Save reasons only (FLAG is saved separately)
        string raw = string.Join(";", unlocked.OrderBy(x => x).ToArray());
        PlayerPrefs.SetString(KEY, raw);
        PlayerPrefs.Save();
    }

    // -------------------------
    // FLAG mechanic progression
    // -------------------------
    public static bool IsFlagUnlocked() => flagUnlocked;

    public static void UnlockFlag()
    {
        if (flagUnlocked) return;
        flagUnlocked = true;
        PlayerPrefs.SetInt(KEY_FLAG, 1);
        PlayerPrefs.Save();
    }

    // -------------------------
    // Reasons progression
    // -------------------------
    public static bool IsUnlocked(string id) => unlocked.Contains(id);

    public static string[] GetUnlockedIds() => unlocked.OrderBy(x => x).ToArray();

    public static void UnlockMany(string[] ids)
    {
        if (ids == null || ids.Length == 0) return;

        bool changed = false;
        for (int i = 0; i < ids.Length; i++)
        {
            var id = (ids[i] ?? "").Trim();
            if (id.Length == 0) continue;
            if (unlocked.Add(id)) changed = true;
        }

        if (changed) Save();
    }

    public static void ClearAll()
    {
        unlocked.Clear();
        flagUnlocked = false;

        PlayerPrefs.DeleteKey(KEY);
        PlayerPrefs.DeleteKey(KEY_FLAG);
        PlayerPrefs.Save();
    }
}
