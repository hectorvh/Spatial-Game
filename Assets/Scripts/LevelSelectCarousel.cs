using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Spec Feature 8: horizontal level carousel. One card per level in a ScrollRect with a
/// HorizontalLayoutGroup, snapped by CarouselPageSnap. Drag, swipe, arrow buttons, page
/// dots, and keyboard (Left / Right or A / D, Enter or Space to start).
/// </summary>
public class LevelSelectCarousel : MonoBehaviour
{
    const float CardWidth = 750f;
    const float CardHeight = 980f;
    const float CardSpacing = 56f;
    const float AreaBottom = 130f;
    const float AreaTop = 220f;

    static readonly Color SelectedMode = new Color(0.208f, 0.545f, 0.824f, 1f);
    static readonly Color IdleMode = new Color(0.11f, 0.13f, 0.17f, 1f);

    struct ModeButtons
    {
        public Image practiceFill;
        public Image assessmentFill;
        public TextMeshProUGUI practiceLabel;
        public TextMeshProUGUI assessmentLabel;
    }

    LevelInfo[] levels;
    Action<LevelInfo, GameMode> onConfirm;
    GameMode selectedMode;
    bool showModeChoice;
    string modeNote;

    RectTransform viewport;
    RectTransform content;
    HorizontalLayoutGroup layout;
    CarouselPageSnap snap;
    RawImage starfield;
    Button prevArrow;
    Button nextArrow;
    readonly List<RectTransform> cards = new List<RectTransform>();
    readonly List<CanvasGroup> cardGroups = new List<CanvasGroup>();
    readonly List<Image> dots = new List<Image>();
    readonly List<ModeButtons> modeButtons = new List<ModeButtons>();
    float lastViewportWidth = -1f;
    float starOffset;
    int pendingPage = -1;
    bool confirmed;

    public static LevelSelectCarousel Create(
        RectTransform parent,
        LevelInfo[] levels,
        GameMode mode,
        bool showModeChoice,
        string modeNote,
        Action<LevelInfo, GameMode> onConfirm)
    {
        RectTransform root = FlowUI.Rect("LevelSelect", parent);
        FlowUI.Stretch(root);

        LevelSelectCarousel carousel = root.gameObject.AddComponent<LevelSelectCarousel>();
        carousel.levels = levels;
        carousel.selectedMode = mode;
        carousel.showModeChoice = showModeChoice;
        carousel.modeNote = modeNote;
        carousel.onConfirm = onConfirm;
        carousel.Build();
        return carousel;
    }

    void Build()
    {
        RectTransform root = (RectTransform)transform;

        RectTransform background = FlowUI.Rect("Starfield", root);
        FlowUI.Stretch(background);
        starfield = background.gameObject.AddComponent<RawImage>();
        starfield.texture = FlowUI.Starfield();

        RectTransform title = FlowUI.Rect("Title", root);
        FlowUI.TopStrip(title, 400f, 96f, 40f);
        TextMeshProUGUI titleLabel = FlowUI.Label(title, "Text", "ASTRO BUNNY", 78f, FlowUI.Caption, FontStyles.Bold);
        titleLabel.characterSpacing = 16f;

        //RectTransform subtitle = FlowUI.Rect("Subtitle", root);
        //FlowUI.TopStrip(subtitle, 250f, 48f, 40f);
        //FlowUI.Label(subtitle, "Text", "Choose your mission", 34f, FlowUI.Muted, FontStyles.Normal);

        BuildScroller(root);
        BuildArrows(root);
        BuildDots(root);

        //RectTransform hint = FlowUI.Rect("Hint", root);
        //FlowUI.BottomStrip(hint, 26f, 40f, 40f);
        //string hintText = "Drag or use Left / Right to browse.   Enter to start.";
        //if (showModeChoice)
        //{
        //    hintText += "   Up / Down to change mode.";
        //}

        //TextMeshProUGUI hintLabel = FlowUI.Label(hint, "Text", hintText, 24f, FlowUI.Muted, FontStyles.Normal);
        //hintLabel.enableAutoSizing = true;
        //hintLabel.fontSizeMin = 16f;
        //hintLabel.fontSizeMax = 24f;

        snap.Configure(levels.Length);
        pendingPage = FirstUnlockedIndex();
        RefreshModeButtons();
        RefreshPageIndicators(pendingPage);
    }

    void BuildScroller(RectTransform root)
    {
        RectTransform area = FlowUI.Rect("Carousel", root);
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(0f, AreaBottom);
        area.offsetMax = new Vector2(0f, -AreaTop);
        FlowUI.Solid(area, new Color(0f, 0f, 0f, 0f));

        ScrollRect scroll = area.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.elasticity = 0.12f;
        scroll.inertia = false;
        scroll.scrollSensitivity = 0f;

        viewport = FlowUI.Rect("Viewport", area);
        FlowUI.Stretch(viewport);

        content = FlowUI.Rect("Content", viewport);
        content.anchorMin = new Vector2(0f, 0.5f);
        content.anchorMax = new Vector2(0f, 0.5f);
        content.pivot = new Vector2(0f, 0.5f);
        content.sizeDelta = new Vector2(CardWidth, CardHeight);

        layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = CardSpacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        scroll.viewport = viewport;
        scroll.content = content;

        snap = area.gameObject.AddComponent<CarouselPageSnap>();
        snap.PageChanged += RefreshPageIndicators;

        for (int i = 0; i < levels.Length; i++)
        {
            BuildCard(levels[i], i);
        }
    }

    void BuildCard(LevelInfo level, int index)
    {
        RectTransform card = FlowUI.Rect("Card_" + level.number, content);
        card.sizeDelta = new Vector2(CardWidth, CardHeight);
        FlowUI.Panel(card, 36, level.unlocked ? FlowUI.Border : FlowUI.Locked, FlowUI.PanelFill, 6f);
        CanvasGroup group = card.gameObject.AddComponent<CanvasGroup>();

        // A tap on a side card centers it. A sideways swipe is handled by the snap
        // and does not also count as a tap.
        CarouselCardPress press = card.gameObject.AddComponent<CarouselCardPress>();
        press.snap = snap;
        press.page = index;

        RectTransform stack = FlowUI.Rect("Stack", card);
        FlowUI.Stretch(stack, 24f);
        VerticalLayoutGroup column = stack.gameObject.AddComponent<VerticalLayoutGroup>();
        column.spacing = 16f;
        column.padding = new RectOffset(8, 8, 4, 4);
        column.childAlignment = TextAnchor.MiddleCenter;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = false;
        column.childForceExpandHeight = false;

        RectTransform caption = StackItem(stack, "World", 640f, 58f);
        TextMeshProUGUI captionLabel = FlowUI.Label(caption, "Text", "LEVEL " + level.number, 38f, FlowUI.Caption, FontStyles.Bold);
        captionLabel.characterSpacing = 10f;

        RectTransform planet = StackItem(stack, "Planet", 400f, 400f);
        RawImage planetImage = planet.gameObject.AddComponent<RawImage>();
        planetImage.texture = FlowUI.Planet(level.planetSurface, level.planetBands, level.planetAtmosphere, level.planetRing, 31 + level.number);
        planetImage.raycastTarget = false;
        if (!level.unlocked)
        {
            planetImage.color = new Color(0.45f, 0.47f, 0.52f, 1f);

            RectTransform lockTag = FlowUI.Rect("LockTag", planet);
            FlowUI.Place(lockTag, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 72f));
            FlowUI.Panel(lockTag, 24, FlowUI.Locked, new Color(0.06f, 0.07f, 0.09f, 0.92f), 3f);
            TextMeshProUGUI lockLabel = FlowUI.Label(lockTag, "Text", "LOCKED", 36f, Color.white, FontStyles.Bold);
            lockLabel.characterSpacing = 6f;
        }

        AddLine(stack, "Title", level.title, 640f, 120f, 56f, Color.white, FontStyles.Bold, true);

        string status = level.unlocked
            ? "<color=#27B449>UNLOCKED</color>     BEST " + level.HighScore.ToString("000")
            : "LOCKED     Coming soon";
        AddLine(stack, "Status", status, 640f, 52f, 36f, level.unlocked ? Color.white : FlowUI.Muted, FontStyles.Bold, false);

        RectTransform modeRow = StackItem(stack, "Mode", 640f, 92f);
        if (level.unlocked && showModeChoice)
        {
            modeButtons.Add(BuildModeButtons(modeRow));
        }
        else if (level.unlocked)
        {
            FlowUI.Label(modeRow, "Text", modeNote, 34f, FlowUI.Caption, FontStyles.Bold);
        }

        RectTransform start = StackItem(stack, "Start", 640f, 120f);
        Button startButton = FlowUI.Button(
            start,
            level.unlocked ? "START" : "COMING SOON",
            50f,
            level.unlocked ? FlowUI.Accent : FlowUI.Locked,
            () => TryConfirm(index),
            out _);
        startButton.interactable = level.unlocked;

        cards.Add(card);
        cardGroups.Add(group);
    }

    RectTransform StackItem(RectTransform stack, string name, float width, float height)
    {
        RectTransform item = FlowUI.Rect(name, stack);
        LayoutElement layout = item.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.preferredHeight = height;
        layout.minWidth = width;
        layout.minHeight = height;
        return item;
    }

    void AddLine(RectTransform stack, string name, string text, float width, float height, float size, Color color, FontStyles style, bool wrap)
    {
        RectTransform line = StackItem(stack, name, width, height);
        TextMeshProUGUI label = FlowUI.Label(line, "Text", text, size, color, style);
        label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMin = size * 0.65f;
        label.fontSizeMax = size;
        label.overflowMode = TextOverflowModes.Ellipsis;
    }

    ModeButtons BuildModeButtons(RectTransform row)
    {
        ModeButtons buttons = new ModeButtons();

        RectTransform practice = FlowUI.Rect("Practice", row);
        practice.anchorMin = new Vector2(0f, 0f);
        practice.anchorMax = new Vector2(0.5f, 1f);
        practice.offsetMin = Vector2.zero;
        practice.offsetMax = new Vector2(-8f, 0f);
        Button practiceButton = FlowUI.Button(practice, "PRACTICE", 34f, IdleMode, () => SetMode(GameMode.Trial), out buttons.practiceLabel);
        buttons.practiceFill = (Image)practiceButton.targetGraphic;

        RectTransform assessment = FlowUI.Rect("Assessment", row);
        assessment.anchorMin = new Vector2(0.5f, 0f);
        assessment.anchorMax = new Vector2(1f, 1f);
        assessment.offsetMin = new Vector2(8f, 0f);
        assessment.offsetMax = Vector2.zero;
        Button assessmentButton = FlowUI.Button(assessment, "ASSESSMENT", 34f, IdleMode, () => SetMode(GameMode.StandardLevel), out buttons.assessmentLabel);
        buttons.assessmentFill = (Image)assessmentButton.targetGraphic;

        return buttons;
    }

    void BuildArrows(RectTransform root)
    {
        float centreY = (AreaBottom - AreaTop) * 0.5f;
        prevArrow = BuildArrow(root, "Prev", new Vector2(0f, 0.5f), new Vector2(70f, centreY), true, () => snap.GoTo(snap.Page - 1));
        nextArrow = BuildArrow(root, "Next", new Vector2(1f, 0.5f), new Vector2(-70f, centreY), false, () => snap.GoTo(snap.Page + 1));
    }

    Button BuildArrow(RectTransform root, string name, Vector2 anchor, Vector2 position, bool flip, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform arrow = FlowUI.Rect(name, root);
        FlowUI.Place(arrow, anchor, position, new Vector2(96f, 96f));
        Image hit = FlowUI.Solid(arrow, new Color(0f, 0f, 0f, 0f));

        RectTransform icon = FlowUI.Rect("Icon", arrow);
        FlowUI.Stretch(icon);
        icon.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
        Image iconImage = icon.gameObject.AddComponent<Image>();
        iconImage.sprite = FlowUI.TriangleSprite();
        iconImage.color = FlowUI.Caption;
        iconImage.raycastTarget = false;

        Button button = arrow.gameObject.AddComponent<Button>();
        button.targetGraphic = iconImage;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(onClick);
        hit.raycastTarget = true;
        return button;
    }

    void BuildDots(RectTransform root)
    {
        RectTransform row = FlowUI.Rect("PageDots", root);
        FlowUI.BottomStrip(row, 84f, 22f, 40f);

        HorizontalLayoutGroup dotLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        dotLayout.spacing = 18f;
        dotLayout.childAlignment = TextAnchor.MiddleCenter;
        dotLayout.childControlWidth = false;
        dotLayout.childControlHeight = false;
        dotLayout.childForceExpandWidth = false;
        dotLayout.childForceExpandHeight = false;

        for (int i = 0; i < levels.Length; i++)
        {
            RectTransform dot = FlowUI.Rect("Dot" + i, row);
            dot.sizeDelta = new Vector2(20f, 20f);
            Image image = dot.gameObject.AddComponent<Image>();
            image.sprite = FlowUI.CircleSprite();

            Button button = dot.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            int page = i;
            button.onClick.AddListener(() => snap.GoTo(page));
            dots.Add(image);
        }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        UpdateLayoutPadding();
        UpdateStarfield(dt);
        UpdateCardFocus();
        HandleKeyboard();
    }

    // Side padding of half a viewport lets the first and last cards sit in the centre.
    void UpdateLayoutPadding()
    {
        float width = viewport.rect.width;
        if (width <= 1f || Mathf.Abs(width - lastViewportWidth) < 0.5f)
        {
            return;
        }

        lastViewportWidth = width;
        int pad = Mathf.Max(0, Mathf.RoundToInt((width - CardWidth) * 0.5f));
        layout.padding = new RectOffset(pad, pad, 0, 0);
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        int page = pendingPage >= 0 ? pendingPage : snap.Page;
        pendingPage = -1;
        snap.GoTo(page, true);
        RefreshPageIndicators(snap.Page);
    }

    void UpdateStarfield(float dt)
    {
        RectTransform rect = (RectTransform)starfield.transform;
        float aspect = rect.rect.height > 1f ? rect.rect.width / rect.rect.height : 1.78f;
        starOffset = Mathf.Repeat(starOffset + (dt * 0.006f), 1f);
        starfield.uvRect = new Rect(starOffset, 0f, aspect, 1f);
    }

    void UpdateCardFocus()
    {
        float position = snap.PagePosition;
        for (int i = 0; i < cards.Count; i++)
        {
            float distance = Mathf.Clamp01(Mathf.Abs(position - i));
            cards[i].localScale = Vector3.one * Mathf.Lerp(1f, 0.86f, distance);
            cardGroups[i].alpha = Mathf.Lerp(1f, 0.45f, distance);
        }
    }

    void HandleKeyboard()
    {
        Keyboard keyboard = Keyboard.current;
        if (confirmed || keyboard == null)
        {
            return;
        }

        if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
        {
            snap.GoTo(snap.Page - 1);
        }
        else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
        {
            snap.GoTo(snap.Page + 1);
        }

        if (showModeChoice &&
            (keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame ||
             keyboard.wKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame))
        {
            SetMode(selectedMode == GameMode.Trial ? GameMode.StandardLevel : GameMode.Trial);
        }

        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
        {
            TryConfirm(snap.Page);
        }
    }

    void SetMode(GameMode mode)
    {
        selectedMode = mode;
        RefreshModeButtons();
    }

    void RefreshModeButtons()
    {
        bool practice = selectedMode == GameMode.Trial;
        for (int i = 0; i < modeButtons.Count; i++)
        {
            ModeButtons buttons = modeButtons[i];
            buttons.practiceFill.color = practice ? SelectedMode : IdleMode;
            buttons.assessmentFill.color = practice ? IdleMode : SelectedMode;
            buttons.practiceLabel.color = practice ? Color.white : FlowUI.Muted;
            buttons.assessmentLabel.color = practice ? FlowUI.Muted : Color.white;
        }
    }

    void RefreshPageIndicators(int page)
    {
        for (int i = 0; i < dots.Count; i++)
        {
            dots[i].color = i == page ? FlowUI.Caption : new Color(1f, 1f, 1f, 0.25f);
        }

        if (prevArrow != null)
        {
            prevArrow.gameObject.SetActive(page > 0);
        }

        if (nextArrow != null)
        {
            nextArrow.gameObject.SetActive(page < levels.Length - 1);
        }
    }

    void TryConfirm(int index)
    {
        if (confirmed || index < 0 || index >= levels.Length)
        {
            return;
        }

        if (!levels[index].unlocked)
        {
            StartCoroutine(Wiggle(cards[index]));
            return;
        }

        if (snap.Page != index)
        {
            snap.GoTo(index);
        }

        confirmed = true;
        onConfirm?.Invoke(levels[index], selectedMode);
    }

    int FirstUnlockedIndex()
    {
        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i].unlocked)
            {
                return i;
            }
        }

        return 0;
    }

    IEnumerator Wiggle(RectTransform card)
    {
        const float duration = 0.35f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float angle = Mathf.Sin(t * Mathf.PI * 6f) * 3f * (1f - t);
            card.localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        card.localRotation = Quaternion.identity;
    }
}
