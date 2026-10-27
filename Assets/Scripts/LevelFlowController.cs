using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Spec section 1 core loop: level-select carousel, level intro cinematic, gameplay mode
/// gate, the run (Trial: stimulus then quiz; Real Level: quiz waves only), corridor finish
/// into the hangar, boarding and takeoff, level clear, back to the carousel.
/// Time.timeScale stays at 0 while the menu and the intro are up, so the track, debris and
/// spawner timers wait. RunStarted and InputEnabled keep BoxSpawner and player input off
/// until the gate opens.
/// </summary>
public class LevelFlowController : MonoBehaviour
{
    public static LevelFlowController Instance { get; private set; }
    public static bool RunStarted { get; private set; }
    public static bool InputEnabled { get; private set; }

    [Header("Flow")]
    public bool showLevelSelect = true;
    public bool playIntroVideo = true;
    public bool playHangarEnding = true;
    public bool playTakeoffVideo = true;
    [Tooltip("Practice / Assessment toggle on the level card. Off: the mode comes from BoxSpawner.gameMode.")]
    public bool allowModeChoiceOnCard = true;
    [Tooltip("WebGL: ?mode=trial or ?mode=assessment in the page URL sets the mode and hides the card toggle.")]
    public bool readModeFromUrl = true;
    public float fadeSeconds = 0.35f;
    [Tooltip("Trial Mode only: how long the start-of-run instruction stays up.")]
    public float guidanceSeconds = 4f;

    [Header("Hangar Approach")]
    [Tooltip("Steering and shooting lock when the hangar entrance is this far ahead of the bunny (spec: Z about 20).")]
    public float lockControlZ = 20f;
    [Tooltip("The track starts braking when the ramp foot is this far past its stop point.")]
    public float brakeStartZ = 18f;
    [Tooltip("The ramp foot comes to rest this far ahead of the bunny.")]
    public float rampStopZ = 6f;
    public float boardRunSpeed = 4.5f;
    [Tooltip("The hangar is placed after this tile, counting from the back of the strip.")]
    public int hangarAfterTile = 5;
    [Tooltip("Track and boarding speed during the hangar approach, as a fraction of the run speed.")]
    public float finishSpeedScale = 1.25f;

    // Static so the card keeps the last chosen mode across the ReturnToLevelMap reload.
    static bool hasPreferredMode;
    static GameMode preferredMode;

    BoxSpawner spawner;
    PlayerControllerX player;
    TrackManager track;
    LevelInfo level;
    GameMode runMode;
    bool finishing;

    CanvasScaler scaler;
    RectTransform screens;
    CanvasGroup fade;
    CutscenePlayer cutscene;
    LevelSelectCarousel carousel;
    Vector2 lastScreenSize;
    float levelClearShownAt = -1f;

    public bool HandlesCorridorFinish => playHangarEnding && player != null && track != null;

    public static LevelFlowController Ensure()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject flowObject = new GameObject("LevelFlowController");
        return flowObject.AddComponent<LevelFlowController>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RunStarted = false;
        InputEnabled = false;
        AudioManager.SetFlowMusicScale(1f);
        Time.timeScale = showLevelSelect || playIntroVideo ? 0f : 1f;
    }

    IEnumerator Start()
    {
        spawner = FindAnyObjectByType<BoxSpawner>();
        player = FindAnyObjectByType<PlayerControllerX>();
        track = FindAnyObjectByType<TrackManager>();
        level = FirstUnlockedLevel();
        BuildCanvas();

        GameMode fromUrl = GameMode.Trial;
        bool urlMode = readModeFromUrl && TryReadUrlMode(out fromUrl);
        GameMode mode = urlMode ? fromUrl
            : hasPreferredMode ? preferredMode
            : spawner != null ? spawner.gameMode
            : GameModeSettings.CurrentMode;

        if (!showLevelSelect)
        {
            if (playIntroVideo)
            {
                SetFade(1f);
            }

            yield return StartLevel(level, mode, false);
            yield break;
        }

        AudioManager.SetMenuMode(true);
        bool modeChoice = allowModeChoiceOnCard && !urlMode;
        string modeNote = "MODE: " + GameModeSettings.Label(mode).ToUpperInvariant();
        carousel = LevelSelectCarousel.Create(screens, LevelCatalog.Levels, mode, modeChoice, modeNote, OnLevelConfirmed);
    }

    void Update()
    {
        Vector2 screenSize = new Vector2(Screen.width, Screen.height);
        if (screenSize != lastScreenSize)
        {
            ApplyOrientation();
        }

        Keyboard keyboard = Keyboard.current;
        if (levelClearShownAt >= 0f && keyboard != null && Time.unscaledTime - levelClearShownAt > 0.6f &&
            (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
        {
            ReturnToLevelMap();
        }
    }

    void OnLevelConfirmed(LevelInfo chosen, GameMode mode)
    {
        StartCoroutine(StartLevel(chosen, mode, true));
    }

    IEnumerator StartLevel(LevelInfo chosen, GameMode mode, bool fromMenu)
    {
        level = chosen;
        runMode = mode;
        hasPreferredMode = true;
        preferredMode = mode;

        if (fromMenu)
        {
            yield return Fade(1f);
            if (carousel != null)
            {
                Destroy(carousel.gameObject);
                carousel = null;
            }
        }

        AudioManager.SetMenuMode(false);

        if (playIntroVideo)
        {
            AudioManager.SetFlowMusicScale(0f);
            yield return cutscene.Play(level.introClipResource, level.introVideoFile);
            AudioManager.SetFlowMusicScale(1f);
        }

        BeginRun(mode);

        // Lets the first running frames render behind the fade instead of hitching on screen.
        yield return null;
        yield return null;
        yield return Fade(0f);

        if (GameModeSettings.ShowGuidance)
        {
            StartCoroutine(ShowGuidancePrompt());
        }
    }

    /// <summary>Gameplay mode gate: everything the run reads about the mode is set here.</summary>
    void BeginRun(GameMode mode)
    {
        GameModeSettings.CurrentMode = mode;
        if (spawner != null)
        {
            spawner.gameMode = mode;
        }

        RunStats.Reset();
        PlaySessionLogger.Ensure().RestartClock();
        Time.timeScale = 1f;
        RunStarted = true;
        InputEnabled = true;
    }

    IEnumerator ShowGuidancePrompt()
    {
        RectTransform prompt = FlowUI.Rect("Guidance", screens);
        FlowUI.Place(prompt, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(980f, 200f));
        FlowUI.Panel(prompt, 30, FlowUI.Border, FlowUI.PanelFill, 4f);
        CanvasGroup group = prompt.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0f;

        RectTransform title = FlowUI.Rect("Title", prompt);
        FlowUI.TopStrip(title, 22f, 50f, 30f);
        TextMeshProUGUI titleLabel = FlowUI.Label(title, "Text", "PRACTICE RUN", 38f, FlowUI.Caption, FontStyles.Bold);
        titleLabel.characterSpacing = 8f;

        RectTransform body = FlowUI.Rect("Body", prompt);
        FlowUI.TopStrip(body, 80f, 96f, 40f);
        TextMeshProUGUI bodyLabel = FlowUI.Label(body, "Text", "Run through the shape that comes toward you,\nthen shoot the box with the right answer.", 30f, Color.white, FontStyles.Normal);
        bodyLabel.textWrappingMode = TextWrappingModes.Normal;
        bodyLabel.enableAutoSizing = true;
        bodyLabel.fontSizeMin = 20f;
        bodyLabel.fontSizeMax = 30f;

        const float fadeIn = 0.3f;
        const float fadeOut = 0.5f;
        float total = fadeIn + guidanceSeconds + fadeOut;
        float elapsed = 0f;
        while (elapsed < total && prompt != null)
        {
            bool paused = GameHUD.Instance != null && GameHUD.Instance.IsPaused;
            if (!paused)
            {
                elapsed += Time.unscaledDeltaTime;
            }

            float alpha = elapsed < fadeIn ? elapsed / fadeIn
                : elapsed > total - fadeOut ? (total - elapsed) / fadeOut
                : 1f;
            group.alpha = paused ? 0f : Mathf.Clamp01(alpha);
            yield return null;
        }

        if (prompt != null)
        {
            Destroy(prompt.gameObject);
        }
    }

    public void BeginCorridorFinish()
    {
        if (finishing || !HandlesCorridorFinish)
        {
            return;
        }

        finishing = true;
        StartCoroutine(CorridorFinishRoutine());
    }

    IEnumerator CorridorFinishRoutine()
    {
        track.recycleTiles = false;
        track.trackSpeed *= Mathf.Max(1f, finishSpeedScale);
        if (AmbientDebrisManager.Instance != null)
        {
            AmbientDebrisManager.Instance.Retire(1.5f);
        }

        if (CurvedWorldController.Instance != null)
        {
            CurvedWorldController.Instance.FadeBendTo(0f, 4f);
        }

        Transform body = player.transform;
        float playerZ = body.position.z;
        bool hasEnd = track.TrimToTileCount(hangarAfterTile, out float endZ, out float surfaceY);
        Vector3 entrance = hasEnd
            ? new Vector3(player.centerRailX, surfaceY, endZ)
            : new Vector3(player.centerRailX, body.position.y, playerZ + 120f);
        float footOffset = body.position.y - entrance.y;
        if (Mathf.Abs(footOffset) > 1.5f)
        {
            footOffset = 0f;
        }

        HangarBay hangar = HangarBay.Create(entrance, track, level);

        while (hangar.EntranceZ - playerZ > lockControlZ)
        {
            yield return null;
        }

        player.LockControl();
        InputEnabled = false;

        float cruise = Mathf.Max(0.5f, track.trackSpeed);
        float stopZ = playerZ + rampStopZ;
        while (hangar.RampFoot.z - stopZ > brakeStartZ)
        {
            yield return null;
        }

        // Speed from v^2 = 2 a d, so the ramp foot arrives at stopZ as the track reaches zero.
        float deceleration = cruise * cruise / (2f * Mathf.Max(1f, brakeStartZ));
        while (hangar.RampFoot.z - stopZ > 0.02f)
        {
            float remaining = hangar.RampFoot.z - stopZ;
            track.trackSpeed = Mathf.Clamp(Mathf.Sqrt(2f * deceleration * remaining), 0.4f, cruise);
            yield return null;
        }

        track.trackSpeed = 0f;
        hangar.SnapRampFootTo(stopZ);

        player.BeginScriptedMotion();
        float runSpeed = boardRunSpeed * Mathf.Max(1f, finishSpeedScale);
        Vector3 lift = Vector3.up * footOffset;
        yield return MoveBunny(body, hangar.RampFoot + lift, runSpeed);
        yield return MoveBunny(body, hangar.Hatch + lift, runSpeed * 0.75f);
        yield return MoveBunny(body, hangar.Cabin + lift, runSpeed * 0.75f);
        SetRenderersVisible(body, false);

        yield return new WaitForSeconds(0.3f);
        yield return hangar.PlayTakeoff();
        yield return new WaitForSeconds(0.6f);

        int score = GameHUD.Instance != null ? GameHUD.Instance.score : 0;
        bool newBest = RecordHighScore(score);
        player.EndRun(false);

        if (playTakeoffVideo)
        {
            AudioManager.SetFlowMusicScale(0f);
            yield return cutscene.Play(level.takeoffClipResource, level.takeoffVideoFile);
            AudioManager.SetFlowMusicScale(1f);
        }

        AudioManager.SetMenuMode(true);
        ShowLevelClear(score, newBest);
    }

    static IEnumerator MoveBunny(Transform body, Vector3 target, float speed)
    {
        while ((body.position - target).sqrMagnitude > 0.0004f)
        {
            body.position = Vector3.MoveTowards(body.position, target, speed * Time.deltaTime);
            yield return null;
        }

        body.position = target;
    }

    static void SetRenderersVisible(Transform root, bool visible)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = visible;
        }
    }

    bool RecordHighScore(int score)
    {
        if (level == null || score <= level.HighScore)
        {
            return false;
        }

        PlayerPrefs.SetInt(level.HighScoreKey, score);
        PlayerPrefs.Save();
        return true;
    }

    void ShowLevelClear(int score, bool newBest)
    {
        RectTransform root = FlowUI.Rect("LevelClear", screens);
        FlowUI.Stretch(root);
        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        FlowUI.Solid(root, new Color(0.01f, 0.02f, 0.05f, 0.72f));

        RectTransform panel = FlowUI.Rect("Panel", root);
        FlowUI.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 860f));
        FlowUI.Panel(panel, 40, FlowUI.Border, FlowUI.PanelFill, 6f);

        RectTransform caption = FlowUI.Rect("Caption", panel);
        FlowUI.TopStrip(caption, 40f, 40f, 40f);
        TextMeshProUGUI captionLabel = FlowUI.Label(caption, "Text", "LEVEL " + level.number + " CLEAR", 34f, FlowUI.Accent, FontStyles.Bold);
        captionLabel.characterSpacing = 10f;

        RectTransform title = FlowUI.Rect("Title", panel);
        FlowUI.TopStrip(title, 88f, 64f, 40f);
        TextMeshProUGUI titleLabel = FlowUI.Label(title, "Text", level.title, 52f, Color.white, FontStyles.Bold);
        titleLabel.enableAutoSizing = true;
        titleLabel.fontSizeMin = 34f;
        titleLabel.fontSizeMax = 52f;

        RectTransform modeLine = FlowUI.Rect("Mode", panel);
        FlowUI.TopStrip(modeLine, 156f, 36f, 40f);
        FlowUI.Label(modeLine, "Text", GameModeSettings.Label(runMode) + " run", 28f, FlowUI.Muted, FontStyles.Normal);

        float top = 226f;
        string best = newBest ? "   <color=#27B449>NEW BEST</color>" : string.Empty;
        AddStatRow(panel, ref top, "Score", score.ToString("000") + best);
        AddStatRow(panel, ref top, "Answer accuracy", Ratio(RunStats.CorrectAnswers, RunStats.QuestionsPresented, RunStats.AnswerAccuracy));
        AddStatRow(panel, ref top, "Shot accuracy", Ratio(RunStats.CorrectAnswers, RunStats.ShotsFired, RunStats.ShotAccuracy));
        AddStatRow(panel, ref top, "Mean answer time",
            RunStats.TryMeanAnswerSeconds(out float answerSeconds)
                ? answerSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s"
                : "-");
        AddStatRow(panel, ref top, "Stimulus reaction",
            RunStats.TryMeanStimulusReactionMs(out float reactionMs)
                ? Mathf.RoundToInt(reactionMs).ToString(CultureInfo.InvariantCulture) + " ms"
                : "-");

        AddStatRow(panel, ref top, "Crashes", RunStats.Crashes.ToString(CultureInfo.InvariantCulture));

        RectTransform button = FlowUI.Rect("LevelMap", panel);
        FlowUI.BottomStrip(button, 48f, 100f, 120f);
        FlowUI.Button(button, "LEVEL MAP", 40f, FlowUI.Accent, ReturnToLevelMap, out _);

        levelClearShownAt = Time.unscaledTime;
        StartCoroutine(FadeGroup(group, 1f, 0.45f));
    }

    static void AddStatRow(RectTransform panel, ref float top, string name, string value)
    {
        const float rowHeight = 64f;
        RectTransform row = FlowUI.Rect(name, panel);
        FlowUI.TopStrip(row, top, rowHeight, 70f);

        TextMeshProUGUI nameLabel = FlowUI.Label(row, "Name", name, 32f, FlowUI.Muted, FontStyles.Normal);
        nameLabel.alignment = TextAlignmentOptions.Left;
        TextMeshProUGUI valueLabel = FlowUI.Label(row, "Value", value, 34f, Color.white, FontStyles.Bold);
        valueLabel.alignment = TextAlignmentOptions.Right;

        RectTransform divider = FlowUI.Rect("Divider", row);
        FlowUI.BottomStrip(divider, 0f, 2f, 0f);
        FlowUI.Solid(divider, new Color(1f, 1f, 1f, 0.08f), false);

        top += rowHeight;
    }

    static string Ratio(int part, int whole, float fraction)
    {
        if (whole <= 0)
        {
            return "-";
        }

        int percent = Mathf.RoundToInt(fraction * 100f);
        return part + " / " + whole + "   (" + percent + "%)";
    }

    /// <summary>Saves the partial session, then reloads the scene, which opens on the carousel.</summary>
    public static void ReturnToLevelMap()
    {
        RunStarted = false;
        InputEnabled = false;

        if (PlaySessionLogger.Instance != null)
        {
            Destroy(PlaySessionLogger.Instance.gameObject);
        }

        if (GameHUD.Instance != null)
        {
            Destroy(GameHUD.Instance.gameObject);
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("FlowCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;

        scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        canvasObject.AddComponent<GraphicRaycaster>();
        FlowUI.EnsureEventSystem();
        ApplyOrientation();

        RectTransform root = (RectTransform)canvasObject.transform;
        screens = FlowUI.Rect("Screens", root);
        FlowUI.Stretch(screens);

        // Above the screens; the cutscene sits above this so a video can cut in from black.
        RectTransform fadeRect = FlowUI.Rect("Fade", root);
        FlowUI.Stretch(fadeRect);
        FlowUI.Solid(fadeRect, Color.black);
        fade = fadeRect.gameObject.AddComponent<CanvasGroup>();
        SetFade(0f);

        cutscene = CutscenePlayer.Create(root);
    }

    void ApplyOrientation()
    {
        lastScreenSize = new Vector2(Screen.width, Screen.height);
        if (scaler == null)
        {
            return;
        }

        bool portrait = Screen.height >= Screen.width;
        scaler.referenceResolution = portrait ? new Vector2(1080f, 1920f) : new Vector2(1920f, 1080f);
    }

    IEnumerator Fade(float target)
    {
        float start = fade.alpha;
        float elapsed = 0f;
        fade.blocksRaycasts = true;
        while (elapsed < fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            fade.alpha = Mathf.Lerp(start, target, elapsed / fadeSeconds);
            yield return null;
        }

        SetFade(target);
    }

    void SetFade(float alpha)
    {
        fade.alpha = alpha;
        fade.blocksRaycasts = alpha > 0.01f;
    }

    static IEnumerator FadeGroup(CanvasGroup group, float target, float seconds)
    {
        float start = group.alpha;
        float elapsed = 0f;
        while (elapsed < seconds && group != null)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, elapsed / seconds);
            yield return null;
        }

        if (group != null)
        {
            group.alpha = target;
        }
    }

    static LevelInfo FirstUnlockedLevel()
    {
        LevelInfo[] levels = LevelCatalog.Levels;
        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i].unlocked)
            {
                return levels[i];
            }
        }

        return levels[0];
    }

    /// <summary>Reads ?mode= from the WebGL page URL so the Next.js shell can pick the mode.</summary>
    static bool TryReadUrlMode(out GameMode mode)
    {
        mode = GameMode.Trial;
        string url = Application.absoluteURL;
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        int queryStart = url.IndexOf('?');
        if (queryStart < 0)
        {
            return false;
        }

        string query = url.Substring(queryStart + 1);
        int fragment = query.IndexOf('#');
        if (fragment >= 0)
        {
            query = query.Substring(0, fragment);
        }

        string[] pairs = query.Split('&');
        for (int i = 0; i < pairs.Length; i++)
        {
            string[] parts = pairs[i].Split('=');
            if (parts.Length != 2 || !parts[0].Equals("mode", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            switch (UnityEngine.Networking.UnityWebRequest.UnEscapeURL(parts[1]).ToLowerInvariant())
            {
                case "trial":
                case "practice":
                case "tutorial":
                    mode = GameMode.Trial;
                    return true;
                case "assessment":
                case "standard":
                case "real":
                case "level":
                    mode = GameMode.StandardLevel;
                    return true;
            }
        }

        return false;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
