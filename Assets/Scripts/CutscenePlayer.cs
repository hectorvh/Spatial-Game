using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Full-screen skippable video for the level intro and the hangar takeoff.
/// Looks for a Resources VideoClip first (desktop / editor), then a StreamingAssets file
/// (required on WebGL). When neither exists, Play returns at once so the flow just
/// continues without the video.
/// </summary>
public class CutscenePlayer : MonoBehaviour
{
    public float prepareTimeout = 6f;
    public float skipArmDelay = 0.35f;
    [Tooltip("Seconds added to the clip length before playback is forced to end.")]
    public float overrunAllowance = 2f;

    CanvasGroup group;
    RawImage screen;
    AspectRatioFitter fitter;
    GameObject skipHint;
    VideoPlayer player;
    RenderTexture target;
    float shownAt;
    bool skipRequested;
    bool finished;
    bool failed;

    public bool IsPlaying { get; private set; }

    public static CutscenePlayer Create(RectTransform parent)
    {
        RectTransform root = FlowUI.Rect("Cutscene", parent);
        FlowUI.Stretch(root);
        CutscenePlayer cutscene = root.gameObject.AddComponent<CutscenePlayer>();
        cutscene.Build(root);
        return cutscene;
    }

    void Build(RectTransform root)
    {
        group = root.gameObject.AddComponent<CanvasGroup>();
        FlowUI.Solid(root, Color.black);

        Button tap = root.gameObject.AddComponent<Button>();
        tap.transition = Selectable.Transition.None;
        tap.navigation = new Navigation { mode = Navigation.Mode.None };
        tap.onClick.AddListener(RequestSkip);

        RectTransform screenRect = FlowUI.Rect("Screen", root);
        FlowUI.Stretch(screenRect);
        screen = screenRect.gameObject.AddComponent<RawImage>();
        screen.color = Color.white;
        screen.raycastTarget = false;
        fitter = screenRect.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 16f / 9f;

        RectTransform hint = FlowUI.Rect("SkipHint", root);
        FlowUI.Place(hint, new Vector2(1f, 0f), new Vector2(-40f, 32f), new Vector2(520f, 44f));
        TextMeshProUGUI hintLabel = FlowUI.Label(hint, "Text", "Tap or press Space to skip", 26f, new Color(1f, 1f, 1f, 0.7f), FontStyles.Normal);
        hintLabel.alignment = TextAlignmentOptions.Right;
        skipHint = hint.gameObject;

        SetVisible(false);
    }

    /// <summary>Plays one cutscene. Yields until it ends, is skipped, or cannot load.</summary>
    public IEnumerator Play(string resourcesClip, string streamingFile)
    {
        VideoClip clip = null;
#if !UNITY_WEBGL || UNITY_EDITOR
        if (!string.IsNullOrEmpty(resourcesClip))
        {
            clip = Resources.Load<VideoClip>(resourcesClip);
        }
#endif

        string url = null;
        if (clip == null && !string.IsNullOrEmpty(streamingFile))
        {
            string candidate = Application.streamingAssetsPath + "/" + streamingFile;
            if (candidate.Contains("://"))
            {
                using (UnityWebRequest probe = UnityWebRequest.Head(candidate))
                {
                    probe.timeout = 5;
                    yield return probe.SendWebRequest();
                    if (probe.result == UnityWebRequest.Result.Success)
                    {
                        url = candidate;
                    }
                }
            }
            else if (File.Exists(candidate))
            {
                url = candidate;
            }
        }

        if (clip == null && url == null)
        {
            yield break;
        }

        IsPlaying = true;
        skipRequested = false;
        finished = false;
        failed = false;
        shownAt = Time.unscaledTime;
        skipHint.SetActive(false);
        screen.texture = null;
        SetVisible(true);

        player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = false;
        player.waitForFirstFrame = true;
        player.skipOnDrop = true;
        // The flow holds timeScale at 0 while a cutscene is up.
        player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.audioOutputMode = VideoAudioOutputMode.Direct;
        player.errorReceived += OnError;
        player.loopPointReached += OnFinished;

        if (clip != null)
        {
            player.source = VideoSource.VideoClip;
            player.clip = clip;
        }
        else
        {
            player.source = VideoSource.Url;
            player.url = url;
        }

        player.Prepare();
        float waited = 0f;
        while (!player.isPrepared && !failed && !skipRequested && waited < prepareTimeout)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (player.isPrepared && !failed && !skipRequested)
        {
            int width = Mathf.Max(16, (int)player.width);
            int height = Mathf.Max(16, (int)player.height);
            target = new RenderTexture(width, height, 0);
            target.Create();
            player.targetTexture = target;
            screen.texture = target;
            fitter.aspectRatio = width / (float)height;

            for (ushort track = 0; track < player.audioTrackCount; track++)
            {
                player.EnableAudioTrack(track, true);
                player.SetDirectAudioVolume(track, 1f);
            }

            player.Play();

            float limit = player.length > 0.0 ? (float)player.length + overrunAllowance : 180f;
            float elapsed = 0f;
            while (!finished && !failed && !skipRequested && elapsed < limit)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        Cleanup();
    }

    void Update()
    {
        if (!IsPlaying)
        {
            return;
        }

        if (!skipHint.activeSelf && Time.unscaledTime - shownAt >= skipArmDelay)
        {
            skipHint.SetActive(true);
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null &&
            (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame ||
             keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
        {
            RequestSkip();
        }
    }

    void RequestSkip()
    {
        if (IsPlaying && Time.unscaledTime - shownAt >= skipArmDelay)
        {
            skipRequested = true;
        }
    }

    void OnError(VideoPlayer source, string message)
    {
        Debug.LogWarning("[CutscenePlayer] Video failed, skipping: " + message);
        failed = true;
    }

    void OnFinished(VideoPlayer source)
    {
        finished = true;
    }

    void Cleanup()
    {
        if (player != null)
        {
            player.errorReceived -= OnError;
            player.loopPointReached -= OnFinished;
            player.Stop();
            Destroy(player);
            player = null;
        }

        screen.texture = null;
        if (target != null)
        {
            target.Release();
            Destroy(target);
            target = null;
        }

        SetVisible(false);
        IsPlaying = false;
    }

    void SetVisible(bool visible)
    {
        group.alpha = visible ? 1f : 0f;
        group.blocksRaycasts = visible;
        group.interactable = visible;
    }

    void OnDestroy()
    {
        if (target != null)
        {
            target.Release();
            Destroy(target);
        }
    }
}
