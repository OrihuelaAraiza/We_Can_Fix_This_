using UnityEngine;

/// <summary>
/// Plays footstep audio tied to this player's movement speed.
/// Added automatically by PlayerManager to every spawned player.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PlayerAudioController : MonoBehaviour
{
    [Header("Footsteps")]
    [SerializeField] AudioClip walkingClip;
    [SerializeField] float     minSpeedThreshold = 0.15f;

    AudioSource footstepSource;
    PlayerMovement movement;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();

        footstepSource = GetComponent<AudioSource>();
        footstepSource.clip        = null;
        footstepSource.loop        = true;
        footstepSource.spatialBlend = 0f;
        footstepSource.volume      = 0.45f;
        footstepSource.playOnAwake = false;
        footstepSource.dopplerLevel = 0f;
    }

    void Start()
    {
        // Grab the clip from the AudioManager if not set directly
        if (walkingClip == null && AudioManager.Instance != null)
            walkingClip = AudioManager.Instance.WalkingClip;

        if (walkingClip != null)
            footstepSource.clip = walkingClip;
    }

    void Update()
    {
        if (movement == null || !movement.IsInitialized || footstepSource.clip == null)
            return;

        bool shouldPlay = movement.IsGrounded && movement.PlanarSpeed > minSpeedThreshold;

        if (shouldPlay && !footstepSource.isPlaying)
            footstepSource.Play();
        else if (!shouldPlay && footstepSource.isPlaying)
            footstepSource.Stop();

        // Scale pitch slightly with speed for feel
        if (footstepSource.isPlaying)
            footstepSource.pitch = Mathf.Lerp(0.9f, 1.2f, movement.SpeedNormalized);
    }

    void OnDisable()
    {
        if (footstepSource != null && footstepSource.isPlaying)
            footstepSource.Stop();
    }
}
