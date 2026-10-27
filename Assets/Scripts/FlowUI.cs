using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Runtime uGUI builders for the level-select, cutscene and level-clear screens.
/// Same rounded panels and palette as GameHUD.
/// </summary>
public static class FlowUI
{
    public static readonly Color PanelFill = new Color(0.149f, 0.169f, 0.220f, 0.96f);
    public static readonly Color Border = new Color(0.310f, 0.659f, 0.776f, 1f);
    public static readonly Color Caption = new Color(0.373f, 0.765f, 0.871f, 1f);
    public static readonly Color Accent = new Color(0.153f, 0.706f, 0.286f, 1f);
    public static readonly Color Muted = new Color(0.56f, 0.61f, 0.69f, 1f);
    public static readonly Color Locked = new Color(0.30f, 0.33f, 0.39f, 1f);
    public static readonly Color SpaceTop = new Color(0.02f, 0.03f, 0.08f, 1f);
    public static readonly Color SpaceBottom = new Color(0.07f, 0.06f, 0.15f, 1f);

    static readonly Dictionary<int, Sprite> roundedSprites = new Dictionary<int, Sprite>();
    static Sprite circleSprite;
    static Sprite triangleSprite;
    static Texture2D starfieldTexture;

    public static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventObject = new GameObject("EventSystem");
        eventObject.AddComponent<EventSystem>();
        eventObject.AddComponent<InputSystemUIInputModule>();
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        GameObject rectObject = new GameObject(name, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        return (RectTransform)rectObject.transform;
    }

    public static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    /// <summary>Full-width strip anchored to the top of the parent.</summary>
    public static void TopStrip(RectTransform rect, float top, float height, float sideInset)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(sideInset, -top - height);
        rect.offsetMax = new Vector2(-sideInset, -top);
    }

    /// <summary>Full-width strip anchored to the bottom of the parent.</summary>
    public static void BottomStrip(RectTransform rect, float bottom, float height, float sideInset)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(sideInset, bottom);
        rect.offsetMax = new Vector2(-sideInset, bottom + height);
    }

    public static Image Solid(RectTransform rect, Color color, bool raycast = true)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    /// <summary>Rounded border plus inset fill. Returns the fill so callers can tint it.</summary>
    public static Image Panel(RectTransform rect, int radius, Color border, Color fill, float thickness)
    {
        Image borderImage = rect.gameObject.AddComponent<Image>();
        borderImage.sprite = RoundedSprite(radius);
        borderImage.type = Image.Type.Sliced;
        borderImage.color = border;

        RectTransform fillRect = Rect("Fill", rect);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(thickness, thickness);
        fillRect.offsetMax = new Vector2(-thickness, -thickness);

        Image fillImage = fillRect.gameObject.AddComponent<Image>();
        fillImage.sprite = RoundedSprite(Mathf.Max(4, radius - (int)thickness));
        fillImage.type = Image.Type.Sliced;
        fillImage.color = fill;
        fillImage.raycastTarget = false;
        return fillImage;
    }

    public static TextMeshProUGUI Label(RectTransform parent, string name, string text, float size, Color color, FontStyles style)
    {
        RectTransform rect = Rect(name, parent);
        Stretch(rect);

        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    public static Button Button(RectTransform rect, string text, float fontSize, Color fill, UnityAction onClick, out TextMeshProUGUI label)
    {
        Image fillImage = Panel(rect, 24, Border, fill, 4f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = fillImage;
        // Screens read arrow keys themselves; uGUI navigation would move selection too.
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.78f, 0.82f, 0.86f, 1f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
        colors.colorMultiplier = 1.2f;
        button.colors = colors;

        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        label = Label(rect, "Label", text, fontSize, Color.white, FontStyles.Bold);
        label.characterSpacing = 4f;
        return button;
    }

    public static Sprite RoundedSprite(int radius)
    {
        if (roundedSprites.TryGetValue(radius, out Sprite cached) && cached != null)
        {
            return cached;
        }

        int size = (radius * 2) + 8;
        Texture2D texture = NewTexture(size, size);
        Color[] pixels = new Color[size * size];
        float half = size * 0.5f;
        float inner = half - radius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - inner, 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - inner, 0f);
                float distance = Mathf.Sqrt((dx * dx) + (dy * dy)) - radius;
                pixels[(y * size) + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - distance));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        Vector4 border = new Vector4(radius, radius, radius, radius);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        roundedSprites[radius] = sprite;
        return sprite;
    }

    public static Sprite CircleSprite()
    {
        if (circleSprite != null)
        {
            return circleSprite;
        }

        const int size = 64;
        Texture2D texture = NewTexture(size, size);
        Color[] pixels = new Color[size * size];
        float radius = size * 0.5f - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - (size * 0.5f);
                float dy = y + 0.5f - (size * 0.5f);
                float distance = Mathf.Sqrt((dx * dx) + (dy * dy)) - radius;
                pixels[(y * size) + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - distance));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        return circleSprite;
    }

    /// <summary>Right-pointing triangle. Flip with localScale.x = -1 for a left arrow.</summary>
    public static Sprite TriangleSprite()
    {
        if (triangleSprite != null)
        {
            return triangleSprite;
        }

        const int size = 96;
        Texture2D texture = NewTexture(size, size);
        Color[] pixels = new Color[size * size];
        Vector2 a = new Vector2(size * 0.28f, size * 0.16f);
        Vector2 b = new Vector2(size * 0.28f, size * 0.84f);
        Vector2 c = new Vector2(size * 0.80f, size * 0.50f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d1 = Edge(p, a, b);
                float d2 = Edge(p, b, c);
                float d3 = Edge(p, c, a);
                bool inside = !((d1 < 0f || d2 < 0f || d3 < 0f) && (d1 > 0f || d2 > 0f || d3 > 0f));
                pixels[(y * size) + x] = new Color(1f, 1f, 1f, inside ? 1f : 0f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        triangleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        return triangleSprite;
    }

    /// <summary>Vertical space gradient with scattered stars. Repeats on X so it can drift.</summary>
    public static Texture2D Starfield()
    {
        if (starfieldTexture != null)
        {
            return starfieldTexture;
        }

        const int width = 512;
        const int height = 512;
        starfieldTexture = NewTexture(width, height);
        starfieldTexture.wrapMode = TextureWrapMode.Repeat;

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            Color row = Color.Lerp(SpaceBottom, SpaceTop, y / (float)(height - 1));
            for (int x = 0; x < width; x++)
            {
                pixels[(y * width) + x] = row;
            }
        }

        System.Random random = new System.Random(9);
        for (int i = 0; i < 420; i++)
        {
            int sx = random.Next(width);
            int sy = random.Next(height);
            float brightness = 0.35f + ((float)random.NextDouble() * 0.65f);
            bool big = random.NextDouble() < 0.08;
            Color tint = Color.Lerp(new Color(0.7f, 0.8f, 1f), new Color(1f, 0.92f, 0.8f), (float)random.NextDouble());
            int reach = big ? 1 : 0;
            for (int oy = -reach; oy <= reach; oy++)
            {
                for (int ox = -reach; ox <= reach; ox++)
                {
                    int px = (sx + ox + width) % width;
                    int py = Mathf.Clamp(sy + oy, 0, height - 1);
                    float falloff = (ox == 0 && oy == 0) ? 1f : 0.35f;
                    int index = (py * width) + px;
                    pixels[index] = Color.Lerp(pixels[index], tint, brightness * falloff);
                }
            }
        }

        starfieldTexture.SetPixels(pixels);
        starfieldTexture.Apply();
        return starfieldTexture;
    }

    /// <summary>Banded planet with a soft atmosphere and an optional tilted ring, for level cards.</summary>
    public static Texture2D Planet(Color surface, Color bands, Color atmosphere, bool ring, int seed)
    {
        const int size = 256;
        Texture2D texture = NewTexture(size, size);
        Color[] pixels = new Color[size * size];

        System.Random random = new System.Random(seed);
        float phase = (float)random.NextDouble() * 10f;
        float frequency = 7f + ((float)random.NextDouble() * 5f);
        float radius = size * 0.30f;
        float centre = size * 0.5f;
        Vector3 light = new Vector3(-0.55f, 0.5f, 0.67f).normalized;
        float tilt = -18f * Mathf.Deg2Rad;
        float cosT = Mathf.Cos(tilt);
        float sinT = Mathf.Sin(tilt);
        Color ringColor = Color.Lerp(surface, Color.white, 0.35f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - centre) / radius;
                float dy = (y + 0.5f - centre) / radius;
                float d2 = (dx * dx) + (dy * dy);

                // Ring plane: rotate, then squash so it reads as a tilted ellipse.
                float rx = (dx * cosT) - (dy * sinT);
                float ry = ((dx * sinT) + (dy * cosT)) / 0.26f;
                float ringDistance = Mathf.Sqrt((rx * rx) + (ry * ry));
                float ringAlpha = 0f;
                if (ring && ringDistance > 1.3f && ringDistance < 1.95f)
                {
                    float stripes = 0.55f + (0.45f * Mathf.Sin(ringDistance * 38f));
                    float edge = Mathf.Clamp01((ringDistance - 1.3f) * 8f) * Mathf.Clamp01((1.95f - ringDistance) * 8f);
                    ringAlpha = 0.75f * stripes * edge;
                }

                bool ringInFront = ry < 0f;
                Color colour = new Color(0f, 0f, 0f, 0f);

                if (d2 <= 1f)
                {
                    float z = Mathf.Sqrt(1f - d2);
                    float band = 0.5f + (0.5f * Mathf.Sin((dy * frequency) + (Mathf.Sin((dx * 3f) + phase) * 0.7f) + phase));
                    Color body = Color.Lerp(surface, bands, band * band);
                    float lit = Mathf.Clamp01(0.18f + (0.95f * Vector3.Dot(new Vector3(dx, dy, z), light)));
                    body *= lit;
                    body += atmosphere * Mathf.Pow(1f - z, 3f) * 0.6f;
                    body.a = Mathf.Clamp01((1f - Mathf.Sqrt(d2)) * radius * 0.9f);
                    colour = body;

                    if (ringAlpha > 0f && ringInFront)
                    {
                        colour = Color.Lerp(colour, ringColor, ringAlpha);
                        colour.a = 1f;
                    }
                }
                else
                {
                    float glow = Mathf.Exp(-(Mathf.Sqrt(d2) - 1f) * 9f) * 0.55f;
                    colour = new Color(atmosphere.r, atmosphere.g, atmosphere.b, glow);
                    if (ringAlpha > 0f)
                    {
                        colour = Color.Lerp(colour, ringColor, ringAlpha);
                        colour.a = Mathf.Max(glow, ringAlpha);
                    }
                }

                pixels[(y * size) + x] = colour;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    static Texture2D NewTexture(int width, int height)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
    }

    static float Edge(Vector2 p, Vector2 a, Vector2 b)
    {
        return ((p.x - b.x) * (a.y - b.y)) - ((a.x - b.x) * (p.y - b.y));
    }
}
