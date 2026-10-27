using System;
using UnityEngine;

/// <summary>
/// Self-bootstrapping 2D audio for gameplay SFX and level music.
/// Clips come from Resources/AudioLibrary; call sites use the static Play* helpers.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    const string LibraryResource = "AudioLibrary";
    const int SfxVoices = 6;
    const float MusicFadePerSecond = 0.8f;

    static AudioClip generatedLoop;
    static float flowMusicScale = 1f;
    static bool menuMode;

    AudioLibrary library;
    AudioSource[] sfxSources;
    int nextSfx;
    AudioSource musicSource;
    PlayerControllerX player;

    public static AudioManager Ensure()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject managerObject = new GameObject("AudioManager");
        return managerObject.AddComponent<AudioManager>();
    }

    public static void PlayShoot() => Play(l => l.shoot, 0.45f, 0.06f);
    public static void PlayCorrectHit() => Play(l => l.correctHit, 0.9f, 0f);
    public static void PlayWrongShot() => Play(l => l.wrongShot, 0.6f, 0.03f);
    public static void PlayCrash() => Play(l => l.crash, 0.85f, 0.04f);
    public static void PlayStimulusCollect() => Play(l => l.stimulusCollect, 0.8f, 0f);
    public static void PlayLaneShift() => Play(l => l.laneShift, 0.35f, 0.08f);

    /// <summary>Extra music ceiling set by LevelFlowController, for example 0 while a cutscene video plays.</summary>
    public static void SetFlowMusicScale(float scale)
    {
        flowMusicScale = Mathf.Clamp01(scale);
    }

    /// <summary>Level select: swaps to menuMusic if assigned, otherwise plays the level music quietly.</summary>
    public static void SetMenuMode(bool menu)
    {
        menuMode = menu;
        AudioManager manager = Ensure();
        if (manager.musicSource == null || manager.library == null)
        {
            return;
        }

        AudioClip wanted = manager.SelectMusicClip();
        if (manager.musicSource.clip == wanted)
        {
            return;
        }

        manager.musicSource.Stop();
        manager.musicSource.clip = wanted;
        manager.musicSource.volume = 0f;
        if (wanted != null)
        {
            manager.musicSource.Play();
        }
    }

    static void Play(Func<AudioLibrary, AudioClip> select, float volume, float pitchJitter)
    {
        AudioManager manager = Ensure();
        if (manager.library == null)
        {
            return;
        }

        manager.PlayClip(select(manager.library), volume, pitchJitter);
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

        library = Resources.Load<AudioLibrary>(LibraryResource);
        if (library == null)
        {
            Debug.LogWarning("AudioManager: Resources/" + LibraryResource + " not found. Running without SFX.");
            library = ScriptableObject.CreateInstance<AudioLibrary>();
        }

        sfxSources = new AudioSource[SfxVoices];
        for (int i = 0; i < SfxVoices; i++)
        {
            sfxSources[i] = CreateSource(false);
        }

        musicSource = CreateSource(true);
        StartMusic();
    }

    AudioSource CreateSource(bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        return source;
    }

    AudioClip SelectMusicClip()
    {
        if (menuMode && library.menuMusic != null)
        {
            return library.menuMusic;
        }

        AudioClip clip = library.levelMusic;
        if (clip == null && library.useProceduralMusicFallback)
        {
            if (generatedLoop == null)
            {
                generatedLoop = ProceduralSynthLoop.Build();
            }

            clip = generatedLoop;
        }

        return clip;
    }

    void StartMusic()
    {
        AudioClip clip = SelectMusicClip();
        if (clip == null)
        {
            return;
        }

        musicSource.clip = clip;
        musicSource.volume = library.musicVolume;
        musicSource.Play();
    }

    // Separate voices so pitch jitter on one shot does not bend clips already playing.
    void PlayClip(AudioClip clip, float volume, float pitchJitter)
    {
        if (clip == null || sfxSources == null)
        {
            return;
        }

        AudioSource source = sfxSources[nextSfx];
        nextSfx = (nextSfx + 1) % sfxSources.Length;

        source.clip = clip;
        source.volume = Mathf.Clamp01(volume * library.sfxVolume);
        source.pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
        source.Play();
    }

    void Update()
    {
        if (musicSource == null || musicSource.clip == null)
        {
            return;
        }

        float scale = 1f;
        if (IsGameOver())
        {
            scale = library.finishedMusicScale;
        }
        else if (GameHUD.Instance != null && GameHUD.Instance.IsPaused)
        {
            scale = library.pausedMusicScale;
        }
        else if (menuMode && library.menuMusic == null)
        {
            scale = library.menuMusicScale;
        }

        scale = Mathf.Min(scale, flowMusicScale);

        // Unscaled so the duck still happens while the pause menu has timeScale at 0.
        musicSource.volume = Mathf.MoveTowards(
            musicSource.volume,
            library.musicVolume * scale,
            MusicFadePerSecond * Time.unscaledDeltaTime);
    }

    bool IsGameOver()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerControllerX>();
        }

        return player != null && player.isGameOver;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
