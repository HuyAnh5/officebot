using UnityEngine;

public static class SecurityCatalog
{
    // id -> label hiển thị (bạn có thể đổi text sau)
    public static string LabelFor(string id)
    {
        switch (id)
        {
            case "GLYPH_SWAP": return "GLYPH SWAP (▲/□/•)";
            case "ISSUED_BY_AI": return "ISSUED BY AI";
            case "FONT_GLITCH": return "FONT GLITCH";
            case "MORSE": return "MORSE / DOTS";
            case "UNCANNY_IMAGE": return "UNCANNY IMAGE";
            case "SCRIBBLES": return "CROSSED OUT / SCRIBBLES";
            default: return id; // fallback
        }
    }
}
