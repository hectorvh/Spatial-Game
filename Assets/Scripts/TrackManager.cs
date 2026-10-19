using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class TrackManager : MonoBehaviour
{
    [Header("Track Settings")]
    public int numberOfTiles = 10;
    public float tileLength = 10.0f;

    [Header("Speed Settings")]
    public float trackSpeed = 10.0f;

    [Header("Position Offsets")]
    public float spawnPosX = -2.1f;
    public float spawnPosY = 1.12f;

    [Header("Tile Scale")]
    public Vector3 tileScale = new Vector3(12f, 0.2f, 10f);

    class TrackSegment
    {
        public GameObject ground;
        public readonly List<GameObject> rails = new List<GameObject>();
    }

    readonly List<TrackSegment> segments = new List<TrackSegment>();
    static readonly Regex NumberedName = new Regex(
        @"^(Ground|Rail)\s*_?-?\s*(\d+)(?:\s*\(\d+\))?$",
        RegexOptions.IgnoreCase);
    PlayerControllerX playerScript;

    void Start()
    {
        playerScript = FindAnyObjectByType<PlayerControllerX>();
        BuildSegments();

        if (segments.Count >= 2 && segments[0].ground != null && segments[1].ground != null)
        {
            tileLength = segments[1].ground.transform.position.z - segments[0].ground.transform.position.z;
        }

        SyncAllRailZ();
        BendTrackVisuals();
    }

    void BendTrackVisuals()
    {
        for (int i = 0; i < segments.Count; i++)
        {
            CurvedWorld.Apply(segments[i].ground);
            for (int r = 0; r < segments[i].rails.Count; r++)
            {
                CurvedWorld.Apply(segments[i].rails[r]);
            }
        }
    }

    void BuildSegments()
    {
        segments.Clear();

        Dictionary<int, GameObject> grounds = new Dictionary<int, GameObject>();
        Dictionary<int, List<GameObject>> rails = new Dictionary<int, List<GameObject>>();

        CollectNumbered(transform, grounds, rails);

        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            CollectNamed(all[i], grounds, rails);
        }

        HashSet<int> indices = new HashSet<int>(grounds.Keys);
        foreach (int railIndex in rails.Keys)
        {
            indices.Add(railIndex);
        }

        List<int> sorted = new List<int>(indices);
        sorted.Sort();

        for (int i = 0; i < sorted.Count; i++)
        {
            int index = sorted[i];
            TrackSegment segment = new TrackSegment();
            grounds.TryGetValue(index, out segment.ground);
            if (rails.TryGetValue(index, out List<GameObject> matchedRails))
            {
                segment.rails.AddRange(matchedRails);
            }

            if (segment.ground != null || segment.rails.Count > 0)
            {
                segments.Add(segment);
            }
        }

        segments.Sort(CompareSegmentsByZ);
    }

    static int CompareSegmentsByZ(TrackSegment a, TrackSegment b)
    {
        return GetSegmentZ(a).CompareTo(GetSegmentZ(b));
    }

    static float GetSegmentZ(TrackSegment segment)
    {
        if (segment == null)
        {
            return 0f;
        }

        if (segment.ground != null)
        {
            return segment.ground.transform.position.z;
        }

        return segment.rails.Count > 0 ? segment.rails[0].transform.position.z : 0f;
    }

    void CollectNumbered(Transform root, Dictionary<int, GameObject> grounds, Dictionary<int, List<GameObject>> rails)
    {
        foreach (Transform child in root)
        {
            if (child != null)
            {
                CollectNamed(child, grounds, rails);
            }
        }
    }

    static void CollectNamed(Transform target, Dictionary<int, GameObject> grounds, Dictionary<int, List<GameObject>> rails)
    {
        if (target == null)
        {
            return;
        }

        Match match = NumberedName.Match(target.name);
        if (!match.Success || !int.TryParse(match.Groups[2].Value, out int index))
        {
            return;
        }

        if (match.Groups[1].Value.Equals("Ground", System.StringComparison.OrdinalIgnoreCase))
        {
            grounds[index] = target.gameObject;
            return;
        }

        if (!rails.TryGetValue(index, out List<GameObject> list))
        {
            list = new List<GameObject>();
            rails[index] = list;
        }

        if (!list.Contains(target.gameObject))
        {
            list.Add(target.gameObject);
        }
    }

    void Update()
    {
        if (playerScript != null && playerScript.isGameOver)
        {
            return;
        }

        float step = trackSpeed * Time.deltaTime;
        for (int i = 0; i < segments.Count; i++)
        {
            TrackSegment segment = segments[i];
            TranslateWorldZ(segment.ground, step);
            for (int r = 0; r < segment.rails.Count; r++)
            {
                GameObject rail = segment.rails[r];
                if (!IsChildOfGround(segment.ground, rail))
                {
                    TranslateWorldZ(rail, step);
                }
            }

            SyncRailZ(segment);
        }

        while (segments.Count > 0 && GetSegmentZ(segments[0]) < -10.0f)
        {
            RelocateFrontToEnd();
        }
    }

    void RelocateFrontToEnd()
    {
        TrackSegment first = segments[0];
        segments.RemoveAt(0);

        if (segments.Count == 0)
        {
            segments.Add(first);
            return;
        }

        float newZ = GetSegmentZ(segments[segments.Count - 1]) + tileLength;
        SetWorldZ(first.ground, newZ);
        for (int r = 0; r < first.rails.Count; r++)
        {
            GameObject rail = first.rails[r];
            if (!IsChildOfGround(first.ground, rail))
            {
                SetWorldZ(rail, newZ);
            }
        }

        SyncRailZ(first);
        segments.Add(first);
    }

    void SyncAllRailZ()
    {
        for (int i = 0; i < segments.Count; i++)
        {
            SyncRailZ(segments[i]);
        }
    }

    static bool IsChildOfGround(GameObject ground, GameObject rail)
    {
        return ground != null &&
               rail != null &&
               rail.transform.IsChildOf(ground.transform);
    }

    static void SyncRailZ(TrackSegment segment)
    {
        if (segment == null)
        {
            return;
        }

        float z = GetSegmentZ(segment);
        for (int i = 0; i < segment.rails.Count; i++)
        {
            SetWorldZ(segment.rails[i], z);
        }
    }

    static void TranslateWorldZ(GameObject target, float step)
    {
        if (target == null)
        {
            return;
        }

        target.transform.Translate(Vector3.back * step, Space.World);
    }

    static void SetWorldZ(GameObject target, float z)
    {
        if (target == null)
        {
            return;
        }

        Vector3 position = target.transform.position;
        position.z = z;
        target.transform.position = position;
    }
}
