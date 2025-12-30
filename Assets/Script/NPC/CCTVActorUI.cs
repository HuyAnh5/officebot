using UnityEngine;
using UnityEngine.UI;

public class CCTVActorUI : MonoBehaviour
{
    public enum Role { A, B }
    public enum State { None, WalkToX, Hold }

    [Header("Runtime")]
    public Role role;
    public float speed;                 // UI units per second
    public float arriveEpsilon = 2f;     // pixels (anchored units)

    [Header("Avoidance")]
    public float avoidDistance = 40f;    // pixels
    public float avoidYOffset = 18f;     // pixels
    public float yLerp = 14f;
    public int avoidSign = 1;            // +1 / -1

    RectTransform _rt;
    Image _img;

    CCTVActorUI _other;

    float _baseY;
    float _targetY;

    State _state = State.None;
    float _targetX;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _img = GetComponent<Image>();
    }

    public void Init(Role r, Color color, float spd, CCTVActorUI other)
    {
        role = r;
        speed = spd;
        _other = other;
        if (_img) _img.color = color;

        _baseY = _rt.anchoredPosition.y;
        _targetY = _baseY;
    }

    public void TeleportTo(RectTransform anchor)
    {
        if (!anchor) return;
        _rt.anchoredPosition = anchor.anchoredPosition;
        _baseY = _rt.anchoredPosition.y;
        _targetY = _baseY;
    }

    public void WalkToX(float targetX)
    {
        _targetX = targetX;
        _state = State.WalkToX;
    }

    public void Hold()
    {
        _state = State.Hold;
    }

    void Update()
    {
        UpdateAvoidanceY();
        ApplyY();

        if (_state == State.WalkToX)
        {
            Vector2 p = _rt.anchoredPosition;
            float dir = Mathf.Sign(_targetX - p.x);
            p.x += dir * speed * Time.deltaTime;
            _rt.anchoredPosition = p;

            // optional flip
            if (_img) _img.rectTransform.localScale = new Vector3(dir >= 0 ? 1 : -1, 1, 1);

            if (Mathf.Abs(_rt.anchoredPosition.x - _targetX) <= arriveEpsilon)
            {
                _rt.anchoredPosition = new Vector2(_targetX, _rt.anchoredPosition.y);
                _state = State.Hold; // director decides next; default hold on arrival
            }
        }
    }

    void UpdateAvoidanceY()
    {
        _targetY = _baseY;
        if (_other == null) return;

        float dx = Mathf.Abs(_rt.anchoredPosition.x - _other._rt.anchoredPosition.x);
        if (dx <= avoidDistance)
            _targetY = _baseY + avoidSign * avoidYOffset;
    }

    void ApplyY()
    {
        Vector2 p = _rt.anchoredPosition;
        p.y = Mathf.Lerp(p.y, _targetY, 1f - Mathf.Exp(-yLerp * Time.deltaTime));
        _rt.anchoredPosition = p;
    }
}
