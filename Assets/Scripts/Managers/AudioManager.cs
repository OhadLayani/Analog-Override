using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Plays the music and sound effects; lives across scenes. Other scripts call AudioManager.Instance?.PlayX().</summary>
[DefaultExecutionOrder(-55)] // After GameManager (-60), before SpringManager (-50)
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    /// <summary>One sound in the Inspector: a clip plus its own volume.</summary>
    [System.Serializable]
    public class SoundClip
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [Header("Master volumes")]
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

    [Header("Music (looped)")]
    [Tooltip("One track per scene, in Build Settings order: 0 = MainMenu, 1 = Tutorial, 2 = Level1-Attic, 3 = Level2-Bedroom. An empty clip means silence in that scene.")]
    [SerializeField] private SoundClip[] musicByBuildIndex = new SoundClip[4];

    [Header("Sound effects")]
    [SerializeField] private SoundClip buttonClick = new SoundClip();
    [Tooltip("Looped while the robot walks.")]
    [SerializeField] private SoundClip walk = new SoundClip();
    [Tooltip("Optional: looped while walking on carpet. Empty = the normal walk sound.")]
    [SerializeField] private SoundClip walkOnCarpet = new SoundClip();
    [SerializeField] private SoundClip attack = new SoundClip();
    [SerializeField] private SoundClip stretch = new SoundClip();

    private AudioSource musicSource;
    private AudioSource sfxSource;
    private AudioSource loopSfxSource;

    private SoundClip currentMusic;
    private SoundClip currentLoop;
    private bool wantsWalking;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        musicSource = CreateSource(loop: true);
        sfxSource = CreateSource(loop: false);
        loopSfxSource = CreateSource(loop: true);

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        // The first scene was already loaded before this object existed, so start its music here.
        PlayMusicForScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Instance = null;
    }

    private void Update()
    {
        // Volumes are re-applied every frame, so moving a slider during Play is heard at once.
        if (currentMusic != null) musicSource.volume = musicVolume * currentMusic.volume;
        if (currentLoop != null) loopSfxSource.volume = sfxVolume * currentLoop.volume;

        // The walk loop goes quiet while paused (the player's Update doesn't run then).
        bool paused = GameManager.Instance != null && GameManager.Instance.IsGamePaused;
        bool shouldPlay = wantsWalking && !paused && loopSfxSource.clip != null;
        if (shouldPlay && !loopSfxSource.isPlaying) loopSfxSource.Play();
        else if (!shouldPlay && loopSfxSource.isPlaying) loopSfxSource.Pause();
    }

    private AudioSource CreateSource(bool loop)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f; // 2D
        return source;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetWalking(false, false);
        PlayMusicForScene(scene.buildIndex);
    }

    private void PlayMusicForScene(int buildIndex)
    {
        if (buildIndex >= 0 && buildIndex < musicByBuildIndex.Length)
            PlayMusic(musicByBuildIndex[buildIndex]);
        else
            StopMusic();
    }

    /// <summary>Loops a music track; keeps going without restarting if that track is already playing.</summary>
    public void PlayMusic(SoundClip music)
    {
        if (music == null || music.clip == null)
        {
            StopMusic();
            return;
        }

        currentMusic = music;
        musicSource.volume = musicVolume * music.volume;
        if (musicSource.clip == music.clip && musicSource.isPlaying) return;

        musicSource.clip = music.clip;
        musicSource.Play();
    }

    public void StopMusic()
    {
        currentMusic = null;
        musicSource.Stop();
        musicSource.clip = null;
    }

    public void PlayButtonClick() => PlaySfx(buttonClick);
    public void PlayAttack() => PlaySfx(attack);
    public void PlayStretch() => PlaySfx(stretch);

    /// <summary>Starts or stops the looped walking sound; called every frame by the player.</summary>
    public void SetWalking(bool walking, bool onCarpet)
    {
        wantsWalking = walking;
        if (!walking)
        {
            if (loopSfxSource != null) loopSfxSource.Stop();
            return;
        }

        var sound = onCarpet && walkOnCarpet.clip != null ? walkOnCarpet : walk;
        if (sound.clip == null || loopSfxSource.clip == sound.clip) return;

        currentLoop = sound;
        loopSfxSource.clip = sound.clip;
        loopSfxSource.volume = sfxVolume * sound.volume;
        loopSfxSource.Play();
    }

    private void PlaySfx(SoundClip sound)
    {
        if (sound == null || sound.clip == null) return;
        sfxSource.PlayOneShot(sound.clip, sfxVolume * sound.volume);
    }
}
