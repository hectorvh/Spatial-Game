using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Spec Feature 4: a pool of collider-free asteroids, hull panels, antennas and star clusters
/// that fly past on both sides of the lanes. Separate from SpawnManager on purpose; those
/// objects are tagged Obstacle and would interfere with crashes and shots.
/// </summary>
public class AmbientDebrisManager : MonoBehaviour
{
    public static AmbientDebrisManager Instance { get; private set; }

    [Header("Pool")]
    [Range(1, 40)] public int poolSize = 18;

    [Header("Spawn Volume")]
    public float corridorCenterX = -2.1f;
    [Tooltip("Half-width of the spawn band either side of the corridor centre.")]
    public float spawnHalfWidth = 15f;
    [Tooltip("Keeps debris out of the lanes and the full-span stimulus.")]
    public float corridorKeepOutHalfWidth = 5.5f;
    public Vector2 spawnZRange = new Vector2(60f, 80f);
    public Vector2 spawnYRange = new Vector2(1f, 8f);
    public float recycleZ = -20f;

    [Header("Motion")]
    public Vector2 forwardSpeedRange = new Vector2(12f, 25f);
    [Tooltip("Lateral drift is always away from the corridor.")]
    public float maxLateralDrift = 2f;
    public float maxVerticalDrift = 0.5f;
    public Vector2 spinSpeedRange = new Vector2(20f, 90f);

    static readonly Color RockColor = new Color(0.36f, 0.33f, 0.31f, 1f);
    static readonly Color HullColor = new Color(0.55f, 0.58f, 0.62f, 1f);
    static readonly Color AntennaColor = new Color(0.42f, 0.44f, 0.47f, 1f);
    static readonly Color AntennaTipGlow = new Color(1.6f, 0.35f, 0.25f, 1f);
    static readonly Color StarColor = new Color(0.9f, 0.95f, 1f, 1f);
    static readonly Color StarGlow = new Color(1.4f, 1.6f, 2.2f, 1f);

    readonly List<AmbientDebrisItem> pool = new List<AmbientDebrisItem>();
    PlayerControllerX player;
    Mesh cubeMesh;
    Mesh[] rockMeshes;
    Mesh starMesh;
    Material rockMaterial;
    Material hullMaterial;
    Material antennaMaterial;
    Material antennaTipMaterial;
    Material starMaterial;
    int spawnCount;

    public static AmbientDebrisManager Ensure()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject managerObject = new GameObject("AmbientDebrisManager");
        return managerObject.AddComponent<AmbientDebrisManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        player = FindAnyObjectByType<PlayerControllerX>();
        BuildAssets();

        for (int i = 0; i < poolSize; i++)
        {
            AmbientDebrisItem item = CreateItem((AmbientDebrisKind)(i % 4), i);
            // Spread the first batch along the whole run so the sky is not empty at start.
            Respawn(item, Random.Range(recycleZ, spawnZRange.y));
            pool.Add(item);
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || (player != null && player.isGameOver))
        {
            return;
        }

        for (int i = 0; i < pool.Count; i++)
        {
            AmbientDebrisItem item = pool[i];
            Transform itemTransform = item.transform;
            itemTransform.position += item.velocity * dt;
            itemTransform.Rotate(item.spinAxis, item.spinSpeed * dt, Space.World);

            if (itemTransform.position.z < recycleZ)
            {
                Respawn(item, Random.Range(spawnZRange.x, spawnZRange.y));
            }
        }
    }

    void Respawn(AmbientDebrisItem item, float z)
    {
        float side = Random.value < 0.5f ? -1f : 1f;
        float keepOut = Mathf.Min(corridorKeepOutHalfWidth, spawnHalfWidth);
        float x = corridorCenterX + (side * Random.Range(keepOut, spawnHalfWidth));
        float y = Random.Range(spawnYRange.x, spawnYRange.y);

        item.transform.position = new Vector3(x, y, z);
        item.transform.rotation = Random.rotationUniform;
        item.velocity = new Vector3(
            side * Random.Range(0f, maxLateralDrift),
            Random.Range(-maxVerticalDrift, maxVerticalDrift),
            -Random.Range(forwardSpeedRange.x, forwardSpeedRange.y));
        item.spinAxis = Random.onUnitSphere;
        item.spinSpeed = item.kind == AmbientDebrisKind.StarCluster
            ? Random.Range(5f, 15f)
            : Random.Range(spinSpeedRange.x, spinSpeedRange.y);

        spawnCount++;
        item.elementId = item.kind + "_" + spawnCount;
    }

    AmbientDebrisItem CreateItem(AmbientDebrisKind kind, int index)
    {
        GameObject root = new GameObject("Debris_" + kind + "_" + index);
        root.transform.SetParent(transform, false);

        AmbientDebrisItem item = root.AddComponent<AmbientDebrisItem>();
        item.kind = kind;

        switch (kind)
        {
            case AmbientDebrisKind.Asteroid:
                AddPart(root.transform, rockMeshes[index % rockMeshes.Length], rockMaterial,
                    Vector3.zero, Vector3.one * Random.Range(0.6f, 1.6f));
                break;
            case AmbientDebrisKind.HullPanel:
                AddPart(root.transform, cubeMesh, hullMaterial,
                    Vector3.zero, new Vector3(Random.Range(1.2f, 2.4f), 0.08f, Random.Range(0.8f, 1.6f)));
                AddPart(root.transform, cubeMesh, hullMaterial,
                    new Vector3(0f, 0.06f, 0f), new Vector3(0.1f, 0.06f, 1.2f));
                break;
            case AmbientDebrisKind.Antenna:
                AddPart(root.transform, cubeMesh, antennaMaterial,
                    Vector3.zero, new Vector3(0.08f, 1.8f, 0.08f));
                AddPart(root.transform, cubeMesh, antennaMaterial,
                    new Vector3(0f, 0.55f, 0f), new Vector3(0.9f, 0.05f, 0.05f));
                AddPart(root.transform, starMesh, antennaTipMaterial,
                    new Vector3(0f, 0.95f, 0f), Vector3.one * 0.14f);
                break;
            case AmbientDebrisKind.StarCluster:
                int stars = Random.Range(5, 9);
                for (int s = 0; s < stars; s++)
                {
                    AddPart(root.transform, starMesh, starMaterial,
                        Random.insideUnitSphere * 1.4f, Vector3.one * Random.Range(0.06f, 0.16f));
                }

                break;
        }

        return item;
    }

    static void AddPart(Transform parent, Mesh mesh, Material material, Vector3 localPosition, Vector3 localScale)
    {
        GameObject part = new GameObject("Part");
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;

        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    void BuildAssets()
    {
        cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        rockMeshes = new Mesh[4];
        for (int i = 0; i < rockMeshes.Length; i++)
        {
            rockMeshes[i] = BuildIcosahedron("DebrisRock" + i, 0.35f, 1000 + i);
        }

        starMesh = BuildIcosahedron("DebrisStar", 0f, 0);

        // CreateMaterial uses the curved shader when present, so debris follows the horizon bend.
        rockMaterial = CurvedWorld.CreateMaterial("DebrisRock", RockColor, Color.black, 0.1f);
        hullMaterial = CurvedWorld.CreateMaterial("DebrisHull", HullColor, Color.black, 0.55f, 0.6f);
        antennaMaterial = CurvedWorld.CreateMaterial("DebrisAntenna", AntennaColor, Color.black, 0.4f, 0.5f);
        antennaTipMaterial = CurvedWorld.CreateMaterial("DebrisAntennaTip", AntennaColor, AntennaTipGlow);
        starMaterial = CurvedWorld.CreateMaterial("DebrisStar", StarColor, StarGlow);
    }

    // Flat-shaded (unshared vertices) icosahedron; jitter > 0 roughens it into a rock.
    static Mesh BuildIcosahedron(string name, float jitter, int seed)
    {
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        Vector3[] corners =
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
        };
        int[] faces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };

        System.Random rng = new System.Random(seed);
        for (int i = 0; i < corners.Length; i++)
        {
            float scale = 1f + (jitter * (((float)rng.NextDouble() * 2f) - 1f));
            corners[i] = corners[i].normalized * 0.5f * scale;
        }

        Vector3[] vertices = new Vector3[faces.Length];
        int[] triangles = new int[faces.Length];
        for (int i = 0; i < faces.Length; i++)
        {
            vertices[i] = corners[faces[i]];
            triangles[i] = i;
        }

        Mesh mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
