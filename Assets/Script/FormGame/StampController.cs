using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StampController : MonoBehaviour
{
    [SerializeField] private Image stampImage; // nằm trên PaperPanel
    [SerializeField] private Sprite acceptedSprite;
    [SerializeField] private Sprite rejectedSprite;

    public void HideInstant()
    {
        var c = stampImage.color; c.a = 0f; stampImage.color = c;
        stampImage.transform.localScale = Vector3.one;
    }

    public IEnumerator Play(bool accepted)
    {
        stampImage.sprite = accepted ? acceptedSprite : rejectedSprite;

        // start
        stampImage.transform.localScale = Vector3.one * 1.4f;
        var c = stampImage.color; c.a = 0f; stampImage.color = c;

        // “đập dấu”
        float t = 0f;
        while (t < 0.12f)
        {
            t += Time.unscaledDeltaTime;
            float k = t / 0.12f;
            stampImage.transform.localScale = Vector3.Lerp(Vector3.one * 1.4f, Vector3.one, k);
            c.a = Mathf.Lerp(0f, 1f, k);
            stampImage.color = c;
            yield return null;
        }

        stampImage.transform.localScale = Vector3.one;
        c.a = 1f;
        stampImage.color = c;
    }
}
