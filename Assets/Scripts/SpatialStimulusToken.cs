using System.Collections;
using UnityEngine;

/// <summary>
/// Phase-1 question token: scrolls toward the bunny, accepts on trigger pickup from
/// any rail, or reports a miss after it passes Z = -10.
/// </summary>
public class SpatialStimulusToken : MonoBehaviour
{
    public const string StimulusTag = "QuestionStimulus";

    public float moveSpeed = 10f;
    public int questionId;
    public int laneIndex;
    public int attemptNumber;
    public float spawnedAtRealtime;

    BoxSpawner spawner;
    PlayerControllerX player;
    bool resolved;

    public void Bind(BoxSpawner owner, int id, int lane, int attempt)
    {
        spawner = owner;
        questionId = id;
        laneIndex = lane;
        attemptNumber = attempt;
        spawnedAtRealtime = Time.realtimeSinceStartup;
        resolved = false;
    }

    void Start()
    {
        player = FindAnyObjectByType<PlayerControllerX>();
        if (spawner == null)
        {
            spawner = FindAnyObjectByType<BoxSpawner>();
        }
    }

    void Update()
    {
        if (resolved)
        {
            return;
        }

        if (player != null && player.isGameOver)
        {
            return;
        }

        transform.Translate(Vector3.back * moveSpeed * Time.deltaTime, Space.World);

        if (transform.position.z < -10f)
        {
            NotifyMissed();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        PlayerControllerX otherPlayer = other.GetComponentInParent<PlayerControllerX>();
        if (otherPlayer != null)
        {
            TryCollect(otherPlayer);
        }
    }

    public void TryCollect(PlayerControllerX collector)
    {
        if (resolved || collector == null || collector.isGameOver)
        {
            return;
        }

        resolved = true;
        DisableTrigger();
        AudioManager.PlayStimulusCollect();

        float reactionTimeMs = (Time.realtimeSinceStartup - spawnedAtRealtime) * 1000f;
        if (spawner != null)
        {
            spawner.NotifyStimulusAccepted(this, reactionTimeMs);
        }

        StartCoroutine(PickupRoutine());
    }

    void NotifyMissed()
    {
        if (resolved)
        {
            return;
        }

        resolved = true;
        int bunnyLane = player != null ? player.currentLane : -1;
        if (spawner != null)
        {
            spawner.NotifyStimulusMissed(this, bunnyLane);
        }

        Destroy(gameObject);
    }

    void DisableTrigger()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
        {
            trigger.enabled = false;
        }
    }

    IEnumerator PickupRoutine()
    {
        PickupEffect.PlayBurst(CurvedWorld.Bend(transform.position));
        yield return PickupEffect.ScaleOut(transform);
        Destroy(gameObject);
    }
}
