using UnityEngine;

/// <summary>
/// Visual-only lean and side nudge for lane changes. Lives on SwayPivot, an un-animated
/// child of the player root that holds the Animator, Armature, mesh and Cannon.
/// The root, Rigidbody and Collider never move from this script, so the hitbox and
/// lane logic are untouched. The sway is an offset on top of SwayPivot's base pose.
/// </summary>
public class RunnerSway : MonoBehaviour
{
    [Tooltip("Meters the model slides toward the steer direction.")]
    [SerializeField] float sideOffset = 0.3f;
    [Tooltip("Degrees of roll into the steer direction.")]
    [SerializeField] float leanAngle = 15f;
    [Tooltip("Smoothing rate. Higher reaches the target faster.")]
    [SerializeField] float speed = 6f;
    [Tooltip("Seconds the lean is held after SetSide(0), so short lane changes still show it.")]
    [SerializeField] float holdTime = 0.15f;
    [Tooltip("Triples offset and angle to make the effect obvious while testing.")]
    [SerializeField] bool debugExaggerate;

    Vector3 basePosition;
    Quaternion baseRotation;
    float target;
    float current;
    float releaseTimer = -1f;

    /// <summary>Current smoothed side value, -1 (left) to 1 (right).</summary>
    public float Current => current;

    void Awake()
    {
        basePosition = transform.localPosition;
        baseRotation = transform.localRotation;
    }

    /// <summary>
    /// -1 leans left, 1 leans right. 0 releases back to upright after holdTime.
    /// Repeated 0 calls do not restart the hold.
    /// </summary>
    public void SetSide(float dir)
    {
        dir = Mathf.Clamp(dir, -1f, 1f);
        if (dir != 0f)
        {
            target = dir;
            releaseTimer = -1f;
            return;
        }

        if (target != 0f && releaseTimer < 0f)
        {
            releaseTimer = holdTime;
        }
    }

    /// <summary>Snaps straight back to the base pose with no leftover tilt.</summary>
    public void ResetSway()
    {
        target = 0f;
        current = 0f;
        releaseTimer = -1f;
        transform.localPosition = basePosition;
        transform.localRotation = baseRotation;
    }

    void OnDisable()
    {
        ResetSway();
    }

    // LateUpdate runs after the Animator, and SwayPivot has no animation curves.
    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        if (releaseTimer >= 0f)
        {
            releaseTimer -= dt;
            if (releaseTimer < 0f)
            {
                target = 0f;
            }
        }

        current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * dt));

        float scale = debugExaggerate ? 3f : 1f;
        transform.localPosition = basePosition + Vector3.right * (current * sideOffset * scale);
        transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, -current * leanAngle * scale);
    }
}
