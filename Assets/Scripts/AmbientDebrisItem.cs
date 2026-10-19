using UnityEngine;

public enum AmbientDebrisKind
{
    Asteroid,
    HullPanel,
    Antenna,
    StarCluster
}

/// <summary>
/// Pooled background dressing. Has no collider and stays Untagged, so it never
/// counts as a hit, miss or crash. elementId is for future recall protocols.
/// </summary>
public class AmbientDebrisItem : MonoBehaviour
{
    public string elementId;
    public AmbientDebrisKind kind;
    public Vector3 velocity;
    public Vector3 spinAxis = Vector3.up;
    public float spinSpeed;
}
