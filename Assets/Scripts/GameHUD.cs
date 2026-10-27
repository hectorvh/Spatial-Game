using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    [Header("Scoring")]
    public int pointsPerCorrectAnswer = 100;
    public int pointsPerCollision = 50;

    public int score { get; private set; }
    public bool IsPaused { get; private set; }

    static readonly Color PanelColor = new Color(0.149f, 0.169f, 0.220f, 0.94f);
    static readonly Color BorderColor = new Color(0.310f, 0.659f, 0.776f, 1f);
    static readonly Color ScoreCaption = new Color(0.373f, 0.765f, 0.871f, 1f);
    static readonly Color BoxViewBorder = new Color(0.271f, 0.722f, 0.871f, 1f);
    static readonly Color BoxViewFill = new Color(0.851f, 0.933f, 0.969f, 1f);

    const float BorderThickness = 6f;
    const float BoxViewBorderThickness = 4f;
    const float BoxViewSize = 320f;
    const float BoxViewGap = 16f;
    const float ScreenMargin = 40f;

    static readonly Dictionary<int, Sprite> roundedSprites = new Dictionary<int, Sprite>();

    CanvasScaler scaler;
    RectTransform scorePanel;
    TextMeshProUGUI scoreValueLabel;
    TextMeshProUGUI questionLabel;
    RectTransform questionPanel;
    RectTransform stimulusPanel;
    UIStimulusView stimulusView;
    Question pendingStimulusViewQuestion;
    bool stimulusViewShowQueued;
    RectTransform finishPanel;
    TextMeshProUGUI finishScoreLabel;
    RectTransform pauseButton;
    Image pauseIcon;
    RectTransform pauseMenu;
    static Sprite pauseSprite;
    static Sprite playSprite;
    Vector2 lastScreenSize;
    readonly List<QuestionWave> activeWaves = new List<QuestionWave>();
    PlayerControllerX player;
    string acceptedQuestionText;

    public static GameHUD Ensure()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject hudObject = new GameObject("GameHUD");
        return hudObject.AddComponent<GameHUD>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        score = 0;

        BuildUI();
        ApplyOrientation();
        Refresh();
    }

    void Update()
    {
        Vector2 screenSize = new Vector2(Screen.width, Screen.height);
        if (screenSize != lastScreenSize)
        {
            ApplyOrientation();
        }

        RefreshQuestionBanner();
        FlushStimulusBoxView();
    }

    public void OnQuestionAccepted(Question question)
    {
        bool guided = GameModeSettings.ShowGuidance;
        acceptedQuestionText = guided && question != null ? question.question : null;
        if (questionLabel != null)
        {
            questionLabel.text = acceptedQuestionText ?? string.Empty;
        }

        SetQuestionVisible(!string.IsNullOrEmpty(acceptedQuestionText));
        QueueStimulusBoxView(guided ? question : null);
    }

    public void ClearTrialBanner()
    {
        acceptedQuestionText = null;
        SetQuestionVisible(false);
        HideStimulusBoxView();
    }

    public void RegisterQuestionWave(QuestionWave wave)
    {
        if (wave == null || activeWaves.Contains(wave))
        {
            return;
        }

        activeWaves.Add(wave);
        RefreshQuestionBanner();
    }

    public void UnregisterQuestionWave(QuestionWave wave)
    {
        if (wave == null)
        {
            return;
        }

        activeWaves.Remove(wave);
        RefreshQuestionBanner();
    }

    public void AddScore(int amount)
    {
        score += amount;
        Refresh();
    }

    public void AddCorrectAnswerScore()
    {
        AddScore(pointsPerCorrectAnswer);
        HideStimulusBoxView();
    }

    public void PlayCorrectScoreCollect(Vector3 worldPosition)
    {
        HideStimulusBoxView();
        StartCoroutine(ScoreCollectRoutine(worldPosition, pointsPerCorrectAnswer));
    }

    IEnumerator ScoreCollectRoutine(Vector3 worldPosition, int points)
    {
        if (scorePanel == null)
        {
            AddScore(points);
            yield break;
        }

        RectTransform canvasRect = transform as RectTransform;
        Camera worldCamera = FindGameplayCamera();
        Vector2 startLocal = WorldToCanvasLocal(worldPosition, canvasRect, worldCamera);
        Vector2 endLocal = RectToCanvasLocal(scorePanel, canvasRect);

        const int sparkCount = 16;
        int remaining = sparkCount;
        bool credited = false;

        for (int i = 0; i < sparkCount; i++)
        {
            float delay = Random.Range(0f, 0.28f);
            float duration = Random.Range(0.95f, 1.35f);
            Vector2 scatter = Random.insideUnitCircle * 48f;
            Vector2 control = Vector2.Lerp(startLocal + scatter, endLocal, 0.45f);
            control += Vector2.Perpendicular((endLocal - startLocal).normalized) * Random.Range(-90f, 90f);

            StartCoroutine(FlySpark(
                canvasRect,
                startLocal + scatter,
                control,
                endLocal,
                delay,
                duration,
                () =>
                {
                    remaining--;
                    if (!credited)
                    {
                        credited = true;
                        AddScore(points);
                        StartCoroutine(PulseScorePanel());
                    }
                }
            ));
        }

        float timeout = 2.4f;
        float elapsed = 0f;
        while (!credited && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!credited)
        {
            AddScore(points);
        }
    }

    IEnumerator FlySpark(RectTransform canvasRect, Vector2 start, Vector2 control, Vector2 end, float delay, float duration, System.Action onArrived)
    {
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        Image spark = CreateScoreSpark(canvasRect);
        RectTransform sparkRect = spark.rectTransform;
        sparkRect.anchoredPosition = start;

        float elapsed = 0f;
        while (elapsed < duration && spark != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t;
            sparkRect.anchoredPosition = QuadBezier(start, control, end, eased);
            float size = Mathf.Lerp(28f, 10f, eased);
            sparkRect.sizeDelta = new Vector2(size, size);
            Color color = spark.color;
            color.a = Mathf.Lerp(1f, 0.85f, eased);
            spark.color = color;
            yield return null;
        }

        if (spark != null)
        {
            Destroy(spark.gameObject);
        }

        onArrived?.Invoke();
    }

    IEnumerator PulseScorePanel()
    {
        if (scorePanel == null)
        {
            yield break;
        }

        Vector3 baseScale = Vector3.one;
        const float peak = 1.12f;
        const float up = 0.08f;
        const float down = 0.14f;
        float elapsed = 0f;
        while (elapsed < up && scorePanel != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / up);
            scorePanel.localScale = Vector3.Lerp(baseScale, Vector3.one * peak, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < down && scorePanel != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / down);
            scorePanel.localScale = Vector3.Lerp(Vector3.one * peak, baseScale, t);
            yield return null;
        }

        if (scorePanel != null)
        {
            scorePanel.localScale = baseScale;
        }
    }

    Image CreateScoreSpark(RectTransform canvasRect)
    {
        GameObject sparkObject = new GameObject("ScoreSpark", typeof(RectTransform));
        sparkObject.transform.SetParent(canvasRect, false);
        sparkObject.transform.SetAsLastSibling();

        RectTransform rect = sparkObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(24f, 24f);

        Image image = sparkObject.AddComponent<Image>();
        image.sprite = GetRoundedSprite(16);
        image.color = new Color(1f, 0.42f, 0.28f, 1f);
        image.raycastTarget = false;
        return image;
    }

    static Vector2 WorldToCanvasLocal(Vector3 worldPosition, RectTransform canvasRect, Camera worldCamera)
    {
        Vector2 screen = worldCamera != null
            ? (Vector2)worldCamera.WorldToScreenPoint(worldPosition)
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
        return local;
    }

    static Vector2 RectToCanvasLocal(RectTransform target, RectTransform canvasRect)
    {
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
        return local;
    }

    static Vector2 QuadBezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return (u * u * a) + (2f * u * t * b) + (t * t * c);
    }

    static Camera FindGameplayCamera()
    {
        if (CameraLaneFollow.Instance != null)
        {
            Camera followCamera = CameraLaneFollow.Instance.GetComponent<Camera>();
            if (followCamera != null && followCamera.targetTexture == null)
            {
                return followCamera;
            }
        }

        return Camera.main;
    }

    public void ApplyCollisionPenalty()
    {
        AddScore(-pointsPerCollision);
        HideStimulusBoxView();
    }

    public void ShowFinish()
    {
        ShowFinish(true);
    }

    /// <param name="showPanel">False when LevelFlowController shows its level-clear screen instead.</param>
    public void ShowFinish(bool showPanel)
    {
        if (showPanel)
        {
            if (finishPanel == null)
            {
                BuildFinishPanel();
            }

            if (finishScoreLabel != null)
            {
                finishScoreLabel.text = score.ToString("000");
            }

            finishPanel.gameObject.SetActive(true);
        }

        ClearTrialBanner();
        SetPaused(false);
        if (pauseButton != null)
        {
            pauseButton.gameObject.SetActive(false);
        }
    }

    void BuildUI()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        gameObject.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // Expand keeps the whole reference layout visible in portrait and landscape.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        BuildScorePanel();
        BuildPauseMenu();
        BuildPauseButton();
        BuildQuestionPanel();
        BuildStimulusBoxView();
        BuildFinishPanel();
    }

    void ApplyOrientation()
    {
        lastScreenSize = new Vector2(Screen.width, Screen.height);

        bool portrait = Screen.height >= Screen.width;
        scaler.referenceResolution = portrait
            ? new Vector2(1080f, 1920f)
            : new Vector2(1920f, 1080f);

        LayoutQuestionPanel(portrait);
        LayoutStimulusBoxView(portrait);
        LayoutFinishPanel(portrait);
        LayoutPauseMenu(portrait);
    }

    void BuildScorePanel()
    {
        RectTransform panel = CreatePanel(
            "ScorePanel",
            new Vector2(0.5f, 0f),
            new Vector2(0f, ScreenMargin),
            new Vector2(240f, 150f),
            30
        );
        scorePanel = panel;

        TextMeshProUGUI caption = CreateLabel("Caption", panel, 30f, ScoreCaption, FontStyles.Bold);
        RectTransform captionRect = caption.rectTransform;
        captionRect.anchorMin = new Vector2(0f, 1f);
        captionRect.anchorMax = new Vector2(1f, 1f);
        captionRect.pivot = new Vector2(0.5f, 1f);
        captionRect.offsetMin = new Vector2(12f, -58f);
        captionRect.offsetMax = new Vector2(-12f, -16f);
        caption.text = "SCORE";
        caption.characterSpacing = 8f;

        scoreValueLabel = CreateLabel("Value", panel, 62f, Color.white, FontStyles.Bold);
        RectTransform valueRect = scoreValueLabel.rectTransform;
        valueRect.anchorMin = new Vector2(0f, 0f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.offsetMin = new Vector2(12f, 14f);
        valueRect.offsetMax = new Vector2(-12f, -56f);
    }

    void BuildPauseButton()
    {
        pauseButton = CreatePanel(
            "PauseButton",
            new Vector2(1f, 0f),
            new Vector2(-ScreenMargin, ScreenMargin),
            new Vector2(150f, 150f),
            30
        );

        Button button = pauseButton.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.85f, 0.95f, 1f, 1f);
        colors.pressedColor = new Color(0.7f, 0.85f, 0.95f, 1f);
        button.colors = colors;
        button.onClick.AddListener(TogglePause);

        if (pauseSprite == null)
        {
            pauseSprite = CreatePauseSprite(128);
        }

        if (playSprite == null)
        {
            playSprite = CreatePlaySprite(128);
        }

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform));
        iconObject.transform.SetParent(pauseButton, false);

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(72f, 72f);

        pauseIcon = iconObject.AddComponent<Image>();
        pauseIcon.sprite = pauseSprite;
        pauseIcon.preserveAspect = true;
        pauseIcon.color = Color.white;
        pauseIcon.raycastTarget = false;
    }

    void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventObject = new GameObject("EventSystem");
        eventObject.AddComponent<EventSystem>();
        eventObject.AddComponent<InputSystemUIInputModule>();
    }

    public void TogglePause()
    {
        if (IsGameOver())
        {
            return;
        }

        SetPaused(!IsPaused);
    }

    void SetPaused(bool paused)
    {
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;

        if (pauseIcon != null)
        {
            pauseIcon.sprite = paused ? playSprite : pauseSprite;
        }

        if (pauseMenu != null)
        {
            pauseMenu.gameObject.SetActive(paused);
        }

        if (paused)
        {
            SetQuestionVisible(false);
            SetStimulusPanelActive(false);
        }
        else
        {
            RefreshQuestionBanner();
            RestoreStimulusPanelIfNeeded();
        }
    }

    void BuildPauseMenu()
    {
        GameObject menuObject = new GameObject("PauseMenu", typeof(RectTransform));
        menuObject.transform.SetParent(transform, false);

        pauseMenu = menuObject.GetComponent<RectTransform>();
        pauseMenu.anchorMin = new Vector2(0.1f, 0.1f);
        pauseMenu.anchorMax = new Vector2(0.9f, 0.9f);
        pauseMenu.offsetMin = Vector2.zero;
        pauseMenu.offsetMax = Vector2.zero;
        DecoratePanel(pauseMenu, 36);
        pauseMenu.gameObject.SetActive(false);

        GameObject columnObject = new GameObject("Buttons", typeof(RectTransform));
        columnObject.transform.SetParent(pauseMenu, false);

        RectTransform column = columnObject.GetComponent<RectTransform>();
        column.anchorMin = Vector2.zero;
        column.anchorMax = Vector2.one;
        column.offsetMin = new Vector2(48f, 48f);
        column.offsetMax = new Vector2(-48f, -48f);

        VerticalLayoutGroup layout = columnObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 28f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateMenuButton(column, "Continue", () => SetPaused(false));
        CreateMenuButton(column, "Settings", OnSettingsClicked);
        CreateMenuButton(column, "Exit", OnExitClicked);
    }

    void LayoutPauseMenu(bool portrait)
    {
        if (pauseMenu == null)
        {
            return;
        }

        pauseMenu.anchorMin = new Vector2(0.1f, 0.1f);
        pauseMenu.anchorMax = new Vector2(0.9f, 0.9f);
        pauseMenu.offsetMin = Vector2.zero;
        pauseMenu.offsetMax = Vector2.zero;
    }

    void CreateMenuButton(RectTransform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(label + "Button", typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        DecoratePanel(rect, 24);

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.minHeight = 120f;
        layout.preferredHeight = 120f;
        layout.flexibleWidth = 1f;

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.85f, 0.95f, 1f, 1f);
        colors.pressedColor = new Color(0.7f, 0.85f, 0.95f, 1f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        TextMeshProUGUI text = CreateLabel("Label", rect, 44f, Color.white, FontStyles.Bold);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.text = label;
        text.enableAutoSizing = true;
        text.fontSizeMin = 28f;
        text.fontSizeMax = 48f;
    }

    void OnSettingsClicked()
    {
        // Placeholder: settings panel will be wired later.
    }

    void OnExitClicked()
    {
        if (LevelFlowController.Instance != null && LevelFlowController.Instance.showLevelSelect)
        {
            LevelFlowController.ReturnToLevelMap();
        }
    }

    void BuildQuestionPanel()
    {
        questionPanel = CreateStretchPanel("QuestionPanel", 30);
        questionPanel.gameObject.SetActive(false);

        questionLabel = CreateLabel("Question", questionPanel, 42f, Color.white, FontStyles.Bold);
        RectTransform labelRect = questionLabel.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(28f, 16f);
        labelRect.offsetMax = new Vector2(-28f, -16f);
        questionLabel.enableWordWrapping = true;
        questionLabel.enableAutoSizing = true;
        questionLabel.overflowMode = TextOverflowModes.Ellipsis;
        questionLabel.alignment = TextAlignmentOptions.Center;
    }

    void BuildStimulusBoxView()
    {
        stimulusPanel = CreatePanel(
            "HUD_StimulusBoxView",
            new Vector2(0f, 1f),
            Vector2.zero,
            new Vector2(BoxViewSize, BoxViewSize),
            24,
            BoxViewBorder,
            BoxViewFill,
            BoxViewBorderThickness
        );
        stimulusPanel.gameObject.SetActive(false);

        Transform fill = stimulusPanel.Find("Fill");
        GameObject previewObject = new GameObject("Preview", typeof(RectTransform));
        previewObject.transform.SetParent(fill != null ? fill : stimulusPanel, false);

        RectTransform previewRect = previewObject.GetComponent<RectTransform>();
        previewRect.anchorMin = Vector2.zero;
        previewRect.anchorMax = Vector2.one;
        previewRect.offsetMin = new Vector2(8f, 8f);
        previewRect.offsetMax = new Vector2(-8f, -8f);

        RawImage previewImage = previewObject.AddComponent<RawImage>();
        previewImage.color = Color.white;
        previewImage.raycastTarget = false;

        stimulusView = UIStimulusView.Attach(stimulusPanel, previewImage);
    }

    void LayoutStimulusBoxView(bool portrait)
    {
        if (stimulusPanel == null)
        {
            return;
        }

        float sideMargin = portrait ? 36f : 120f;
        float topMargin = portrait ? 36f : 28f;
        float bannerHeight = portrait ? 280f : 150f;
        float size = BoxViewSize;

        stimulusPanel.anchorMin = new Vector2(0f, 1f);
        stimulusPanel.anchorMax = new Vector2(0f, 1f);
        stimulusPanel.pivot = new Vector2(0f, 1f);
        stimulusPanel.anchoredPosition = new Vector2(sideMargin, -(topMargin + bannerHeight + BoxViewGap));
        stimulusPanel.sizeDelta = new Vector2(size, size);
    }

    void ShowStimulusBoxView(Question question)
    {
        if (stimulusView == null)
        {
            return;
        }

        stimulusView.Show(question);
    }

    void QueueStimulusBoxView(Question question)
    {
        pendingStimulusViewQuestion = question;
        stimulusViewShowQueued = question != null;
        if (question == null)
        {
            HideStimulusBoxView();
        }
    }

    void FlushStimulusBoxView()
    {
        if (!stimulusViewShowQueued)
        {
            return;
        }

        stimulusViewShowQueued = false;
        Question question = pendingStimulusViewQuestion;
        pendingStimulusViewQuestion = null;

        if (question == null || IsPaused || IsGameOver())
        {
            return;
        }

        ShowStimulusBoxView(question);
    }

    void HideStimulusBoxView()
    {
        stimulusViewShowQueued = false;
        pendingStimulusViewQuestion = null;
        if (stimulusView != null)
        {
            stimulusView.Hide();
        }
    }

    void SetStimulusPanelActive(bool active)
    {
        if (stimulusPanel == null)
        {
            return;
        }

        if (stimulusPanel.gameObject.activeSelf != active)
        {
            stimulusPanel.gameObject.SetActive(active);
        }
    }

    void RestoreStimulusPanelIfNeeded()
    {
        if (stimulusView != null && stimulusView.IsVisible)
        {
            SetStimulusPanelActive(true);
        }
    }

    void LayoutQuestionPanel(bool portrait)
    {
        if (questionPanel == null)
        {
            return;
        }

        // Phone portrait needs a taller banner and more wrapping; landscape/PC stays compact.
        float sideMargin = portrait ? 36f : 120f;
        float topMargin = portrait ? 36f : 28f;
        float height = portrait ? 280f : 150f;

        questionPanel.anchorMin = new Vector2(0f, 1f);
        questionPanel.anchorMax = new Vector2(1f, 1f);
        questionPanel.pivot = new Vector2(0.5f, 1f);
        questionPanel.offsetMin = new Vector2(sideMargin, -topMargin - height);
        questionPanel.offsetMax = new Vector2(-sideMargin, -topMargin);

        if (questionLabel != null)
        {
            questionLabel.fontSizeMin = portrait ? 28f : 26f;
            questionLabel.fontSizeMax = portrait ? 54f : 44f;
        }
    }

    void BuildFinishPanel()
    {
        finishPanel = CreatePanel(
            "FinishPanel",
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(640f, 280f),
            30
        );
        finishPanel.gameObject.SetActive(false);

        TextMeshProUGUI title = CreateLabel("Title", finishPanel, 48f, ScoreCaption, FontStyles.Bold);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(24f, -110f);
        titleRect.offsetMax = new Vector2(-24f, -28f);
        title.text = "FINISHED";
        title.characterSpacing = 8f;

        finishScoreLabel = CreateLabel("FinalScore", finishPanel, 72f, Color.white, FontStyles.Bold);
        RectTransform scoreRect = finishScoreLabel.rectTransform;
        scoreRect.anchorMin = new Vector2(0f, 0f);
        scoreRect.anchorMax = new Vector2(1f, 1f);
        scoreRect.offsetMin = new Vector2(24f, 28f);
        scoreRect.offsetMax = new Vector2(-24f, -110f);
    }

    void LayoutFinishPanel(bool portrait)
    {
        if (finishPanel == null)
        {
            return;
        }

        finishPanel.sizeDelta = portrait ? new Vector2(720f, 320f) : new Vector2(640f, 280f);
    }

    void RefreshQuestionBanner()
    {
        if (questionPanel == null || questionLabel == null)
        {
            return;
        }

        if (IsGameOver())
        {
            SetQuestionVisible(false);
            return;
        }

        if (IsPaused)
        {
            SetQuestionVisible(false);
            SetStimulusPanelActive(false);
            return;
        }

        if (!GameModeSettings.ShowGuidance)
        {
            SetQuestionVisible(false);
            SetStimulusPanelActive(false);
            return;
        }

        if (!string.IsNullOrEmpty(acceptedQuestionText))
        {
            if (questionLabel.text != acceptedQuestionText)
            {
                questionLabel.text = acceptedQuestionText;
            }

            SetQuestionVisible(true);
            return;
        }

        QuestionWave nearest = null;
        float nearestZ = float.MaxValue;
        for (int i = activeWaves.Count - 1; i >= 0; i--)
        {
            QuestionWave wave = activeWaves[i];
            if (wave == null)
            {
                activeWaves.RemoveAt(i);
                continue;
            }

            float z = wave.transform.position.z;
            if (z < 0f)
            {
                continue;
            }

            if (z < nearestZ)
            {
                nearestZ = z;
                nearest = wave;
            }
        }

        if (nearest == null || string.IsNullOrEmpty(nearest.questionText))
        {
            SetQuestionVisible(false);
            return;
        }

        if (questionLabel.text != nearest.questionText)
        {
            questionLabel.text = nearest.questionText;
        }

        SetQuestionVisible(true);
    }

    void SetQuestionVisible(bool visible)
    {
        if (questionPanel == null)
        {
            return;
        }

        if (questionPanel.gameObject.activeSelf != visible)
        {
            questionPanel.gameObject.SetActive(visible);
        }
    }

    bool IsGameOver()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerControllerX>();
        }

        return player != null && player.isGameOver;
    }

    RectTransform CreateStretchPanel(string name, int cornerRadius)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform));
        panelObject.transform.SetParent(transform, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        DecoratePanel(rect, cornerRadius);
        return rect;
    }

    RectTransform CreatePanel(string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, int cornerRadius)
    {
        return CreatePanel(name, anchor, anchoredPosition, size, cornerRadius, BorderColor, PanelColor, BorderThickness);
    }

    RectTransform CreatePanel(string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, int cornerRadius, Color borderColor, Color fillColor, float borderThickness)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform));
        panelObject.transform.SetParent(transform, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        DecoratePanel(rect, cornerRadius, borderColor, fillColor, borderThickness);
        return rect;
    }

    void DecoratePanel(RectTransform rect, int cornerRadius)
    {
        DecoratePanel(rect, cornerRadius, BorderColor, PanelColor, BorderThickness);
    }

    void DecoratePanel(RectTransform rect, int cornerRadius, Color borderColor, Color fillColor, float borderThickness)
    {
        Image border = rect.gameObject.AddComponent<Image>();
        border.sprite = GetRoundedSprite(cornerRadius);
        border.type = Image.Type.Sliced;
        border.color = borderColor;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform));
        fillObject.transform.SetParent(rect, false);

        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(borderThickness, borderThickness);
        fillRect.offsetMax = new Vector2(-borderThickness, -borderThickness);

        Image fill = fillObject.AddComponent<Image>();
        fill.sprite = GetRoundedSprite(Mathf.Max(4, cornerRadius - (int)borderThickness));
        fill.type = Image.Type.Sliced;
        fill.color = fillColor;
    }

    TextMeshProUGUI CreateLabel(string name, RectTransform parent, float fontSize, Color color, FontStyles style)
    {
        GameObject labelObject = new GameObject(name, typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.color = color;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        return label;
    }

    void Refresh()
    {
        if (scoreValueLabel != null)
        {
            scoreValueLabel.text = score.ToString("000");
        }
    }

    static Sprite GetRoundedSprite(int radius)
    {
        if (roundedSprites.TryGetValue(radius, out Sprite cached) && cached != null)
        {
            return cached;
        }

        int size = (radius * 2) + 8;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

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
                float alpha = Mathf.Clamp01(0.5f - distance);
                pixels[(y * size) + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        Vector4 border = new Vector4(radius, radius, radius, radius);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        roundedSprites[radius] = sprite;
        return sprite;
    }

    static Sprite CreatePauseSprite(int size)
    {
        Texture2D texture = NewIconTexture(size);
        Color[] pixels = new Color[size * size];

        float barWidth = size * 0.16f;
        float barHeight = size * 0.56f;
        float gap = size * 0.14f;
        float center = size * 0.5f;
        float top = center + (barHeight * 0.5f);
        float bottom = center - (barHeight * 0.5f);
        float left1 = center - (gap * 0.5f) - barWidth;
        float right1 = left1 + barWidth;
        float left2 = center + (gap * 0.5f);
        float right2 = left2 + barWidth;
        float radius = barWidth * 0.45f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inLeft = IsInsideRoundedBar(x + 0.5f, y + 0.5f, left1, right1, bottom, top, radius);
                bool inRight = IsInsideRoundedBar(x + 0.5f, y + 0.5f, left2, right2, bottom, top, radius);
                pixels[(y * size) + x] = new Color(1f, 1f, 1f, inLeft || inRight ? 1f : 0f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite CreatePlaySprite(int size)
    {
        Texture2D texture = NewIconTexture(size);
        Color[] pixels = new Color[size * size];

        Vector2 a = new Vector2(size * 0.30f, size * 0.20f);
        Vector2 b = new Vector2(size * 0.30f, size * 0.80f);
        Vector2 c = new Vector2(size * 0.78f, size * 0.50f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inside = IsInsideTriangle(new Vector2(x + 0.5f, y + 0.5f), a, b, c);
                pixels[(y * size) + x] = new Color(1f, 1f, 1f, inside ? 1f : 0f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Texture2D NewIconTexture(int size)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
    }

    static bool IsInsideRoundedBar(float px, float py, float left, float right, float bottom, float top, float radius)
    {
        if (px < left || px > right || py < bottom || py > top)
        {
            return false;
        }

        float innerLeft = left + radius;
        float innerRight = right - radius;
        float innerBottom = bottom + radius;
        float innerTop = top - radius;

        if (px >= innerLeft && px <= innerRight)
        {
            return true;
        }

        if (py >= innerBottom && py <= innerTop)
        {
            return true;
        }

        float cx = px < innerLeft ? innerLeft : innerRight;
        float cy = py < innerBottom ? innerBottom : innerTop;
        float dx = px - cx;
        float dy = py - cy;
        return (dx * dx) + (dy * dy) <= radius * radius;
    }

    static bool IsInsideTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);
        bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNeg && hasPos);
    }

    static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return ((p1.x - p3.x) * (p2.y - p3.y)) - ((p2.x - p3.x) * (p1.y - p3.y));
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Time.timeScale = 1f;
            Instance = null;
        }
    }
}
