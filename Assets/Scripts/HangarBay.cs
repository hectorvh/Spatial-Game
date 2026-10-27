using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spec Feature 10 end cap. Placed after the last corridor tile once recycling stops, then
/// scrolled at the track speed until LevelFlowController brakes the track.
///
/// Uses Resources/Hangar/HangarBayEndTile when that prefab exists. Its root must sit at the
/// entrance, at floor level, with +Z pointing into the bay. Child names read by this script:
/// RampFoot, Hatch, Cabin (boarding path), and optionally Shuttle, RampPivot (stows by
/// rotating +X), DoorLeft, DoorRight (slide outward on X), Plume* (scaled on ignition).
/// Without the prefab a primitive bay with a docked shuttle is built instead.
/// </summary>
public class HangarBay : MonoBehaviour
{
    public const string PrefabResource = "Hangar/HangarBayEndTile";

    const float ShuttleScale = 1.25f;
    const float ShuttleLocalZ = 26f;
    const float DoorZ = 45.7f;
    const float RampStowAngle = 110f;

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    TrackManager track;
    PlayerControllerX player;
    LevelInfo level;
    bool scrolling = true;

    Transform rampFoot;
    Transform hatch;
    Transform cabin;
    Transform shuttle;
    Transform struts;
    Transform rampPivot;
    Transform doorLeft;
    Transform doorRight;
    Transform spaceView;
    readonly List<Transform> plumes = new List<Transform>();
    readonly List<Vector3> plumeScales = new List<Vector3>();
    float doorTravel = 8.4f;

    Material warningMaterial;
    Color warningColor = new Color(1f, 0.55f, 0.08f);
    float blinkRate = 1.2f;
    float blinkClock;

    public float EntranceZ => transform.position.z;
    public Vector3 RampFoot => rampFoot.position;
    public Vector3 Hatch => hatch.position;
    public Vector3 Cabin => cabin.position;

    public static HangarBay Create(Vector3 entrance, TrackManager track, LevelInfo level)
    {
        GameObject prefab = Resources.Load<GameObject>(PrefabResource);
        GameObject root = prefab != null
            ? Instantiate(prefab, entrance, Quaternion.identity)
            : new GameObject("HangarBayEndTile (procedural)");
        root.transform.position = entrance;

        HangarBay bay = root.GetComponent<HangarBay>();
        if (bay == null)
        {
            bay = root.AddComponent<HangarBay>();
        }

        bay.track = track;
        bay.level = level;
        bay.player = FindAnyObjectByType<PlayerControllerX>();

        if (prefab != null)
        {
            root.name = "HangarBayEndTile";
            bay.BindPrefab();
        }
        else
        {
            bay.BuildProcedural();
        }

        CurvedWorld.Apply(root);
        return bay;
    }

    /// <summary>Moves the bay so the ramp foot sits exactly at z, after braking.</summary>
    public void SnapRampFootTo(float z)
    {
        scrolling = false;
        transform.position += Vector3.forward * (z - rampFoot.position.z);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        bool runOver = player != null && player.isGameOver;
        if (scrolling && track != null && !runOver)
        {
            transform.position += Vector3.back * (track.trackSpeed * dt);
        }

        if (warningMaterial != null)
        {
            blinkClock += dt * blinkRate;
            bool lit = Mathf.Repeat(blinkClock, 1f) < 0.5f;
            warningMaterial.SetColor(EmissionColorId, warningColor * (lit ? 2.5f : 0.12f));
        }

    }

    /// <summary>Ramp stows, engines ignite, blast doors open, the shuttle lifts and leaves.</summary>
    public IEnumerator PlayTakeoff()
    {
        blinkRate = 3.2f;

        Quaternion rampOpen = rampPivot != null ? rampPivot.localRotation : Quaternion.identity;
        Quaternion rampClosed = rampOpen * Quaternion.Euler(RampStowAngle, 0f, 0f);
        yield return Animate(1.0f, t =>
        {
            if (rampPivot != null)
            {
                rampPivot.localRotation = Quaternion.Slerp(rampOpen, rampClosed, Ease(t));
            }
        });

        if (spaceView != null)
        {
            spaceView.gameObject.SetActive(true);
        }

        Vector3 leftClosed = doorLeft != null ? doorLeft.localPosition : Vector3.zero;
        Vector3 rightClosed = doorRight != null ? doorRight.localPosition : Vector3.zero;
        yield return Animate(2.2f, t =>
        {
            SetPlumes(Mathf.Lerp(0.25f, 1f, Ease(Mathf.Clamp01(t * 1.6f))));
            float open = Ease(t) * doorTravel;
            if (doorLeft != null)
            {
                doorLeft.localPosition = leftClosed + (Vector3.left * open);
            }

            if (doorRight != null)
            {
                doorRight.localPosition = rightClosed + (Vector3.right * open);
            }
        });

        if (shuttle == null)
        {
            yield break;
        }

        Vector3 docked = shuttle.localPosition;
        Vector3 strutScale = struts != null ? struts.localScale : Vector3.one;
        yield return Animate(1.4f, t =>
        {
            shuttle.localPosition = docked + (Vector3.up * (2.4f * Ease(t)));
            if (struts != null)
            {
                struts.localScale = Vector3.Scale(strutScale, new Vector3(1f, 1f - Ease(t), 1f));
            }

            SetPlumes(1f);
        });

        Vector3 hover = shuttle.localPosition;
        float speed = 0f;
        float travelled = 0f;
        float elapsed = 0f;
        while (elapsed < 3f)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            speed += 30f * dt;
            travelled += speed * dt;

            // Shallow arc: from the fixed camera the door opening ends about 5 degrees above the horizon.
            float arc = travelled * travelled;
            shuttle.localPosition = hover + new Vector3(0.0006f * arc, 0.0008f * arc, travelled);
            shuttle.localRotation = Quaternion.Euler(-Mathf.Min(12f, travelled * 0.15f), 0f, 0f);
            SetPlumes(1.15f);
            yield return null;
        }

        shuttle.gameObject.SetActive(false);
    }

    IEnumerator Animate(float seconds, Action<float> step)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            step(Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }

        step(1f);
    }

    static float Ease(float t)
    {
        return Mathf.SmoothStep(0f, 1f, t);
    }

    void SetPlumes(float strength)
    {
        float flicker = 1f + (0.12f * Mathf.Sin(Time.time * 47f)) + UnityEngine.Random.Range(-0.05f, 0.05f);
        for (int i = 0; i < plumes.Count; i++)
        {
            Vector3 full = plumeScales[i];
            float length = Mathf.Max(0.05f, strength * flicker);
            plumes[i].localScale = new Vector3(full.x * Mathf.Lerp(0.6f, 1f, strength), full.y * Mathf.Lerp(0.6f, 1f, strength), full.z * length);
        }
    }

    void BindPrefab()
    {
        rampFoot = FindDeep(transform, "RampFoot");
        hatch = FindDeep(transform, "Hatch");
        cabin = FindDeep(transform, "Cabin");
        shuttle = FindDeep(transform, "Shuttle");
        rampPivot = FindDeep(transform, "RampPivot");
        doorLeft = FindDeep(transform, "DoorLeft");
        doorRight = FindDeep(transform, "DoorRight");

        if (rampFoot == null)
        {
            Debug.LogWarning("HangarBay: prefab has no RampFoot child; using 16.7 m inside the entrance.");
            rampFoot = Marker("RampFoot", transform, new Vector3(0f, 0f, 16.7f));
        }

        if (hatch == null)
        {
            hatch = Marker("Hatch", transform, rampFoot.localPosition + new Vector3(0f, 2f, 5f));
        }

        if (cabin == null)
        {
            cabin = Marker("Cabin", transform, hatch.localPosition + new Vector3(0f, 0.4f, 2f));
        }

        if (doorLeft != null)
        {
            Renderer doorRenderer = doorLeft.GetComponentInChildren<Renderer>();
            if (doorRenderer != null)
            {
                doorTravel = doorRenderer.bounds.size.x + 0.3f;
            }
        }

        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name.StartsWith("Plume", StringComparison.OrdinalIgnoreCase))
            {
                plumes.Add(all[i]);
                plumeScales.Add(all[i].localScale);
            }
        }

        SetPlumes(0.25f);
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    void BuildProcedural()
    {
        Material floor = CurvedWorld.CreateMaterial("HangarFloor", new Color(0.22f, 0.24f, 0.28f), Color.black, 0.35f, 0.3f);
        Material wall = CurvedWorld.CreateMaterial("HangarWall", new Color(0.16f, 0.18f, 0.22f), Color.black, 0.25f, 0.2f);
        Material rib = CurvedWorld.CreateMaterial("HangarRib", new Color(0.11f, 0.12f, 0.15f), Color.black, 0.3f, 0.4f);
        Material hazard = CurvedWorld.CreateMaterial("HangarHazard", new Color(0.95f, 0.72f, 0.12f), new Color(0.35f, 0.25f, 0.02f), 0.3f, 0f);
        Material guide = CurvedWorld.CreateMaterial("HangarGuide", new Color(0.37f, 0.77f, 0.87f), new Color(0.3f, 0.9f, 1.1f), 0.3f, 0f);
        Material strip = CurvedWorld.CreateMaterial("HangarLightStrip", new Color(0.85f, 0.92f, 1f), new Color(1.3f, 1.45f, 1.6f), 0.3f, 0f);
        Material door = CurvedWorld.CreateMaterial("HangarDoor", new Color(0.30f, 0.33f, 0.38f), Color.black, 0.4f, 0.5f);
        warningMaterial = CurvedWorld.CreateMaterial("HangarWarning", warningColor, warningColor, 0.3f, 0f);

        Block("Floor", PrimitiveType.Cube, transform, new Vector3(0f, -0.1f, 23f), new Vector3(16.6f, 0.2f, 46f), floor);
        Block("ThresholdStripe", PrimitiveType.Cube, transform, new Vector3(0f, 0.01f, 0.6f), new Vector3(16.6f, 0.02f, 1.2f), hazard);
        Block("GuideLeft", PrimitiveType.Cube, transform, new Vector3(-1.3f, 0.01f, 9.5f), new Vector3(0.12f, 0.02f, 14f), guide);
        Block("GuideRight", PrimitiveType.Cube, transform, new Vector3(1.3f, 0.01f, 9.5f), new Vector3(0.12f, 0.02f, 14f), guide);

        Block("WallLeft", PrimitiveType.Cube, transform, new Vector3(-8.6f, 5.125f, 23f), new Vector3(0.6f, 10.25f, 46f), wall);
        Block("WallRight", PrimitiveType.Cube, transform, new Vector3(8.6f, 5.125f, 23f), new Vector3(0.6f, 10.25f, 46f), wall);
        Block("Roof", PrimitiveType.Cube, transform, new Vector3(0f, 10.5f, 23f), new Vector3(17.8f, 0.5f, 46f), wall);
        Block("EntranceLintel", PrimitiveType.Cube, transform, new Vector3(0f, 9.25f, 0.4f), new Vector3(17.8f, 2f, 0.8f), wall);
        Block("LintelStripe", PrimitiveType.Cube, transform, new Vector3(0f, 8.45f, -0.03f), new Vector3(16.6f, 0.35f, 0.05f), hazard);

        for (float z = 9f; z < 44f; z += 9f)
        {
            Block("RibLeft", PrimitiveType.Cube, transform, new Vector3(-8.15f, 5f, z), new Vector3(0.4f, 10f, 0.6f), rib);
            Block("RibRight", PrimitiveType.Cube, transform, new Vector3(8.15f, 5f, z), new Vector3(0.4f, 10f, 0.6f), rib);
            Block("RibBeam", PrimitiveType.Cube, transform, new Vector3(0f, 10.05f, z), new Vector3(16.4f, 0.4f, 0.6f), rib);
        }

        Block("LightStripLeft", PrimitiveType.Cube, transform, new Vector3(-3f, 10.2f, 23f), new Vector3(0.5f, 0.06f, 40f), strip);
        Block("LightStripRight", PrimitiveType.Cube, transform, new Vector3(3f, 10.2f, 23f), new Vector3(0.5f, 0.06f, 40f), strip);

        for (float z = 6f; z < 45f; z += 9f)
        {
            Block("WarningLeft", PrimitiveType.Cube, transform, new Vector3(-8.25f, 7.5f, z), new Vector3(0.12f, 0.35f, 0.7f), warningMaterial);
            Block("WarningRight", PrimitiveType.Cube, transform, new Vector3(8.25f, 7.5f, z), new Vector3(0.12f, 0.35f, 0.7f), warningMaterial);
        }

        doorLeft = BuildDoor("DoorLeft", -4.05f, 3.9f, door, hazard);
        doorRight = BuildDoor("DoorRight", 4.05f, -3.9f, door, hazard);

        // The curved shader only takes the main light, so the bay is sunlit like the
        // corridor: the shell casts no shadow onto its own floor. The shuttle still does.
        MeshRenderer[] shell = GetComponentsInChildren<MeshRenderer>();
        for (int i = 0; i < shell.Length; i++)
        {
            shell[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        BuildShuttle();
        BuildSpaceView();
    }

    Transform BuildDoor(string name, float x, float stripeX, Material door, Material hazard)
    {
        Transform pivot = Marker(name, transform, new Vector3(x, 0f, DoorZ));
        Block("Panel", PrimitiveType.Cube, pivot, new Vector3(0f, 5f, 0f), new Vector3(8.1f, 10f, 0.6f), door);
        Block("EdgeStripe", PrimitiveType.Cube, pivot, new Vector3(stripeX, 5f, -0.02f), new Vector3(0.3f, 10f, 0.62f), hazard);
        return pivot;
    }

    void BuildShuttle()
    {
        Material hull = CurvedWorld.CreateMaterial("AcornHull", new Color(0.90f, 0.86f, 0.78f), Color.black, 0.55f, 0.15f);
        Material cap = CurvedWorld.CreateMaterial("AcornCap", new Color(0.55f, 0.36f, 0.20f), Color.black, 0.3f, 0f);
        Material glass = CurvedWorld.CreateMaterial("AcornGlass", new Color(0.25f, 0.75f, 0.95f), new Color(0.1f, 0.45f, 0.6f), 0.8f, 0f);
        Material engine = CurvedWorld.CreateMaterial("AcornEngine", new Color(0.12f, 0.12f, 0.14f), Color.black, 0.4f, 0.6f);
        Material plume = CurvedWorld.CreateMaterial("AcornPlume", new Color(1f, 0.6f, 0.2f), new Color(4f, 1.6f, 0.3f), 0f, 0f);
        Material doorway = CurvedWorld.CreateMaterial("AcornDoorway", new Color(0.05f, 0.05f, 0.07f), new Color(0.1f, 0.2f, 0.3f), 0.2f, 0f);
        Material ramp = CurvedWorld.CreateMaterial("AcornRamp", new Color(0.40f, 0.43f, 0.48f), Color.black, 0.35f, 0.5f);
        Material rail = CurvedWorld.CreateMaterial("AcornRampRail", new Color(0.95f, 0.72f, 0.12f), new Color(0.35f, 0.25f, 0.02f), 0.3f, 0f);

        shuttle = Marker("Shuttle", transform, new Vector3(0f, 0f, ShuttleLocalZ));
        shuttle.localScale = Vector3.one * ShuttleScale;

        struts = Marker("Struts", shuttle, Vector3.zero);
        for (int i = 0; i < 4; i++)
        {
            float sx = i % 2 == 0 ? -1.4f : 1.4f;
            float sz = i < 2 ? -2.2f : 2.2f;
            Block("Strut", PrimitiveType.Cylinder, struts, new Vector3(sx, 0.55f, sz), new Vector3(0.25f, 0.55f, 0.25f), engine);
        }

        // Capsule rotated onto Z: scale.y is half the length, scale.z the height.
        Block("Hull", PrimitiveType.Capsule, shuttle, new Vector3(0f, 2.6f, 0f), new Vector3(3f, 3.6f, 2.8f), hull, new Vector3(90f, 0f, 0f));
        Block("Cap", PrimitiveType.Sphere, shuttle, new Vector3(0f, 3.5f, 0.3f), new Vector3(3.3f, 1.5f, 5f), cap);
        Block("Stem", PrimitiveType.Cylinder, shuttle, new Vector3(0f, 4.45f, 0.3f), new Vector3(0.3f, 0.25f, 0.3f), cap);
        Block("Cockpit", PrimitiveType.Sphere, shuttle, new Vector3(0f, 3.1f, 2.7f), new Vector3(1.8f, 1f, 1.6f), glass);
        Block("FinLeft", PrimitiveType.Cube, shuttle, new Vector3(-1.6f, 2.9f, -2.6f), new Vector3(0.2f, 1.4f, 1.6f), cap, new Vector3(0f, 0f, 25f));
        Block("FinRight", PrimitiveType.Cube, shuttle, new Vector3(1.6f, 2.9f, -2.6f), new Vector3(0.2f, 1.4f, 1.6f), cap, new Vector3(0f, 0f, -25f));
        Block("WingLeft", PrimitiveType.Cube, shuttle, new Vector3(-2.1f, 2.3f, -1f), new Vector3(1.6f, 0.15f, 1.8f), hull);
        Block("WingRight", PrimitiveType.Cube, shuttle, new Vector3(2.1f, 2.3f, -1f), new Vector3(1.6f, 0.15f, 1.8f), hull);
        Block("Doorway", PrimitiveType.Cube, shuttle, new Vector3(0f, 2.35f, -3.45f), new Vector3(1.3f, 1.5f, 0.1f), doorway);

        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 1.15f;
            Block("Engine", PrimitiveType.Cylinder, shuttle, new Vector3(x, 2.3f, -3.7f), new Vector3(0.9f, 0.5f, 0.9f), engine, new Vector3(90f, 0f, 0f));
            Transform plumePivot = Marker("Plume", shuttle, new Vector3(x, 2.3f, -4.2f));
            Block("Flame", PrimitiveType.Capsule, plumePivot, new Vector3(0f, 0f, -0.5f), new Vector3(0.6f, 0.5f, 0.6f), plume, new Vector3(90f, 0f, 0f));
            plumePivot.localScale = new Vector3(1f, 1f, 3.2f);
            plumes.Add(plumePivot);
            plumeScales.Add(plumePivot.localScale);
        }

        hatch = Marker("Hatch", shuttle, new Vector3(0f, 1.6f, -3.35f));
        cabin = Marker("Cabin", shuttle, new Vector3(0f, 2f, -1.5f));
        rampFoot = Marker("RampFoot", shuttle, new Vector3(0f, 0f, -7.3f));

        Vector3 toFoot = rampFoot.localPosition - hatch.localPosition;
        float length = toFoot.magnitude;
        float angle = -Mathf.Atan2(-toFoot.y, -toFoot.z) * Mathf.Rad2Deg;
        rampPivot = Marker("RampPivot", shuttle, hatch.localPosition);
        rampPivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
        Block("RampPlate", PrimitiveType.Cube, rampPivot, new Vector3(0f, 0f, -length * 0.5f), new Vector3(1.6f, 0.12f, length), ramp);
        Block("RampRailLeft", PrimitiveType.Cube, rampPivot, new Vector3(-0.78f, 0.12f, -length * 0.5f), new Vector3(0.06f, 0.18f, length), rail);
        Block("RampRailRight", PrimitiveType.Cube, rampPivot, new Vector3(0.78f, 0.12f, -length * 0.5f), new Vector3(0.06f, 0.18f, length), rail);

        SetPlumes(0.25f);
    }

    // Seen through the blast doors once they open. Sprites are unlit, so no lights needed.
    void BuildSpaceView()
    {
        spaceView = Marker("SpaceView", transform, new Vector3(0f, 18f, 175f));

        Texture2D stars = FlowUI.Starfield();
        SpriteRenderer backdrop = new GameObject("Stars").AddComponent<SpriteRenderer>();
        backdrop.transform.SetParent(spaceView, false);
        backdrop.sprite = Sprite.Create(stars, new Rect(0f, 0f, stars.width, stars.height), new Vector2(0.5f, 0.5f), stars.width);
        backdrop.transform.localScale = new Vector3(320f, 180f, 1f);
        backdrop.sortingOrder = 0;

        LevelInfo info = level ?? LevelCatalog.Levels[0];
        Texture2D planetTexture = FlowUI.Planet(info.planetSurface, info.planetBands, info.planetAtmosphere, info.planetRing, 31 + info.number);
        SpriteRenderer planet = new GameObject("GasGiant").AddComponent<SpriteRenderer>();
        planet.transform.SetParent(spaceView, false);
        planet.sprite = Sprite.Create(planetTexture, new Rect(0f, 0f, planetTexture.width, planetTexture.height), new Vector2(0.5f, 0.5f), planetTexture.width);
        planet.transform.localPosition = new Vector3(10f, -8f, -8f);
        planet.transform.localScale = new Vector3(55f, 55f, 1f);
        planet.sortingOrder = 1;

        spaceView.gameObject.SetActive(false);
    }

    static Transform Marker(string name, Transform parent, Vector3 localPosition)
    {
        Transform marker = new GameObject(name).transform;
        marker.SetParent(parent, false);
        marker.localPosition = localPosition;
        return marker;
    }

    static Transform Block(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 scale, Material material, Vector3 euler = default)
    {
        GameObject block = GameObject.CreatePrimitive(type);
        block.name = name;

        // Visual only: the bunny walks through on a scripted path.
        Collider blockCollider = block.GetComponent<Collider>();
        if (blockCollider != null)
        {
            Destroy(blockCollider);
        }

        block.transform.SetParent(parent, false);
        block.transform.localPosition = localPosition;
        block.transform.localRotation = Quaternion.Euler(euler);
        block.transform.localScale = scale;
        block.GetComponent<MeshRenderer>().sharedMaterial = material;
        return block.transform;
    }
}
