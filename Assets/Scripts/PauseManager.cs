using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }
    public static bool IsPaused { get; private set; }

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
                UIController.Instance.CursorImage.enabled = false;
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
                UIController.Instance.CursorImage.enabled = true;
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
}