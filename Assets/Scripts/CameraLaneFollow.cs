using UnityEngine;

/// <summary>
/// Slides the camera on X with the bunny's lane: left, center, or right.
/// Y and Z stay fixed so the runner framing does not change.
/// </summary>
public class CameraLaneFollow : MonoBehaviour
{
    public static CameraLaneFollow Instance { get; private set; }

    [Header("Camera X per lane")]
    public float leftX = -3.67f;
    public float centerX = -2.17f;
    public float rightX = -0.67f;

    PlayerControllerX player;
    float sceneCenterX;
    bool hasSceneCenter;

    public static CameraLaneFollow Ensure()
    {
        if (Instance != null)
        {
            return Instance;
        }

        Camera cam = FindAnyObjectByType<Camera>();
        if (cam == null)
        {
            Debug.LogWarning("CameraLaneFollow: no Camera found in the scene.");
            return null;
        }

        CameraLaneFollow follow = cam.GetComponent<CameraLaneFollow>();
        if (follow == null)
        {
            follow = cam.gameObject.AddComponent<CameraLaneFollow>();
        }

        return follow;
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        player = FindAnyObjectByType<PlayerControllerX>();
        sceneCenterX = transform.position.x;
        hasSceneCenter = true;
    }

    void LateUpdate()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerControllerX>();
            if (player == null)
            {
                return;
            }
        }

        Vector3 pos = transform.position;
        pos.x = TargetX(player.transform.position.x);
        transform.position = pos;
    }

    float TargetX(float playerX)
    {
        float[] rails = player != null && player.railsX != null && player.railsX.Length >= 3
            ? player.railsX
            : new[] { -5.10f, -2.10f, 0.90f };

        float leftRail = rails[0];
        float centerRail = rails[1];
        float rightRail = rails[rails.Length - 1];

        float mapped = playerX <= centerRail
            ? Mathf.Lerp(leftX, centerX, Mathf.InverseLerp(leftRail, centerRail, playerX))
            : Mathf.Lerp(centerX, rightX, Mathf.InverseLerp(centerRail, rightRail, playerX));

        if (hasSceneCenter)
        {
            return mapped + (sceneCenterX - centerX);
        }

        return mapped;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
