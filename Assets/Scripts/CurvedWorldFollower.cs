using UnityEngine;

/// <summary>
/// Moves a non-bendable visual (TMP label) to where the curved shader draws its parent,
/// so answer text stays glued to its box. Collider-free by design.
/// </summary>
public class CurvedWorldFollower : MonoBehaviour
{
    Vector3 baseLocalPosition;

    void Awake()
    {
        baseLocalPosition = transform.localPosition;
    }

    void LateUpdate()
    {
        transform.localPosition = baseLocalPosition;
        Vector3 straight = transform.position;
        transform.position = straight + CurvedWorld.Offset(straight);
    }
}
