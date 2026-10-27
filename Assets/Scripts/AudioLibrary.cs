using UnityEngine;

/// <summary>
/// Clip assignments for <see cref="AudioManager"/>. The shipped instance lives at
/// Assets/Resources/AudioLibrary.asset so only the referenced clips go into builds.
/// </summary>
[CreateAssetMenu(fileName = "AudioLibrary", menuName = "Astro Bunny/Audio Library")]
public class AudioLibrary : ScriptableObject
{
    [Header("SFX")]
    public AudioClip shoot;
    public AudioClip correctHit;
    public AudioClip wrongShot;
    public AudioClip crash;
    public AudioClip stimulusCollect;
    public AudioClip laneShift;

    [Header("Music")]
    [Tooltip("Level 1 BGM. Leave empty to use the generated synth loop.")]
    public AudioClip levelMusic;
    [Tooltip("Level select and level clear. Leave empty to play the level music quietly.")]
    public AudioClip menuMusic;
    public bool useProceduralMusicFallback = true;

    [Header("Mix")]
    [Range(0f, 1f)] public float sfxVolume = 0.8f;
    [Range(0f, 1f)] public float musicVolume = 0.3f;
    [Tooltip("Music level while the pause menu is open, as a fraction of musicVolume.")]
    [Range(0f, 1f)] public float pausedMusicScale = 0.35f;
    [Tooltip("Music level on the FINISHED screen, as a fraction of musicVolume.")]
    [Range(0f, 1f)] public float finishedMusicScale = 0.4f;
    [Tooltip("Level-music level on the carousel when menuMusic is empty.")]
    [Range(0f, 1f)] public float menuMusicScale = 0.45f;
}
