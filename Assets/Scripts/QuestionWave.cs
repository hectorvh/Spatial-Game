using System.Collections;
using UnityEngine;

public class QuestionWave : MonoBehaviour
{
    public float moveSpeed = 5.4f;
    public float fallStartY = 15f;
    public float fallEndY = 1f;
    public float approachZ = 27f;
    public string questionText;

    private PlayerControllerX playerScript;
    bool isResolving;
    bool hasLanded;

    void Start()
    {
        playerScript = FindAnyObjectByType<PlayerControllerX>();
        GameHUD.Ensure().RegisterQuestionWave(this);

        Vector3 start = transform.position;
        start.y = fallStartY;
        start.z = approachZ;
        transform.position = start;
        hasLanded = start.y <= fallEndY;
    }

    void Update()
    {
        if (isResolving)
        {
            return;
        }

        if (playerScript != null && playerScript.isGameOver)
        {
            return;
        }

        if (!hasLanded)
        {
            FallTowardTrack();
            return;
        }

        transform.Translate(Vector3.back * moveSpeed * Time.deltaTime, Space.World);

        if (transform.position.z < -10.0f)
        {
            Destroy(gameObject);
        }
    }

    void FallTowardTrack()
    {
        transform.Translate(Vector3.down * moveSpeed * Time.deltaTime, Space.World);

        Vector3 position = transform.position;
        position.z = approachZ;
        if (position.y <= fallEndY)
        {
            position.y = fallEndY;
            hasLanded = true;
        }

        transform.position = position;
    }

    public void ResolveCorrect()
    {
        ResolveCorrect(transform.position);
    }

    public void ResolveCorrect(Vector3 burstPosition)
    {
        if (isResolving)
        {
            return;
        }

        isResolving = true;
        DisableInteraction();
        FindAnyObjectByType<BoxSpawner>()?.NotifyLastQuestionResolved();
        StartCoroutine(AcceptRoutine(burstPosition));
    }

    IEnumerator AcceptRoutine(Vector3 burstPosition)
    {
        yield return PickupEffect.ScaleOut(transform);
        Destroy(gameObject);
    }

    void DisableInteraction()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }

        TargetBox[] boxes = GetComponentsInChildren<TargetBox>(true);
        for (int i = 0; i < boxes.Length; i++)
        {
            if (boxes[i] != null)
            {
                boxes[i].enabled = false;
            }
        }
    }

    void OnDestroy()
    {
        if (GameHUD.Instance != null)
        {
            GameHUD.Instance.UnregisterQuestionWave(this);
        }

        BoxSpawner spawner = FindAnyObjectByType<BoxSpawner>();
        if (spawner != null)
        {
            spawner.NotifyWaveEnded(this);
        }
    }
}
