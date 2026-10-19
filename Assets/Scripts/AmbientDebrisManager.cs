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
    [Tooltip("Each asteroid draws two speeds from this range. The higher one is used far away, the lower one as it reaches the bunny, blended in a straight line.")]
    public Vector2 forwardSpeedRange = new Vector2(18f, 70f);
    [Tooltip("Lateral drift is always away from the corridor.")]
    public float maxLateralDrift = 2f;
    public float maxVerticalDrift = 0.5f;
    public Vector2 spinSpeedRange = new Vector2(20f, 90f);
    [Tooltip("Seconds between each asteroid placed when the run starts.")]
    public float initialPlaceInterval = 3.0f;
    [Tooltip("How long an asteroid takes to fade in after it appears ahead.")]
    public float fadeInSeconds = 2f;

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
            ApplyApproachSpeed(item);
            itemTransform.position += item.velocity * dt;
            itemTransform.Rotate(item.spinAxis, item.spinSpeed * dt, Space.World);
            UpdateFade(item, dt);

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
        AssignSpeedCurve(item);
        item.velocity = new Vector3(
            side * Random.Range(0f, maxLateralDrift),
            Random.Range(-maxVerticalDrift, maxVerticalDrift),
            0f);
        ApplyApproachSpeed(item);
        item.spinAxis = Random.onUnitSphere;
        item.spinSpeed = item.kind == AmbientDebrisKind.StarCluster
            ? Random.Range(5f, 15f)
            : Random.Range(spinSpeedRange.x, spinSpeedRange.y);

        spawnCount++;
        item.elementId = item.kind + "_" + spawnCount;
        BeginFade(item);
    }

    void BeginFade(AmbientDebrisItem item)
    {
        if (item.renderers == null || item.renderers.Length == 0)
        {
            item.renderers = item.GetComponentsInChildren<Renderer>(true);
        }

        item.fadeIn = 0f;
        item.fading = true;
        ForEachMaterial(item, delegate (Material material)
        {
            SetTransparent(material);
            SetAlpha(material, 0f);
        });
    }

    void UpdateFade(AmbientDebrisItem item, float dt)
    {
        if (!item.fading)
        {
            return;
        }

        item.fadeIn += dt / Mathf.Max(0.05f, fadeInSeconds);
        float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(item.fadeIn));
        bool finished = item.fadeIn >= 1f;
        ForEachMaterial(item, delegate (Material material)
        {
            SetAlpha(material, finished ? 1f : alpha);
            if (finished)
            {
                SetOpaque(material);
            }
        });

        if (finished)
        {
            item.fading = false;
        }
    }

    static void ForEachMaterial(AmbientDebrisItem item, System.Action<Material> action)
    {
        Renderer[] renderers = item.renderers;
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.materials;
            for (int m = 0; m < materials.Length; m++)
            {
                if (materials[m] != null)
                {
                    action(materials[m]);
                }
            }
        }
    }

    static void SetAlpha(Material material, float alpha)
    {
        if (material.HasColor("_BaseColor"))
        {
            Color color = material.GetColor("_BaseColor");
            color.a = alpha;
            material.SetColor("_BaseColor", color);
        }

        if (material.HasColor("_Color"))
        {
            Color color = material.GetColor("_Color");
            color.a = alpha;
            material.SetColor("_Color", color);
        }
    }

    static void SetTransparent(Material material)
    {
        if (material.HasFloat("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasFloat("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        if (material.HasFloat("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        if (material.HasFloat("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }

        if (material.HasFloat("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }

        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    static void SetOpaque(Material material)
    {
        SetAlpha(material, 1f);
        if (material.HasFloat("_Surface"))
        {
            material.SetFloat("_Surface", 0f);
        }

        if (material.HasFloat("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 1f);
        }

        if (material.HasFloat("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
        }

        if (material.HasFloat("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
        }

        material.SetOverrideTag("RenderType", "Opaque");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
    }

    // Far end of the spawn band is full speed. Speed falls in a straight line to the
    // asteroid's own minimum as it reaches the bunny at z = 0.
    void ApplyApproachSpeed(AmbientDebrisItem item)
    {
        float farZ = Mathf.Max(spawnZRange.x, spawnZRange.y);
        float along = Mathf.InverseLerp(farZ, 0f, item.transform.position.z);
        float speed = Mathf.Lerp(item.maxForwardSpeed, item.minForwardSpeed, along);
        item.velocity.z = -Mathf.Max(0f, speed);
    }

    void AssignSpeedCurve(AmbientDebrisItem item)
    {
        float slow = Mathf.Min(forwardSpeedRange.x, forwardSpeedRange.y);
        float fast = Mathf.Max(forwardSpeedRange.x, forwardSpeedRange.y);
        float span = Mathf.Max(0.01f, fast - slow);
        float gap = span / Mathf.Max(pool.Count, 1);

        for (int attempt = 0; attempt < 40; attempt++)
        {
            float a = Random.Range(slow, fast);
            float b = Random.Range(slow, fast);
            float minSpeed = Mathf.Min(a, b);
            float maxSpeed = Mathf.Max(a, b);
            if (maxSpeed - minSpeed < gap * 0.5f)
            {
                continue;
            }

            if (CurveIsSeparated(item, minSpeed, maxSpeed, gap))
            {
                item.minForwardSpeed = minSpeed;
                item.maxForwardSpeed = maxSpeed;
                return;
            }
        }

        item.maxForwardSpeed = FarthestUnusedSpeed(item, slow, fast, true);
        float minCeiling = Mathf.Max(slow, item.maxForwardSpeed - gap * 0.5f);
        item.minForwardSpeed = FarthestUnusedSpeed(item, slow, minCeiling, false);
        if (item.minForwardSpeed > item.maxForwardSpeed)
        {
            float swap = item.minForwardSpeed;
            item.minForwardSpeed = item.maxForwardSpeed;
            item.maxForwardSpeed = swap;
        }
    }

    bool CurveIsSeparated(AmbientDebrisItem self, float minSpeed, float maxSpeed, float gap)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            AmbientDebrisItem other = pool[i];
            if (other == null || other == self || other.maxForwardSpeed < 0f)
            {
                continue;
            }

            if (Mathf.Abs(other.maxForwardSpeed - maxSpeed) < gap
                || Mathf.Abs(other.minForwardSpeed - minSpeed) < gap)
            {
                return false;
            }
        }

        return true;
    }

    float FarthestUnusedSpeed(AmbientDebrisItem self, float slow, float fast, bool useMax)
    {
        if (fast < slow)
        {
            float swap = slow;
            slow = fast;
            fast = swap;
        }

        float[] used = new float[pool.Count];
        int usedCount = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            AmbientDebrisItem other = pool[i];
            if (other == null || other == self || other.maxForwardSpeed < 0f)
            {
                continue;
            }

            used[usedCount] = useMax ? other.maxForwardSpeed : other.minForwardSpeed;
            usedCount++;
        }

        if (usedCount == 0 || fast - slow < 0.01f)
        {
            return Random.Range(slow, Mathf.Max(slow, fast));
        }

        System.Array.Sort(used, 0, usedCount);
        float best = slow;
        float bestGap = used[0] - slow;

        for (int i = 0; i < usedCount - 1; i++)
        {
            float opening = (used[i + 1] - used[i]) * 0.5f;
            if (opening > bestGap)
            {
                bestGap = opening;
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
