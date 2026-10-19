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
    [Range(1, 40)] public int poolSize = 6;

    [Header("Spawn Volume")]
    public float corridorCenterX = -2.1f;
    [Tooltip("Half-width of the spawn band either side of the corridor centre.")]
    public float spawnHalfWidth = 15f;
    [Tooltip("Keeps debris out of the lanes and the full-span stimulus.")]
    public float corridorKeepOutHalfWidth = 5.5f;
    public Vector2 spawnZRange = new Vector2(250f, 300f);
    public Vector2 spawnYRange = new Vector2(1f, 8f);
    public float recycleZ = -20f;

    [Header("Motion")]
    public Vector2 forwardSpeedRange = new Vector2(18f, 37.5f);
    [Tooltip("Lateral drift is always away from the corridor.")]
    public float maxLateralDrift = 2f;
    public float maxVerticalDrift = 0.5f;
    public Vector2 spinSpeedRange = new Vector2(20f, 90f);
    [Tooltip("Seconds between each asteroid placed when the run starts.")]
    public float initialPlaceInterval = 3.0f;

    static readonly Color RockColor = new Color(0.36f, 0.33f, 0.31f, 1f);
    static readonly Color HullColor = new Color(0.55f, 0.58f, 0.62f, 1f);
    static readonly Color AntennaColor = new Color(0.42f, 0.44f, 0.47f, 1f);
    static readonly Color AntennaTipGlow = new Color(1.6f, 0.35f, 0.25f, 1f);
    static readonly Color StarColor = new Color(0.9f, 0.95f, 1f, 1f);
    static readonly Color StarGlow = new Color(1.4f, 1.6f, 2.2f, 1f);

    const string ModelResourceRoot = "AmbientDebris";

    static readonly float[] ImportedSize =
    {
        3.6f,
        2.0f,
        1.8f,
        1.4f
    };

    readonly List<AmbientDebrisItem> pool = new List<AmbientDebrisItem>();
    GameObject[][] importedModels;
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
    int nextToPlace;
    float placeTimer;

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
        LoadImportedModels();
        if (NeedsGeneratedMeshes())
        {
            BuildAssets();
        }

        bool useImportedAsteroid = HasImportedAsteroid();
        for (int i = 0; i < poolSize; i++)
        {
            AmbientDebrisKind kind = useImportedAsteroid
                ? AmbientDebrisKind.Asteroid
                : (AmbientDebrisKind)(i % 4);
            AmbientDebrisItem item = CreateItem(kind, i);
            item.gameObject.SetActive(false);
            pool.Add(item);
        }

        PlaceNextAsteroid();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || (player != null && player.isGameOver))
        {
            return;
        }

        if (nextToPlace < pool.Count)
        {
            placeTimer += dt;
            if (placeTimer >= initialPlaceInterval)
            {
                placeTimer = 0f;
                PlaceNextAsteroid();
            }
        }

        for (int i = 0; i < pool.Count; i++)
        {
            AmbientDebrisItem item = pool[i];
            if (!item.gameObject.activeSelf)
            {
                continue;
            }

            Transform itemTransform = item.transform;
            itemTransform.position += item.velocity * dt;
            itemTransform.Rotate(item.spinAxis, item.spinSpeed * dt, Space.World);

            if (itemTransform.position.z < recycleZ)
            {
                Respawn(item, Random.Range(spawnZRange.x, spawnZRange.y));
            }
        }
    }

    void PlaceNextAsteroid()
    {
        if (nextToPlace >= pool.Count)
        {
            return;
        }

        AmbientDebrisItem item = pool[nextToPlace];
        nextToPlace++;
        item.gameObject.SetActive(true);
        Respawn(item, Random.Range(spawnZRange.x, spawnZRange.y));
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
            -PickDistinctForwardSpeed(item));
        item.spinAxis = Random.onUnitSphere;
        item.spinSpeed = item.kind == AmbientDebrisKind.StarCluster
            ? Random.Range(5f, 15f)
            : Random.Range(spinSpeedRange.x, spinSpeedRange.y);

        spawnCount++;
        item.elementId = item.kind + "_" + spawnCount;
    }

    // Random inside forwardSpeedRange, but far enough from every other asteroid
    // that no two share the same approach speed.
    float PickDistinctForwardSpeed(AmbientDebrisItem self)
    {
        float slow = Mathf.Min(forwardSpeedRange.x, forwardSpeedRange.y);
        float fast = Mathf.Max(forwardSpeedRange.x, forwardSpeedRange.y);
        int slots = Mathf.Max(pool.Count, 1);
        float minGap = (fast - slow) / slots;
        if (minGap < 0.01f)
        {
            return Random.Range(slow, fast);
        }

        for (int attempt = 0; attempt < 32; attempt++)
        {
            float speed = Random.Range(slow, fast);
            if (IsSeparatedFromOthers(self, speed, minGap))
            {
                return speed;
            }
        }

        return FarthestForwardSpeed(self, slow, fast);
    }

    bool IsSeparatedFromOthers(AmbientDebrisItem self, float speed, float minGap)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            AmbientDebrisItem other = pool[i];
            if (other == null || other == self)
            {
                continue;
            }

            float otherSpeed = Mathf.Abs(other.velocity.z);
            if (otherSpeed < 0.01f)
            {
                continue;
            }

            if (Mathf.Abs(otherSpeed - speed) < minGap)
            {
                return false;
            }
        }

        return true;
    }

    float FarthestForwardSpeed(AmbientDebrisItem self, float slow, float fast)
    {
        float[] used = new float[pool.Count];
        int usedCount = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            AmbientDebrisItem other = pool[i];
            if (other == null || other == self)
            {
                continue;
            }

            float otherSpeed = Mathf.Abs(other.velocity.z);
            if (otherSpeed < 0.01f)
            {
                continue;
            }

            used[usedCount] = otherSpeed;
            usedCount++;
        }

        if (usedCount == 0)
        {
            return Random.Range(slow, fast);
        }

        System.Array.Sort(used, 0, usedCount);
        float best = slow;
        float bestGap = used[0] - slow;

        for (int i = 0; i < usedCount - 1; i++)
        {
            float gap = (used[i + 1] - used[i]) * 0.5f;
            if (gap > bestGap)
            {
                bestGap = gap;
                best = (used[i] + used[i + 1]) * 0.5f;
            }
        }

        float tailGap = fast - used[usedCount - 1];
        if (tailGap > bestGap)
        {
            best = fast;
        }

        return Mathf.Clamp(best, slow, fast);
    }

    AmbientDebrisItem CreateItem(AmbientDebrisKind kind, int index)
    {
        GameObject root = new GameObject("Debris_" + kind + "_" + index);
        root.transform.SetParent(transform, false);

        AmbientDebrisItem item = root.AddComponent<AmbientDebrisItem>();
        item.kind = kind;

        GameObject[] asteroids = importedModels[(int)AmbientDebrisKind.Asteroid];
        if (asteroids != null && asteroids.Length > 0)
        {
            GameObject visual = Instantiate(asteroids[index % asteroids.Length], root.transform);
            visual.name = "Model";
            PrepareImportedModel(visual, ImportedSize[(int)AmbientDebrisKind.Asteroid]);
            return item;
        }

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

    void LoadImportedModels()
    {
        importedModels = new GameObject[4][];
        for (int kind = 0; kind < importedModels.Length; kind++)
        {
            string folder = ModelResourceRoot + "/" + (AmbientDebrisKind)kind;
            importedModels[kind] = Resources.LoadAll<GameObject>(folder);
        }
    }

    bool HasImportedAsteroid()
    {
        GameObject[] asteroids = importedModels[(int)AmbientDebrisKind.Asteroid];
        return asteroids != null && asteroids.Length > 0;
    }

    bool NeedsGeneratedMeshes()
    {
        return !HasImportedAsteroid();
    }

    static void PrepareImportedModel(GameObject visual, float targetSize)
    {
        Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Destroy(colliders[i]);
        }

        Rigidbody[] bodies = visual.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            Destroy(bodies[i]);
        }

        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            renderers[i].receiveShadows = false;
            renderers[i].gameObject.tag = "Untagged";
        }

        visual.tag = "Untagged";
        MakeImportedMaterialsVisible(renderers);
        FitToSize(visual, targetSize);
    }

    // The curved shader has no skybox reflections. On a metal Meshy material that
    // zeroes the diffuse color, so the mesh stays in the hierarchy and draws black.
    // Keep the imported URP material and only force it opaque and double-sided.
    static void MakeImportedMaterialsVisible(Renderer[] renderers)
    {
        Texture2D albedo = Resources.Load<Texture2D>(
            ModelResourceRoot + "/Asteroid/Meshy_AI_Ironbound_Asteroid_1019212454_texture");

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.enabled = true;
            renderer.forceRenderingOff = false;

            Material[] materials = renderer.materials;
            for (int m = 0; m < materials.Length; m++)
            {
                Material material = materials[m];
                if (material == null || material.shader == null || material.shader.name.Contains("Error"))
                {
                    material = CurvedWorld.CreateMaterial("DebrisAsteroid", Color.white, Color.black, 0f, 0.15f);
                    if (albedo != null)
                    {
                        material.SetTexture("_BaseMap", albedo);
                    }

                    materials[m] = material;
                }

                if (material.HasTexture("_BaseMap") && material.GetTexture("_BaseMap") == null && albedo != null)
                {
                    material.SetTexture("_BaseMap", albedo);
                }

                if (material.HasColor("_BaseColor"))
                {
                    Color tint = material.GetColor("_BaseColor");
                    tint.a = 1f;
                    if (tint.maxColorComponent <= 0f)
                    {
                        tint = Color.white;
                    }

                    material.SetColor("_BaseColor", tint);
                }

                // A fully metal surface with this lighting reads as empty space.
                if (material.HasFloat("_Metallic"))
                {
                    material.SetFloat("_Metallic", Mathf.Min(material.GetFloat("_Metallic"), 0.35f));
                }

                SetFloatIfPresent(material, "_Smoothness", 0f);
                SetFloatIfPresent(material, "_Glossiness", 0f);
                SetFloatIfPresent(material, "smoothnessFactor", 0f);

                if (material.HasFloat("_Cull"))
                {
                    material.SetFloat("_Cull", 0f);
                }

                material.SetOverrideTag("RenderType", "Opaque");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                if (material.HasFloat("_Surface"))
                {
                    material.SetFloat("_Surface", 0f);
                }

                if (material.HasFloat("_ZWrite"))
                {
                    material.SetFloat("_ZWrite", 1f);
                }

                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }

            renderer.materials = materials;
        }
    }

    static void SetFloatIfPresent(Material material, string property, float value)
    {
        if (material.HasFloat(property))
        {
            material.SetFloat(property, value);
        }
    }

    static void FitToSize(GameObject visual, float targetSize)
    {
        if (visual == null || targetSize <= 0f || !TryGetLocalBounds(visual, out Bounds bounds))
        {
            return;
        }

        float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (largest < 0.0001f)
        {
            return;
        }

        float scale = targetSize / largest;
        visual.transform.localScale = Vector3.one * scale;
        visual.transform.localPosition = -bounds.center * scale;
    }

    // Mesh bounds, not renderer.bounds: a freshly instantiated FBX often reports
    // an empty renderer bounds, which used to leave the model at a near-zero scale.
    static bool TryGetLocalBounds(GameObject visual, out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool found = false;
        Transform root = visual.transform;

        MeshFilter[] filters = visual.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            Mesh mesh = filters[i].sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            Encapsulate(root, filters[i].transform, mesh.bounds, ref bounds, ref found);
        }

        SkinnedMeshRenderer[] skinned = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinned.Length; i++)
        {
            Mesh mesh = skinned[i].sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            Encapsulate(root, skinned[i].transform, mesh.bounds, ref bounds, ref found);
        }

        return found;
    }

    static void Encapsulate(Transform root, Transform meshTransform, Bounds meshBounds, ref Bounds bounds, ref bool found)
    {
        Vector3 extents = meshBounds.extents;
        Vector3 center = meshBounds.center;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 local = center + new Vector3(
                (corner & 1) == 0 ? -extents.x : extents.x,
                (corner & 2) == 0 ? -extents.y : extents.y,
                (corner & 4) == 0 ? -extents.z : extents.z);
            Vector3 inRoot = root.InverseTransformPoint(meshTransform.TransformPoint(local));
            if (!found)
            {
                bounds = new Bounds(inRoot, Vector3.zero);
                found = true;
            }
            else
            {
                bounds.Encapsulate(inRoot);
            }
        }
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
