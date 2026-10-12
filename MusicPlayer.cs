using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An interactable music player. Attach to a GameObject that also has an
/// AudioSource and a Collider (needed for click interaction).
/// </summary>
[RequireComponent(typeof(AudioSource))]
[AddComponentMenu("Audio/Music Player")]
public class MusicPlayer : MonoBehaviour
{
    public enum PlayerState { Stopped, Playing, Paused }

    [Header("Playlist")]
    [Tooltip("Tracks are played in list order (or shuffled if Shuffle is on).")]
    [SerializeField] private List<AudioClip> playlist = new List<AudioClip>();

    [Header("Playback Options")]
    [SerializeField] private bool shuffle = false;
    [SerializeField] private bool loopPlaylist = true;
    [SerializeField] private bool autoPlayOnStart = false;

    [Tooltip("Pressing 'Previous' restarts the current track if it is further along than this (seconds).")]
    [SerializeField] private float restartThreshold = 3f;

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.8f;
    [SerializeField] private bool muted = false;

    // ---------------------------------------------------------------- events
    /// <summary>(playlistIndex, clip)</summary>
    public event Action<int, AudioClip> TrackChanged;
    public event Action<PlayerState> StateChanged;
    /// <summary>Fires with the effective volume (0 while muted).</summary>
    public event Action<float> VolumeChanged;

    // ---------------------------------------------------------------- private
    private AudioSource source;

    /// <summary>Indices into the playlist, in the order they will be played.</summary>
    private readonly List<int> playOrder = new List<int>();
    private int orderIndex = -1;    // position inside playOrder
    private int currentIndex = -1;  // position inside playlist

    private PlayerState state = PlayerState.Stopped;

    // ------------------------------------------------------------ properties
    public PlayerState State => state;
    public bool IsPlaying => state == PlayerState.Playing;
    public bool IsPaused => state == PlayerState.Paused;
    public bool IsMuted => muted;
    public float Volume => volume;
    public int TrackCount => playlist.Count;
    public int CurrentTrackIndex => currentIndex;

    public AudioClip CurrentClip =>
        (currentIndex >= 0 && currentIndex < playlist.Count) ? playlist[currentIndex] : null;

    public float TrackLength => source != null && source.clip != null ? source.clip.length : 0f;
    public float TrackTime => source != null ? source.time : 0f;
    public float TrackProgress => TrackLength > 0f ? Mathf.Clamp01(source.time / TrackLength) : 0f;

    public bool Shuffle
    {
        get => shuffle;
        set
        {
            if (shuffle == value) return;
            shuffle = value;
            RebuildPlayOrder(keepCurrentTrack: true);
        }
    }

    // ------------------------------------------------------------- lifecycle
    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;                 // looping is handled per-playlist
        source.volume = muted ? 0f : volume;

        RebuildPlayOrder(keepCurrentTrack: false);
    }

    private void Start()
    {
        if (autoPlayOnStart && playlist.Count > 0)
            PlayIndex(0);
    }

    private void Update()
    {
        // Detect a track finishing naturally.
        if (state == PlayerState.Playing && !source.isPlaying)
        {
            if (source.clip == null || source.clip.length <= 0f) return; // guard against empty clips
            Advance(+1);
        }
    }

    private void OnValidate()
    {
        volume = Mathf.Clamp01(volume);
        restartThreshold = Mathf.Max(0f, restartThreshold);
    }

    // ---------------------------------------------------------- public API

    /// <summary>Play if stopped/paused, pause if playing. This is what a click usually calls.</summary>
    public void PlayPause()
    {
        switch (state)
        {
            case PlayerState.Playing: Pause(); break;
            case PlayerState.Paused: Resume(); break;
            default: PlayTrack(currentIndex >= 0 ? currentIndex : 0); break;
        }
    }

    public void Play()
    {
        if (state == PlayerState.Paused) Resume();
        else PlayTrack(currentIndex >= 0 ? currentIndex : 0);
    }

    public void Pause()
    {
        if (state != PlayerState.Playing) return;
        source.Pause();
        SetState(PlayerState.Paused);
    }

    public void Resume()
    {
        if (state != PlayerState.Paused) return;
        source.UnPause();
        SetState(PlayerState.Playing);
    }

    public void Stop()
    {
        if (state == PlayerState.Stopped) return;
        source.Stop();
        SetState(PlayerState.Stopped);
    }

    public void NextTrack()
    {
        if (playlist.Count == 0) return;
        if (orderIndex < 0) { PlayIndex(0); return; }
        Advance(+1);
    }

    public void PreviousTrack()
    {
        if (playlist.Count == 0) return;
        if (orderIndex < 0) { PlayIndex(0); return; }

        // Restart the current song if we're already a few seconds in.
        if (source.clip != null && source.time > restartThreshold)
        {
            PlayIndex(orderIndex);
            return;
        }
        Advance(-1);
    }

    /// <summary>Jump straight to a playlist index.</summary>
    public void PlayTrack(int trackIndex)
    {
        if (playlist.Count == 0) return;

        trackIndex = Mathf.Clamp(trackIndex, 0, playlist.Count - 1);
        int pos = playOrder.IndexOf(trackIndex);

        if (pos < 0)
        {
            RebuildPlayOrder(keepCurrentTrack: false);
            pos = playOrder.IndexOf(trackIndex);
        }
        if (pos >= 0) PlayIndex(pos);
    }

    public void SetVolume(float value)
    {
        volume = Mathf.Clamp01(value);
        if (!muted) source.volume = volume;
        VolumeChanged?.Invoke(muted ? 0f : volume);
    }

    public void ToggleMute() => SetMuted(!muted);

    public void SetMuted(bool value)
    {
        muted = value;
        source.volume = muted ? 0f : volume;
        VolumeChanged?.Invoke(muted ? 0f : volume);
    }

    /// <summary>Seek within the current track. 0 = start, 1 = end.</summary>
    public void Seek(float normalized01)
    {
        if (source.clip == null) return;
        source.time = Mathf.Clamp01(normalized01) * source.clip.length;
    }

    // --------------------------------------------------------------- internals

    private void Advance(int direction)
    {
        int next = orderIndex + direction;

        if (next >= playOrder.Count)
        {
            if (!loopPlaylist) { Stop(); return; }
            RebuildPlayOrder(keepCurrentTrack: false); // reshuffles if shuffle is on
            next = 0;
        }
        else if (next < 0)
        {
            if (!loopPlaylist) { PlayIndex(0); return; }
            next = playOrder.Count - 1;
        }

        PlayIndex(next);
    }

    private void PlayIndex(int pos)
    {
        if (playOrder.Count == 0 || pos < 0 || pos >= playOrder.Count) return;

        orderIndex = pos;
        currentIndex = playOrder[pos];

        source.clip = playlist[currentIndex];
        source.volume = muted ? 0f : volume;
        source.Play();

        SetState(PlayerState.Playing);
        TrackChanged?.Invoke(currentIndex, source.clip);
    }

    private void SetState(PlayerState newState)
    {
        if (state == newState) return;
        state = newState;
        StateChanged?.Invoke(state);
    }

    private void RebuildPlayOrder(bool keepCurrentTrack)
    {
        int keep = keepCurrentTrack ? currentIndex : -1;

        playOrder.Clear();
        for (int i = 0; i < playlist.Count; i++) playOrder.Add(i);

        if (shuffle)
        {
            // Fisher-Yates
            for (int i = playOrder.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (playOrder[i], playOrder[j]) = (playOrder[j], playOrder[i]);
            }
        }

        orderIndex = keep >= 0 ? playOrder.IndexOf(keep) : -1;
    }
}