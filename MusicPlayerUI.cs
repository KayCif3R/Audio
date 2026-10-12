using UnityEngine;
using UnityEngine.UI;

public class MusicPlayerUI : MonoBehaviour
{
    [SerializeField] private MusicPlayer player;
    [SerializeField] private Button playPauseButton;
    [SerializeField] private Text trackLabel;
    [SerializeField] private Slider volumeSlider;

    void Start()
    {
        if (player == null) return;

        playPauseButton.onClick.AddListener(player.PlayPause);
        volumeSlider.onValueChanged.AddListener(player.SetVolume);

        player.TrackChanged += OnTrackChanged;
        player.StateChanged += OnStateChanged;
        player.VolumeChanged += OnVolumeChanged;

        // Initialise UI
        OnStateChanged(player.State);
        volumeSlider.SetValueWithoutNotify(player.Volume);
        if (player.CurrentClip != null)
            OnTrackChanged(player.CurrentTrackIndex, player.CurrentClip);
    }

    void OnDestroy()
    {
        if (player == null) return;
        player.TrackChanged -= OnTrackChanged;
        player.StateChanged -= OnStateChanged;
        player.VolumeChanged -= OnVolumeChanged;
    }

    void OnTrackChanged(int index, AudioClip clip)
    {
        if (trackLabel != null)
            trackLabel.text = clip != null ? clip.name : "No Track";
    }

    void OnStateChanged(MusicPlayer.PlayerState state)
    {
        if (playPauseButton != null)
        {
            var label = playPauseButton.GetComponentInChildren<Text>();
            if (label != null)
                label.text = state == MusicPlayer.PlayerState.Playing ? "Pause" : "Play";
        }
    }

    void OnVolumeChanged(float volume)
    {
        if (volumeSlider != null)
            volumeSlider.SetValueWithoutNotify(volume);
    }
}