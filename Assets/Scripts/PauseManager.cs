using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }
    public static bool IsPaused { get; private set; }
    public static bool IsQuittingToMenu { get; private set; }

    [Header("Pause UI")]
    [Tooltip("Root GameObject that contains all pause screen UI elements.")]
    [SerializeField] private GameObject pauseScreenRoot;

    [Header("Post Processing")]
    [Tooltip("The Volume holding the DepthOfField override. Can be the same as UIController's volume.")]
    [SerializeField] private Volume postProcessingVolume;
    [Tooltip("How blurry the gaussian DOF gets at full pause blur (gaussianMaxRadius).")]
    [SerializeField] private float dofBlurTarget   = 1.8f;
    [Tooltip("Speed of the DOF blur lerp when pausing (higher = faster).")]
    [SerializeField] private float dofLerpInSpeed  = 6f;
    [Tooltip("Speed of the DOF blur lerp when unpausing (higher = faster).")]
    [SerializeField] private float dofLerpOutSpeed = 6f;

    [Header("Audio")]
    [SerializeField] private AudioClip pauseSound;
    [SerializeField] private AudioClip unpauseSound;
    [SerializeField] private AudioSource pauseAudioSource;

    private DepthOfField _dof;
    private Coroutine    _dofCoroutine;

    // Saved state before pause blur so we can restore exactly what was there
    private bool               _prePauseActive;
    private DepthOfFieldMode   _prePauseMode;
    private float              _prePauseFocalLength;
    private float              _prePauseGaussianMaxRadius;
    private float              _prePauseGaussianStart;
    private float              _prePauseGaussianEnd;

    private struct AudioState
    {
        public AudioSource source;
        public bool        wasPlaying;
    }
    private readonly List<AudioState> _savedAudio = new List<AudioState>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (postProcessingVolume != null)
            postProcessingVolume.profile.TryGet(out _dof);
    }

    void Update()
    {
        if (MainMenuController.IsInMenuSequence) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsPaused) Unpause();
            else          Pause();
        }
    }

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;

        // Freeze time — stops NavMesh agents, physics, animations, and scaled coroutines
        Time.timeScale = 0f;

        // Save and pause every AudioSource in the scene (all of them, no exceptions)
        _savedAudio.Clear();
        foreach (var src in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            _savedAudio.Add(new AudioState { source = src, wasPlaying = src.isPlaying });
            if (src.isPlaying) src.Pause();
        }

        // Play pause sound AFTER the loop so it isn't immediately paused
        if (pauseAudioSource != null && pauseSound != null)
            pauseAudioSource.PlayOneShot(pauseSound);

        // Hide game HUD elements
        if (UIController.Instance != null)
        {
            if (UIController.Instance.UIEntryParent != null)
                UIController.Instance.UIEntryParent.SetActive(false);
            if (UIController.Instance.UIEntryCollectedParent != null)
                UIController.Instance.UIEntryCollectedParent.SetActive(false);
            if (UIController.Instance.CursorImage != null)
                UIController.Instance.CursorImage.gameObject.SetActive(false);
        }

        // Show pause screen
        if (pauseScreenRoot != null)
            pauseScreenRoot.SetActive(true);

        // Free the cursor for menu navigation
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        // Snapshot current DOF state so we can restore it perfectly on unpause
        if (_dof != null)
        {
            _prePauseActive           = _dof.active;
            _prePauseMode             = _dof.mode.value;
            _prePauseFocalLength      = _dof.focalLength.value;
            _prePauseGaussianMaxRadius = _dof.gaussianMaxRadius.value;
            _prePauseGaussianStart    = _dof.gaussianStart.value;
            _prePauseGaussianEnd      = _dof.gaussianEnd.value;
        }

        if (_dofCoroutine != null) StopCoroutine(_dofCoroutine);
        _dofCoroutine = StartCoroutine(LerpDOFIn());
    }

    public void Unpause()
    {
        if (!IsPaused) return;
        IsPaused = false;

        // Restore time
        Time.timeScale = 1f;

        // Resume only sources that were playing before the pause
        foreach (var state in _savedAudio)
        {
            if (state.source != null && state.wasPlaying)
                state.source.UnPause();
        }
        _savedAudio.Clear();

        if (pauseAudioSource != null && unpauseSound != null)
            pauseAudioSource.PlayOneShot(unpauseSound);

        // Hide pause screen
        if (pauseScreenRoot != null)
            pauseScreenRoot.SetActive(false);

        // Restore game HUD elements
        if (UIController.Instance != null)
        {
            if (UIController.Instance.UIEntryParent != null)
                UIController.Instance.UIEntryParent.SetActive(true);
            if (UIController.Instance.UIEntryCollectedParent != null)
                UIController.Instance.UIEntryCollectedParent.SetActive(true);
            if (UIController.Instance.CursorImage != null)
                UIController.Instance.CursorImage.gameObject.SetActive(true);
        }

        // Re-lock cursor — only when player is in Normal state (not dialogue/cutscene)
        bool shouldLock =
            playerController.Instance == null ||
            playerController.Instance.CurrentState == playerController.playerState.Normal;

        if (shouldLock)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        if (_dofCoroutine != null) StopCoroutine(_dofCoroutine);
        _dofCoroutine = StartCoroutine(LerpDOFOut());
    }

    /// <summary>Lerps gaussian blur IN (pause). Uses unscaledDeltaTime so it works at timeScale 0.</summary>
    private IEnumerator LerpDOFIn()
    {
        if (_dof == null) yield break;

        _dof.active = true;
        _dof.mode.Override(DepthOfFieldMode.Gaussian);
        _dof.gaussianStart.Override(0f);
        _dof.gaussianEnd.Override(0.01f);
        _dof.gaussianMaxRadius.Override(0f);   // force start at 0 so the lerp has range

        float elapsed  = 0f;
        float duration = Mathf.Max(1f / dofLerpInSpeed, 0.01f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _dof.gaussianMaxRadius.Override(Mathf.Lerp(0f, dofBlurTarget, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        _dof.gaussianMaxRadius.Override(dofBlurTarget);
        _dofCoroutine = null;
    }

    /// <summary>Lerps gaussian blur OUT (unpause), then restores the pre-pause DOF state exactly.</summary>
    private IEnumerator LerpDOFOut()
    {
        if (_dof == null) yield break;

        float start    = _dof.gaussianMaxRadius.value;
        float elapsed  = 0f;
        float duration = Mathf.Max(1f / dofLerpOutSpeed, 0.01f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _dof.gaussianMaxRadius.Override(Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        _dof.active = _prePauseActive;
        _dof.mode.Override(_prePauseMode);
        _dof.focalLength.Override(_prePauseFocalLength);
        _dof.gaussianMaxRadius.Override(_prePauseGaussianMaxRadius);
        _dof.gaussianStart.Override(_prePauseGaussianStart);
        _dof.gaussianEnd.Override(_prePauseGaussianEnd);

        _dofCoroutine = null;
    }

    public void QuitToMenu()
    {
        if (_quitCoroutine != null) return;
        _quitCoroutine = StartCoroutine(QuitToMenuCoroutine());
    }

    private Coroutine _quitCoroutine;

    [Header("Quit To Menu")]
    [SerializeField] private float quitFadeOutDuration = 4f;

    private IEnumerator QuitToMenuCoroutine()
    {
        // Restore timeScale immediately so deltaTime-driven effects run
        IsPaused = false;
        IsQuittingToMenu = true;
        Time.timeScale = 1f;

        foreach (var state in _savedAudio)
            if (state.source != null && state.wasPlaying)
                state.source.UnPause();
        _savedAudio.Clear();

        if (pauseScreenRoot != null)
            pauseScreenRoot.SetActive(false);

        // Stop any running DOF coroutine — we're driving it ourselves now
        if (_dofCoroutine != null) { StopCoroutine(_dofCoroutine); _dofCoroutine = null; }

        // Grab post-processing overrides exactly like LoopTrigger does
        UIController.Instance.PostProcessingVolume.profile.TryGet(out DepthOfField dof);
        UIController.Instance.PostProcessingVolume.profile.TryGet(out FilmGrain filmGrain);
        UIController.Instance.PostProcessingVolume.profile.TryGet(out MotionBlur motionBlur);

        // Lock the player in place for the duration
        if (playerController.Instance != null)
            playerController.Instance.SetState(playerController.playerState.Cutscene);

        float elapsed = 0f;
        while (elapsed < quitFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / quitFadeOutDuration);

            // Depth of field
            if (dof != null)
                dof.focalLength.value = Mathf.Lerp(0f, 200f, t);

            // Film grain
            if (filmGrain != null)
                filmGrain.intensity.value = Mathf.Lerp(0f, 0.5f, t);

            // Motion blur
            if (motionBlur != null)
                motionBlur.intensity.value = Mathf.Lerp(0f, 1f, t);

            // Camera shake
            UIController.Instance.ShakeMagnitude = Mathf.Lerp(0f, 0.005f, t);

            // Fade to black
            UIController.Instance.Fade.color = new Color(0f, 0f, 0f, t);

            yield return null;
        }

        // Snap fully black and reset shake
        UIController.Instance.Fade.color     = new Color(0f, 0f, 0f, 1f);
        UIController.Instance.ShakeMagnitude = 0f;

        yield return StartCoroutine(LoadingScreenController.Instance.Play());

        IsQuittingToMenu = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}