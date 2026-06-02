using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class LoadingScreenController : MonoBehaviour
{
    public static LoadingScreenController Instance { get; private set; }

    [SerializeField] private VideoPlayer videoPlayer;
    [Tooltip("CanvasGroup wrapping the fullscreen RawImage that shows the video.")]
    [SerializeField] private CanvasGroup videoCanvasGroup;
    [SerializeField] private float fadeInDuration  = 0.5f;
    [SerializeField] private float fadeOutDuration = 0.5f;
    [Tooltip("Maximum seconds the video plays before cutting to fade-out. Set to 0 to play the full clip.")]
    [SerializeField] private float maxPlayDuration = 0f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Ensure the panel is hidden at startup
        if (videoCanvasGroup != null)
        {
            videoCanvasGroup.alpha = 0f;
            videoCanvasGroup.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Prepares the video, fades in, plays (up to maxPlayDuration if set),
    /// fades out, then returns so the caller can reload/respawn.
    /// </summary>
    public IEnumerator Play()
    {
        // ── Activate panel (invisible) so the VideoPlayer is enabled ─────────
        videoCanvasGroup.alpha = 0f;
        videoCanvasGroup.gameObject.SetActive(true);

        // ── Prepare ───────────────────────────────────────────────────────────
        videoPlayer.Stop();
        videoPlayer.Prepare();
        while (!videoPlayer.isPrepared)
            yield return null;

        // Play then immediately pause — this forces frame 0 onto the RenderTexture
        // before the panel becomes visible, eliminating the snap.
        videoPlayer.Play();
        videoPlayer.Pause();
        yield return null; // one frame for the texture to update
        yield return null; // second frame as safety margin

        // ── Fade in ───────────────────────────────────────────────────────────
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            videoCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }
        videoCanvasGroup.alpha = 1f;

        // ── Resume playback ───────────────────────────────────────────────────
        videoPlayer.Play();

        float playElapsed = 0f;
        bool  hasLimit    = maxPlayDuration > 0f;

        while (videoPlayer.isPlaying)
        {
            playElapsed += Time.deltaTime;
            if (hasLimit && playElapsed >= maxPlayDuration)
            {
                videoPlayer.Stop();
                break;
            }
            yield return null;
        }

        // ── Fade out ──────────────────────────────────────────────────────────
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            videoCanvasGroup.alpha = Mathf.Clamp01(1f - elapsed / fadeOutDuration);
            yield return null;
        }
        videoCanvasGroup.alpha = 0f;
        videoCanvasGroup.gameObject.SetActive(false);
    }
}