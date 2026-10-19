using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// CPU side of the curved-world vertex bend (Resources/CurvedWorldLit.shader).
/// Only visuals bend: transforms and colliders stay on the straight track, so lanes,
/// shots and triggers are unaffected. Offset() must match CurveBend() in the shader.
/// </summary>
public static class CurvedWorld
{
    public const string ShaderResource = "CurvedWorldLit";

    static readonly int OriginZId = Shader.PropertyToID("_CurveOriginZ");
    static readonly int StrengthXId = Shader.PropertyToID("_CurveStrengthX");
    static readonly int StrengthYId = Shader.PropertyToID("_CurveStrengthY");

    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    static readonly int MetallicGlossMapId = Shader.PropertyToID("_MetallicGlossMap");
    static readonly int CullId = Shader.PropertyToID("_Cull");

    static readonly string[] TextureProperties = { "baseColorTexture", "_BaseMap", "_MainTex" };
    static readonly string[] ColorProperties = { "_BaseColor", "_Color", "baseColorFactor" };

    static readonly Dictionary<Material, Material> converted = new Dictionary<Material, Material>();
    static Shader curvedShader;
    static bool shaderLookupDone;

    public static float OriginZ { get; private set; }
    public static float StrengthX { get; private set; }
    public static float StrengthY { get; private set; }

    public static Shader CurvedShader
    {
        get
        {
            if (!shaderLookupDone)
            {
                shaderLookupDone = true;
                curvedShader = Resources.Load<Shader>(ShaderResource);
                if (curvedShader == null || !curvedShader.isSupported)
                {
                    Debug.LogWarning("CurvedWorld: shader Resources/" + ShaderResource + " unavailable. World stays flat.");
                    curvedShader = null;
                }
            }

            return curvedShader;
        }
    }

    public static void SetGlobals(float originZ, float strengthX, float strengthY)
    {
        OriginZ = originZ;
        StrengthX = strengthX;
        StrengthY = strengthY;
        Shader.SetGlobalFloat(OriginZId, originZ);
        Shader.SetGlobalFloat(StrengthXId, strengthX);
        Shader.SetGlobalFloat(StrengthYId, strengthY);
    }

    public static Vector3 Offset(Vector3 worldPosition)
    {
        float d = Mathf.Max(0f, worldPosition.z - OriginZ);
        float d2 = d * d;
        return new Vector3(StrengthX * d2, -StrengthY * d2, 0f);
    }

    /// <summary>Where a straight-track world point is drawn on screen.</summary>
    public static Vector3 Bend(Vector3 worldPosition)
    {
        return worldPosition + Offset(worldPosition);
    }

    public static Material CreateMaterial(string name, Color baseColor, Color emission, float smoothness = 0.3f, float metallic = 0f)
    {
        Shader shader = CurvedShader;
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        Material material = new Material(shader) { name = name };
        material.SetColor(BaseColorId, baseColor);
        if (material.HasProperty(SmoothnessId))
        {
            material.SetFloat(SmoothnessId, smoothness);
        }

        if (material.HasProperty(MetallicId))
        {
            material.SetFloat(MetallicId, metallic);
        }

        if (emission != Color.black && material.HasProperty(EmissionColorId))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor(EmissionColorId, emission);
        }

        return material;
    }

    /// <summary>
    /// Swaps opaque materials under root to the curved shader and makes TMP labels follow the bend.
    /// Transparent and particle materials are left as they are.
    /// </summary>
    public static void Apply(GameObject root)
    {
        if (root == null || CurvedShader == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
            {
                continue;
            }

            if (renderer.GetComponent<TMP_Text>() != null)
            {
                if (renderer.GetComponent<CurvedWorldFollower>() == null)
                {
                    renderer.gameObject.AddComponent<CurvedWorldFollower>();
                }

                continue;
            }

            if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer || renderer is TrailRenderer || renderer is LineRenderer))
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int m = 0; m < materials.Length; m++)
            {
                Material bent = Convert(materials[m]);
                if (bent != materials[m])
                {
                    materials[m] = bent;
                    changed = true;
                }
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }
    }

    static Material Convert(Material source)
    {
        if (source == null || source.shader == CurvedShader || IsTransparent(source))
        {
            return source;
        }

        if (converted.TryGetValue(source, out Material cached) && cached != null)
        {
            return cached;
        }

        Material bent = new Material(CurvedShader) { name = source.name + " (Curved)" };

        for (int i = 0; i < TextureProperties.Length; i++)
        {
            if (CopyTexture(source, TextureProperties[i], bent, BaseMapId))
            {
                break;
            }
        }

        // glTF metalness lives in the B channel of this map. Without it a rail
        // whose metallicFactor is 1 is drawn as pure metal and the albedo disappears.
        CopyTexture(source, "metallicRoughnessTexture", bent, MetallicGlossMapId);

        for (int i = 0; i < ColorProperties.Length; i++)
        {
            if (!TryGetColor(source, ColorProperties[i], out Color tint))
            {
                continue;
            }

            // Shader Graph leaves baseColorFactor at (0,0,0,0) until it is assigned.
            if (tint.a <= 0f && tint.maxColorComponent <= 0f)
            {
                tint = Color.white;
            }

            bent.SetColor(BaseColorId, tint);
            break;
        }

        if (source.HasFloat("_Cull"))
        {
            bent.SetFloat(CullId, source.GetFloat("_Cull"));
        }

        if (source.HasColor("_EmissionColor") && source.IsKeywordEnabled("_EMISSION"))
        {
            bent.SetColor(EmissionColorId, source.GetColor("_EmissionColor"));
        }
        else if (source.HasColor("emissiveFactor"))
        {
            bent.SetColor(EmissionColorId, source.GetColor("emissiveFactor"));
        }

        if (source.HasFloat("_Smoothness"))
        {
            bent.SetFloat(SmoothnessId, source.GetFloat("_Smoothness"));
        }
        else if (source.HasFloat("roughnessFactor"))
        {
            bent.SetFloat(SmoothnessId, 1f - source.GetFloat("roughnessFactor"));
        }

        if (source.HasFloat("_Metallic"))
        {
            bent.SetFloat(MetallicId, source.GetFloat("_Metallic"));
        }
        else if (source.HasFloat("metallicFactor"))
        {
            bent.SetFloat(MetallicId, source.GetFloat("metallicFactor"));
        }

        converted[source] = bent;
        return bent;
    }

    static bool CopyTexture(Material source, string property, Material bent, int destination)
    {
        if (!source.HasProperty(property))
        {
            return false;
        }

        Texture texture = source.GetTexture(property);
        if (texture == null)
        {
            return false;
        }

        bent.SetTexture(destination, texture);

        Vector2 scale = source.GetTextureScale(property);
        Vector2 offset = source.GetTextureOffset(property);
        string scaleProperty = property + "_ST";
        if (source.HasProperty(scaleProperty))
        {
            Vector4 scaleOffset = source.GetVector(scaleProperty);
            if (scaleOffset.x != 0f || scaleOffset.y != 0f)
            {
                scale = new Vector2(scaleOffset.x, scaleOffset.y);
                offset = new Vector2(scaleOffset.z, scaleOffset.w);
            }
        }

        bent.SetTextureScale(destination, scale);
        bent.SetTextureOffset(destination, offset);
        return true;
    }

    static bool TryGetColor(Material source, string property, out Color color)
    {
        color = Color.white;
        if (!source.HasProperty(property))
        {
            return false;
        }

        if (source.HasColor(property))
        {
            color = source.GetColor(property);
            return true;
        }

        Vector4 value = source.GetVector(property);
        color = new Color(value.x, value.y, value.z, value.w);
        return true;
    }

    static bool IsTransparent(Material material)
    {
        if (material.renderQueue >= 2450)
        {
            return true;
        }

        return material.HasFloat("_Surface") && material.GetFloat("_Surface") > 0.5f;
    }
}
