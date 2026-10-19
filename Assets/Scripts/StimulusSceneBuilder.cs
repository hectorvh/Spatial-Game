using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the hollow gray frame and red sphere for a question token
/// from that question's spatialConfig (or a relation inferred from the correct answer).
/// </summary>
public class StimulusSceneBuilder : MonoBehaviour
{
    const float BeamThickness = 0.085f;
    const float BorderThickness = 0.12f;

    static readonly Color FrameFill = new Color(0.62f, 0.63f, 0.66f, 1f);
    static readonly Color FrameBorder = new Color(0.14f, 0.15f, 0.17f, 1f);
    static readonly Color SphereColor = new Color(0.86f, 0.10f, 0.12f, 1f);
    static readonly Color SphereEmission = new Color(0.45f, 0.02f, 0.03f, 1f);

    static Material frameFillMaterial;
    static Material frameBorderMaterial;
    static Material sphereMaterial;

    public void Build(Question question)
    {
        SpatialConfig config = SpatialConfig.Resolve(question);
        Vector3 boxSize = config.boxDimensions.ToVector3();

        Transform boxRoot = new GameObject("HollowBox").transform;
        boxRoot.SetParent(transform, false);
        boxRoot.localPosition = Vector3.zero;
        boxRoot.localRotation = Quaternion.identity;
        boxRoot.localScale = Vector3.one;

        BuildFrame(boxRoot, boxSize);

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "RedSphere";
        sphere.transform.SetParent(boxRoot, false);
        sphere.transform.localPosition = config.sphereLocalPosition;
        sphere.transform.localRotation = Quaternion.identity;
        sphere.transform.localScale = config.sphereScale;
        StripCollider(sphere);

        Renderer sphereRenderer = sphere.GetComponent<Renderer>();
        if (sphereRenderer != null)
        {
            sphereRenderer.sharedMaterial = GetSphereMaterial();
        }

        DynamicStimulusAnimator.Attach(sphere, config);
    }

    static void BuildFrame(Transform parent, Vector3 size)
    {
        float hx = size.x * 0.5f;
        float hy = size.y * 0.5f;
        float hz = size.z * 0.5f;

        // 12 edges of a rectangular frame. Dark outer beam + lighter inner beam.
        AddEdge(parent, new Vector3(0f, hy, hz), new Vector3(size.x, BeamThickness, BeamThickness));
        AddEdge(parent, new Vector3(0f, hy, -hz), new Vector3(size.x, BeamThickness, BeamThickness));
        AddEdge(parent, new Vector3(0f, -hy, hz), new Vector3(size.x, BeamThickness, BeamThickness));
        AddEdge(parent, new Vector3(0f, -hy, -hz), new Vector3(size.x, BeamThickness, BeamThickness));

        AddEdge(parent, new Vector3(hx, 0f, hz), new Vector3(BeamThickness, size.y, BeamThickness));
        AddEdge(parent, new Vector3(-hx, 0f, hz), new Vector3(BeamThickness, size.y, BeamThickness));
        AddEdge(parent, new Vector3(hx, 0f, -hz), new Vector3(BeamThickness, size.y, BeamThickness));
        AddEdge(parent, new Vector3(-hx, 0f, -hz), new Vector3(BeamThickness, size.y, BeamThickness));

        AddEdge(parent, new Vector3(hx, hy, 0f), new Vector3(BeamThickness, BeamThickness, size.z));
        AddEdge(parent, new Vector3(-hx, hy, 0f), new Vector3(BeamThickness, BeamThickness, size.z));
        AddEdge(parent, new Vector3(hx, -hy, 0f), new Vector3(BeamThickness, BeamThickness, size.z));
        AddEdge(parent, new Vector3(-hx, -hy, 0f), new Vector3(BeamThickness, BeamThickness, size.z));
    }

    static void AddEdge(Transform parent, Vector3 localPosition, Vector3 innerSize)
    {
        Vector3 borderSize = new Vector3(
            Mathf.Max(innerSize.x, BorderThickness) + (innerSize.x > BeamThickness * 1.5f ? 0f : BorderThickness - BeamThickness),
            Mathf.Max(innerSize.y, BorderThickness) + (innerSize.y > BeamThickness * 1.5f ? 0f : BorderThickness - BeamThickness),
            Mathf.Max(innerSize.z, BorderThickness) + (innerSize.z > BeamThickness * 1.5f ? 0f : BorderThickness - BeamThickness)
        );

        // Stretch the dark border only on the thin axes so long edges stay a dark outline.
        if (innerSize.x > innerSize.y && innerSize.x > innerSize.z)
        {
            borderSize = new Vector3(innerSize.x, BorderThickness, BorderThickness);
        }
        else if (innerSize.y > innerSize.x && innerSize.y > innerSize.z)
        {
            borderSize = new Vector3(BorderThickness, innerSize.y, BorderThickness);
        }
        else
        {
            borderSize = new Vector3(BorderThickness, BorderThickness, innerSize.z);
        }

        CreateBeam(parent, "EdgeBorder", localPosition, borderSize, GetFrameBorderMaterial());
        CreateBeam(parent, "EdgeFill", localPosition, innerSize, GetFrameFillMaterial());
    }

    static void CreateBeam(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material)
    {
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = name;
        beam.transform.SetParent(parent, false);
        beam.transform.localPosition = localPosition;
        beam.transform.localRotation = Quaternion.identity;
        beam.transform.localScale = scale;
        StripCollider(beam);

        Renderer renderer = beam.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    static void StripCollider(GameObject primitive)
    {
        Collider collider = primitive.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Object.Destroy(collider);
        }
    }

    static Material GetFrameFillMaterial()
    {
        if (frameFillMaterial == null)
        {
            frameFillMaterial = CreateLitMaterial("StimulusFrameFill", FrameFill, Color.black);
        }

        return frameFillMaterial;
    }

    static Material GetFrameBorderMaterial()
    {
        if (frameBorderMaterial == null)
        {
            frameBorderMaterial = CreateLitMaterial("StimulusFrameBorder", FrameBorder, Color.black);
        }

        return frameBorderMaterial;
    }

    static Material GetSphereMaterial()
    {
        if (sphereMaterial == null)
        {
            sphereMaterial = CreateLitMaterial("StimulusSphere", SphereColor, SphereEmission);
        }

        return sphereMaterial;
    }

    static Material CreateLitMaterial(string name, Color color, Color emission)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        Material material = new Material(shader != null ? shader : Shader.Find("Standard"))
        {
            name = name
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        material.color = color;

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.45f);
        }

        if (emission != Color.black && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
        }

        return material;
    }

    static readonly Color HudEdge = new Color(0.129f, 0.145f, 0.161f, 1f);
    static readonly Color HudFace = new Color(0.620f, 0.678f, 0.722f, 0.40f);
    static readonly Color HudSphere = new Color(0.898f, 0.145f, 0.129f, 1f);

    static Material hudEdgeMaterial;
    static Material hudFaceMaterial;
    static Material hudSphereMaterial;

    const float HudEdgeThickness = 0.09f;

    /// <summary>
    /// Shadowless unlit box + sphere for the HUD render-texture studio.
    /// Uses the same spatialConfig as the on-track token.
    /// </summary>
    public static void BuildHudPreview(Transform parent, Question question, int layer)
    {
        SpatialConfig config = SpatialConfig.Resolve(question);
        Vector3 boxSize = config.boxDimensions.ToVector3();

        Transform boxRoot = new GameObject("PreviewBox").transform;
        boxRoot.SetParent(parent, false);
        boxRoot.localPosition = Vector3.zero;
        boxRoot.localRotation = Quaternion.identity;
        boxRoot.localScale = Vector3.one;
        ApplyPreviewLayer(boxRoot.gameObject, layer);

        BuildPreviewFaces(boxRoot, boxSize, layer);
        BuildPreviewEdges(boxRoot, boxSize, layer);

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "RedSphereMesh";
        sphere.transform.SetParent(boxRoot, false);
        sphere.transform.localPosition = config.sphereLocalPosition;
        sphere.transform.localRotation = Quaternion.identity;
        sphere.transform.localScale = config.sphereScale;
        StripCollider(sphere);
        ApplyPreviewRenderer(sphere, GetHudSphereMaterial(), layer);

        // The Box-View is rebuilt from JSON, so it needs its own driver to keep moving.
        DynamicStimulusAnimator.Attach(sphere, config);
    }

    static void BuildPreviewFaces(Transform parent, Vector3 size, int layer)
    {
        float hx = size.x * 0.5f;
        float hy = size.y * 0.5f;
        float hz = size.z * 0.5f;
        Material face = GetHudFaceMaterial();

        AddPreviewQuad(parent, "FaceFront", new Vector3(0f, 0f, -hz), Quaternion.Euler(0f, 180f, 0f), new Vector3(size.x, size.y, 1f), face, layer);
        AddPreviewQuad(parent, "FaceBack", new Vector3(0f, 0f, hz), Quaternion.identity, new Vector3(size.x, size.y, 1f), face, layer);
        AddPreviewQuad(parent, "FaceLeft", new Vector3(-hx, 0f, 0f), Quaternion.Euler(0f, -90f, 0f), new Vector3(size.z, size.y, 1f), face, layer);
        AddPreviewQuad(parent, "FaceRight", new Vector3(hx, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector3(size.z, size.y, 1f), face, layer);
        AddPreviewQuad(parent, "FaceTop", new Vector3(0f, hy, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(size.x, size.z, 1f), face, layer);
        AddPreviewQuad(parent, "FaceBottom", new Vector3(0f, -hy, 0f), Quaternion.Euler(-90f, 0f, 0f), new Vector3(size.x, size.z, 1f), face, layer);
    }

    static void BuildPreviewEdges(Transform parent, Vector3 size, int layer)
    {
        float hx = size.x * 0.5f;
        float hy = size.y * 0.5f;
        float hz = size.z * 0.5f;
        float t = HudEdgeThickness;
        Material edge = GetHudEdgeMaterial();

        AddPreviewBeam(parent, new Vector3(0f, hy, hz), new Vector3(size.x, t, t), edge, layer);
        AddPreviewBeam(parent, new Vector3(0f, hy, -hz), new Vector3(size.x, t, t), edge, layer);
        AddPreviewBeam(parent, new Vector3(0f, -hy, hz), new Vector3(size.x, t, t), edge, layer);
        AddPreviewBeam(parent, new Vector3(0f, -hy, -hz), new Vector3(size.x, t, t), edge, layer);

        AddPreviewBeam(parent, new Vector3(hx, 0f, hz), new Vector3(t, size.y, t), edge, layer);
        AddPreviewBeam(parent, new Vector3(-hx, 0f, hz), new Vector3(t, size.y, t), edge, layer);
        AddPreviewBeam(parent, new Vector3(hx, 0f, -hz), new Vector3(t, size.y, t), edge, layer);
        AddPreviewBeam(parent, new Vector3(-hx, 0f, -hz), new Vector3(t, size.y, t), edge, layer);

        AddPreviewBeam(parent, new Vector3(hx, hy, 0f), new Vector3(t, t, size.z), edge, layer);
        AddPreviewBeam(parent, new Vector3(-hx, hy, 0f), new Vector3(t, t, size.z), edge, layer);
        AddPreviewBeam(parent, new Vector3(hx, -hy, 0f), new Vector3(t, t, size.z), edge, layer);
        AddPreviewBeam(parent, new Vector3(-hx, -hy, 0f), new Vector3(t, t, size.z), edge, layer);
    }

    static void AddPreviewQuad(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, Vector3 scale, Material material, int layer)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = localPosition;
        quad.transform.localRotation = localRotation;
        quad.transform.localScale = scale;
        StripCollider(quad);
        ApplyPreviewRenderer(quad, material, layer);
    }

    static void AddPreviewBeam(Transform parent, Vector3 localPosition, Vector3 scale, Material material, int layer)
    {
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = "FrameBoxMesh";
        beam.transform.SetParent(parent, false);
        beam.transform.localPosition = localPosition;
        beam.transform.localRotation = Quaternion.identity;
        beam.transform.localScale = scale;
        StripCollider(beam);
        ApplyPreviewRenderer(beam, material, layer);
    }

    static void ApplyPreviewRenderer(GameObject target, Material material, int layer)
    {
        ApplyPreviewLayer(target, layer);
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.allowOcclusionWhenDynamic = false;
    }

    static void ApplyPreviewLayer(GameObject target, int layer)
    {
        if (layer >= 0)
        {
            target.layer = layer;
        }
    }

    static Material GetHudEdgeMaterial()
    {
        if (hudEdgeMaterial == null)
        {
            hudEdgeMaterial = CreateUnlitMaterial("HudBoxEdge", HudEdge, false);
        }

        return hudEdgeMaterial;
    }

    static Material GetHudFaceMaterial()
    {
        if (hudFaceMaterial == null)
        {
            hudFaceMaterial = CreateUnlitMaterial("HudBoxFace", HudFace, true);
        }

        return hudFaceMaterial;
    }

    static Material GetHudSphereMaterial()
    {
        if (hudSphereMaterial == null)
        {
            hudSphereMaterial = CreateUnlitMaterial("HudSphere", HudSphere, false);
        }

        return hudSphereMaterial;
    }

    static Material CreateUnlitMaterial(string name, Color color, bool transparent)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find(transparent ? "Unlit/Transparent" : "Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader)
        {
            name = name
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        material.color = color;

        if (transparent)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        return material;
    }
}
