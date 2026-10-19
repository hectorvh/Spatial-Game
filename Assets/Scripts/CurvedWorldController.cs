using UnityEngine;

/// <summary>
/// Drives the curved-world shader globals each frame: a constant horizon drop plus a slow
/// left/right sway that reads as the track turning. Nothing physical moves.
/// </summary>
public class CurvedWorldController : MonoBehaviour
{
    public static CurvedWorldController Instance { get; private set; }

    [Header("Bend")]
    [Tooltip("Downward bend per metre squared ahead of the bunny.")]
    public float horizonDrop = 0.0009f;
    [Tooltip("Peak sideways bend per metre squared; the sign flips over each sway period.")]
    public float maxTurnStrength = 0.00125f;
    [Tooltip("Seconds for one full left-right-left sway. 80s is 30% of the original 24s rate.")]
    public float turnPeriod = 80f;
    public bool enableTurns = true;

    PlayerControllerX player;
    float swayTime;

    public static CurvedWorldController Ensure()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject controllerObject = new GameObject("CurvedWorldController");
        return controllerObject.AddComponent<CurvedWorldController>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        player = FindAnyObjectByType<PlayerControllerX>();
        Push();
    }

    void Update()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerControllerX>();
        }

        // Scaled time: the sway freezes with the pause menu and on the finish screen.
        if (player == null || !player.isGameOver)
        {
            swayTime += Time.deltaTime;
        }

        Push();
    }

    void Push()
    {
        float originZ = player != null ? player.transform.position.z : 0f;
        float turn = 0f;
        if (enableTurns && turnPeriod > 0.01f)
        {
            turn = maxTurnStrength * Mathf.Sin(2f * Mathf.PI * swayTime / turnPeriod);
        }

        CurvedWorld.SetGlobals(originZ, turn, horizonDrop);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            CurvedWorld.SetGlobals(0f, 0f, 0f);
        }
    }
}
