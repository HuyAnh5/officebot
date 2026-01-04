using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StampController : MonoBehaviour
{
    [Header("Commit Stamp (ACCEPT / REJECT)")]
    [SerializeField] private Image commitStampImage; // Image nằm trên PaperPanel
    [SerializeField] private Sprite acceptedSprite;
    [SerializeField] private Sprite rejectedSprite;

    [Header("Form Error Stamp (TOGGLE)")]
    [SerializeField] private Image formErrorStampImage; // Image khác nằm trên PaperPanel
    [SerializeField] private Sprite formErrorSprite;

    [Header("Anim")]
    [SerializeField] private float popTime = 0.12f;
    [SerializeField] private float popScale = 1.4f;

    private bool _formErrorOn;
    private Coroutine _formErrorCo;

    public bool IsFormErrorStamped => _formErrorOn;

    // Gọi mỗi khi load level mới
    public void HideInstant()
    {
        HideImageInstant(commitStampImage);
        HideImageInstant(formErrorStampImage);
        _formErrorOn = false;

        if (_formErrorCo != null) StopCoroutine(_formErrorCo);
        _formErrorCo = null;
    }

    // Giữ nguyên API cũ để LevelManager không phải đổi nhiều
    public IEnumerator Play(bool accepted)
    {
        if (commitStampImage == null) yield break;

        commitStampImage.sprite = accepted ? acceptedSprite : rejectedSprite;
        yield return PlayStampAnim(commitStampImage);
    }

    // Button gọi hàm này (không commit)
    public void ToggleFormError()
    {
        SetFormError(!_formErrorOn, animate: true);
    }

    public void SetFormError(bool on, bool animate)
    {
        _formErrorOn = on;

        if (formErrorStampImage == null) return;

        if (_formErrorCo != null) StopCoroutine(_formErrorCo);
        _formErrorCo = null;

        if (!on)
        {
            HideImageInstant(formErrorStampImage);
            return;
        }

        formErrorStampImage.sprite = formErrorSprite;

        if (animate)
            _formErrorCo = StartCoroutine(PlayStampAnim(formErrorStampImage));
        else
            ShowImageInstant(formErrorStampImage);
    }

    private IEnumerator PlayStampAnim(Image img)
    {
        if (img == null) yield break;

        // start
        img.transform.localScale = Vector3.one * popScale;
        var c = img.color; c.a = 0f; img.color = c;

        float t = 0f;
        while (t < popTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / popTime);

            img.transform.localScale = Vector3.Lerp(Vector3.one * popScale, Vector3.one, k);
            c.a = Mathf.Lerp(0f, 1f, k);
            img.color = c;

            yield return null;
        }

        img.transform.localScale = Vector3.one;
        c.a = 1f;
        img.color = c;
    }

    private static void HideImageInstant(Image img)
    {
        if (img == null) return;
        var c = img.color; c.a = 0f; img.color = c;
        img.transform.localScale = Vector3.one;
    }

    private static void ShowImageInstant(Image img)
    {
        if (img == null) return;
        var c = img.color; c.a = 1f; img.color = c;
        img.transform.localScale = Vector3.one;
    }
}
