using UnityEngine;

public class CCTVDirectorUI : MonoBehaviour
{
    [Header("Anchors")]
    public RectTransform spawnLeft;
    public RectTransform spawnRight;
    public RectTransform stopMid;
    public RectTransform despawnLeft;
    public RectTransform despawnRight; // optional

    [Header("Prefab")]
    public CCTVActorUI actorPrefab;
    public RectTransform actorsRoot;

    [Header("Speeds")]
    public float speedA = 140f; // pixels/sec
    public float speedB = 170f;

    [Header("Colors")]
    public Color colorA = new Color(0.2f, 0.9f, 1f, 1f);
    public Color colorB = new Color(1f, 0.4f, 0.2f, 1f);

    [Header("Avoidance")]
    public float avoidDistance = 40f;
    public float avoidYOffset = 18f;

    CCTVActorUI _a;
    CCTVActorUI _b;
    int _levelIndex = 0;

    // Debug test
    public bool debugKeys = true;
    public KeyCode nextLevelKey = KeyCode.N;

    void Start()
    {
        StartLevel(0);
    }

    void Update()
    {
        if (debugKeys && Input.GetKeyDown(nextLevelKey))
        {
            _levelIndex++;
            StartLevel(_levelIndex);
        }

        // despawn checks
        if (_a != null && _a.GetComponent<RectTransform>().anchoredPosition.x <= despawnLeft.anchoredPosition.x)
            Destroy(_a.gameObject);

        if (_b != null && _b.GetComponent<RectTransform>().anchoredPosition.x <= despawnLeft.anchoredPosition.x)
            Destroy(_b.gameObject);
    }

    public void StartLevel(int levelIndex)
    {
        _levelIndex = levelIndex;

        // 1) Spawn A every level: Right -> Left exit
        SpawnA();

        // 2) B behavior depends on level:
        // Level 0 (or first time): spawn Left -> StopMid (hold)
        // Next levels: from its current position -> move Left to despawn
        if (_b == null)
        {
            SpawnB_MoveToMid();
        }
        else
        {
            // From wherever it is, go left and despawn
            _b.WalkToX(despawnLeft.anchoredPosition.x);
        }
    }

    void SpawnA()
    {
        if (_a != null) Destroy(_a.gameObject);

        _a = Instantiate(actorPrefab, actorsRoot ? actorsRoot : transform);
        _a.TeleportTo(spawnRight);
        _a.avoidDistance = avoidDistance;
        _a.avoidYOffset = avoidYOffset;
        _a.avoidSign = +1;
        _a.Init(CCTVActorUI.Role.A, colorA, speedA, _b);

        _a.WalkToX(despawnLeft.anchoredPosition.x);

        // refresh other link for B
        if (_b != null)
            _b.Init(CCTVActorUI.Role.B, colorB, speedB, _a);
    }

    void SpawnB_MoveToMid()
    {
        _b = Instantiate(actorPrefab, actorsRoot ? actorsRoot : transform);
        _b.TeleportTo(spawnLeft);
        _b.avoidDistance = avoidDistance;
        _b.avoidYOffset = avoidYOffset;
        _b.avoidSign = -1;
        _b.Init(CCTVActorUI.Role.B, colorB, speedB, _a);

        _b.WalkToX(stopMid.anchoredPosition.x); // on arrival it holds
    }

    // Call from LevelManager when player ACCEPT/REJECT commits (optional)
    public void OnStampCommitted()
    {
        // If you want: end A immediately on stamp (so A is strictly "per level")
        if (_a != null)
        {
            Destroy(_a.gameObject);
            _a = null;
        }
        // B stays where it is; next StartLevel will send it left.
    }
}
