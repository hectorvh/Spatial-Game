using UnityEngine;

/// <summary>
/// Drives the red sphere along a parametric path relative to the hollow box so a
/// question can test path-motion relations (around / through / over) instead of a
/// fixed arrangement. Runs on both the on-track token and the HUD Box-View copy.
/// </summary>
public class DynamicStimulusAnimator : MonoBehaviour
{
    public StimulusMotionType motionType = StimulusMotionType.Static;
    public float speed = 2.5f;
    public Vector3 amplitude = new Vector3(2.2f, 0f, 1.4f);
    public Vector3 origin;

    float elapsed;

    public static DynamicStimulusAnimator Attach(GameObject target, SpatialConfig config)
    {
        if (target == null || config == null || !config.HasMotion)
        {
            return null;
        }

        DynamicStimulusAnimator animator = target.AddComponent<DynamicStimulusAnimator>();
        animator.motionType = config.ResolvedMotionType;
        animator.speed = Mathf.Abs(config.speed) > 0.001f ? config.speed : 2.5f;
        animator.amplitude = config.amplitude;
        animator.origin = config.sphereLocalPosition;
        animator.Apply(0f);
        return animator;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        Apply(elapsed);
    }

    void Apply(float time)
    {
        transform.localPosition = Evaluate(time);
    }

    Vector3 Evaluate(float time)
    {
        float omega = speed;

        switch (motionType)
        {
            case StimulusMotionType.OrbitHorizontal:
                return origin + new Vector3(
                    amplitude.x * Mathf.Cos(omega * time),
                    0f,
                    amplitude.z * Mathf.Sin(omega * time));

            case StimulusMotionType.TraverseThrough:
                return origin + TraverseOffset(time);

            case StimulusMotionType.OscillateVertical:
                return origin + new Vector3(0f, amplitude.y * Mathf.Sin(omega * time), 0f);

            default:
                return origin;
        }
    }

    /// <summary>Ping-pongs through the open cage along whichever axis has the larger amplitude.</summary>
    Vector3 TraverseOffset(float time)
    {
        bool alongX = Mathf.Abs(amplitude.x) > Mathf.Abs(amplitude.z);
        float span = Mathf.Max(0.01f, Mathf.Abs(alongX ? amplitude.x : amplitude.z) * 2f);
        float offset = Mathf.PingPong(time * speed, span) - span * 0.5f;

        return alongX ? new Vector3(offset, 0f, 0f) : new Vector3(0f, 0f, offset);
    }
}
