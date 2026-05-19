using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central audio manager. Persists across scenes.
/// Attach to a GameObject in the Bootstrap or first scene.
/// Assign AudioClips from the Inspector.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // ── Music ──────────────────────────────────────────────────
    [Header("Music")]
    [SerializeField] AudioClip lobbyMusic;
    [SerializeField] AudioClip gameplayMusic;
    [SerializeField] AudioClip gameplayIntenseMusic;
    [SerializeField] AudioClip failStateMusic;

    // ── Ambient (one chosen at random per session) ─────────────
    [Header("Ship Ambient (picks one at random)")]
    [SerializeField] AudioClip[] shipAmbientClips;

    // ── Player SFX ─────────────────────────────────────────────
    [Header("Player SFX")]
    [SerializeField] AudioClip playerWalkingClip;

    // ── Station SFX ────────────────────────────────────────────
    [Header("Station SFX")]
    [SerializeField] AudioClip   stationDamagedClip;
    [SerializeField] AudioClip   stationHealthyClip;
    [SerializeField] AudioClip[] stationRepairClips;

    // ── Public clip accessors (used by PlayerAudioController) ──
    public AudioClip WalkingClip => playerWalkingClip;

    // ── Audio sources ──────────────────────────────────────────
    AudioSource musicSource;
    AudioSource ambientSource;
    AudioSource sfxSource;

    float musicVolume  = 0.7f;
    float ambientVolume = 0.35f;
    float sfxVolume    = 1f;

    // ── Lifecycle ──────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildAudioSources();
    }

    bool isPlayingIntense;
    bool shipCriticalMusic;
    bool stationEmergencyMusic;
    bool timeCriticalMusic;
    bool bossModeMusic;
    Coroutine musicTransition;

    void Update()
    {
        if (SceneManager.GetActiveScene().name != "03_Gameplay")
            return;

        bool hasStationEmergency = HasEmergencyStations();
        if (stationEmergencyMusic == hasStationEmergency)
            return;

        stationEmergencyMusic = hasStationEmergency;
        UpdateGameplayMusicState(0.75f);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded        += OnSceneLoaded;
        FailureSystem.OnStationFailed   += OnStationFailed;
        FailureSystem.OnStationRepaired += OnStationRepaired;
        RepairStation.OnStateChanged    += OnStationStateChanged;
        GameManager.OnGameOver          += OnGameOver;
        GameManager.OnGameWon           += OnGameWon;
        CoreXBrain.OnBossModeActivated  += OnBossModeActivated;
        ShipHealth.OnShipCritical       += OnShipCritical;
        ShipHealth.OnShipRecovered      += OnShipRecovered;
        SurvivalTimerUI.OnTimeCritical  += OnTimeCritical;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded        -= OnSceneLoaded;
        FailureSystem.OnStationFailed   -= OnStationFailed;
        FailureSystem.OnStationRepaired -= OnStationRepaired;
        RepairStation.OnStateChanged    -= OnStationStateChanged;
        GameManager.OnGameOver          -= OnGameOver;
        GameManager.OnGameWon           -= OnGameWon;
        CoreXBrain.OnBossModeActivated  -= OnBossModeActivated;
        ShipHealth.OnShipCritical       -= OnShipCritical;
        ShipHealth.OnShipRecovered      -= OnShipRecovered;
        SurvivalTimerUI.OnTimeCritical  -= OnTimeCritical;
    }

    // ── Scene routing ──────────────────────────────────────────

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        switch (scene.name)
        {
            case "02_Lobby":
                PlayMusic(lobbyMusic, loop: true);
                StopAmbient();
                break;

            case "03_Gameplay":
                isPlayingIntense = false;
                shipCriticalMusic = false;
                stationEmergencyMusic = HasEmergencyStations();
                timeCriticalMusic = false;
                bossModeMusic = false;
                PlayMusic(gameplayMusic, loop: true);
                UpdateGameplayMusicState(0.25f);
                PlayRandomAmbient();
                break;

            default:
                StopMusic();
                StopAmbient();
                break;
        }
    }

    // ── Event handlers ─────────────────────────────────────────

    void OnStationFailed(RepairStation _)
    {
        PlaySFX(stationDamagedClip);
        RefreshStationEmergencyMusic();
    }

    void OnStationRepaired(RepairStation _)
    {
        PlaySFX(stationHealthyClip);
        RefreshStationEmergencyMusic();
    }

    void OnStationStateChanged(RepairStation station,
        RepairStation.StationState prev,
        RepairStation.StationState next)
    {
        if (next == RepairStation.StationState.Repairing
            && prev == RepairStation.StationState.Broken)
        {
            PlayRandomSFX(stationRepairClips);
        }

        RefreshStationEmergencyMusic();
    }

    void OnGameOver()
    {
        StopAmbient();
        PlayMusic(failStateMusic, loop: false);
    }

    void OnGameWon()
    {
        StopAmbient();
        StopMusic();
    }

    void OnBossModeActivated()
    {
        bossModeMusic = true;
        UpdateGameplayMusicState(1.5f);
    }

    void OnShipCritical()
    {
        shipCriticalMusic = true;
        UpdateGameplayMusicState(2f);
    }

    void OnTimeCritical()
    {
        timeCriticalMusic = true;
        UpdateGameplayMusicState(2f);
    }

    void OnShipRecovered()
    {
        shipCriticalMusic = false;
        UpdateGameplayMusicState(2f);
    }

    // ── Public API ─────────────────────────────────────────────

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    public void PlayRandomSFX(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;
        AudioClip chosen = clips[Random.Range(0, clips.Length)];
        PlaySFX(chosen);
    }

    // ── Internal helpers ───────────────────────────────────────

    void PlayMusic(AudioClip clip, bool loop)
    {
        if (clip == null || musicSource == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        StopMusicTransition();
        musicSource.loop = loop;
        musicSource.clip = clip;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    void UpdateGameplayMusicState(float crossfadeDuration)
    {
        bool shouldPlayIntense = shipCriticalMusic || stationEmergencyMusic || timeCriticalMusic || bossModeMusic;

        if (shouldPlayIntense == isPlayingIntense)
            return;

        AudioClip targetClip = shouldPlayIntense ? gameplayIntenseMusic : gameplayMusic;
        if (targetClip == null)
            return;

        isPlayingIntense = shouldPlayIntense;
        StartMusicTransition(CrossfadeMusic(targetClip, crossfadeDuration));
    }

    void RefreshStationEmergencyMusic()
    {
        stationEmergencyMusic = HasEmergencyStations();
        UpdateGameplayMusicState(2f);
    }

    static bool HasEmergencyStations()
    {
        foreach (RepairStation station in RepairStation.ActiveStations)
        {
            if (station == null)
                continue;

            if (station.State == RepairStation.StationState.Broken
                || station.State == RepairStation.StationState.Repairing)
                return true;
        }

        return false;
    }

    void StopMusic()
    {
        StopMusicTransition();
        if (musicSource != null)
            musicSource.Stop();
    }

    void PlayRandomAmbient()
    {
        if (shipAmbientClips == null || shipAmbientClips.Length == 0 || ambientSource == null)
            return;

        AudioClip chosen = shipAmbientClips[Random.Range(0, shipAmbientClips.Length)];
        if (chosen == null) return;

        ambientSource.clip = chosen;
        ambientSource.loop = true;
        ambientSource.volume = ambientVolume;
        ambientSource.Play();
    }

    void StopAmbient()
    {
        if (ambientSource != null)
            ambientSource.Stop();
    }

    IEnumerator CrossfadeMusic(AudioClip newClip, float duration)
    {
        if (newClip == null || musicSource == null)
            yield break;

        float startVol = musicSource.volume;
        float t = 0f;

        while (t < duration * 0.5f)
        {
            t += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(startVol, 0f, t / (duration * 0.5f));
            yield return null;
        }

        musicSource.clip = newClip;
        musicSource.Play();
        t = 0f;

        while (t < duration * 0.5f)
        {
            t += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(0f, musicVolume, t / (duration * 0.5f));
            yield return null;
        }

        musicSource.volume = musicVolume;
        musicTransition = null;
    }

    IEnumerator CrossfadeToStop(float duration)
    {
        float startVol = musicSource.volume;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(startVol, 0f, t / duration);
            yield return null;
        }

        musicSource.Stop();
        musicSource.clip = null;
        musicSource.volume = musicVolume;
        musicTransition = null;
    }

    void StartMusicTransition(IEnumerator transition)
    {
        StopMusicTransition();
        musicTransition = StartCoroutine(transition);
    }

    void StopMusicTransition()
    {
        if (musicTransition == null)
            return;

        StopCoroutine(musicTransition);
        musicTransition = null;
    }

    void BuildAudioSources()
    {
        musicSource = GetComponent<AudioSource>();
        ConfigureSource(musicSource, loop: true, volume: musicVolume, is2D: true);

        ambientSource = gameObject.AddComponent<AudioSource>();
        ConfigureSource(ambientSource, loop: true, volume: ambientVolume, is2D: true);

        sfxSource = gameObject.AddComponent<AudioSource>();
        ConfigureSource(sfxSource, loop: false, volume: sfxVolume, is2D: true);
    }

    static void ConfigureSource(AudioSource src, bool loop, float volume, bool is2D)
    {
        if (src == null) return;
        src.loop              = loop;
        src.volume            = volume;
        src.spatialBlend      = is2D ? 0f : 1f;
        src.playOnAwake       = false;
        src.dopplerLevel      = 0f;
    }
}
