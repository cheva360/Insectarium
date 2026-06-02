using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    public static MainMenuController Instance { get; private set; }
    public static bool IsInMainMenu { get; private set; } = true;

    /// <summary>
    /// True during the entire main menu phase including the start lerp.
    /// PauseManager checks this so it stays locked until gameplay actually begins.
    /// </summary>
    public static bool IsInMenuSequence { get; private set; } = true;

    [Header("Main Menu UI")]
    [SerializeField] private GameObject mainMenuRoot;

    [Header("Game UI")]
    [SerializeField] private GameObject radarUIRoot;

    // ── Start Button ──────────────────────────────────────────────────────────
    [Header("Start – Player Destination")]
    [SerializeField] private Transform startPlayerTarget;
    [SerializeField] private float startLerpSpeed = 2f;
    [SerializeField] private float menuFadeOutDuration = 0.8f;

    // ── Settings Button ───────────────────────────────────────────────────────
    [Header("Settings – Camera Destination")]
    [SerializeField] private Transform settingsCameraTarget;
    [SerializeField] private Transform settingsLookTarget;
    [SerializeField] private float settingsLerpSpeed = 2f;
    [SerializeField] private float settingsFadeOutDuration = 0.5f;

    // ── Settings Menu UI ──────────────────────────────────────────────────────
    [Header("Settings Menu")]
    [SerializeField] private GameObject settingsMenuRoot;
    [Tooltip("Image that becomes active when Screen.fullScreen is true.")]
    [SerializeField] private Image fullscreenCheckImage;

    // ── Back Button ───────────────────────────────────────────────────────────
    [Header("Back – Camera Return")]
    [SerializeField] private float backLerpSpeed = 2f;
    [SerializeField] private float settingsFadeInDuration = 0.5f;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration = 1f;

    private Coroutine _activeTransition;
    private bool      _inSettings;

    // Snapshotted at Start so Back can return exactly here
    private Vector3    _menuPlayerPos;
    private Quaternion _menuPlayerRot;
    private Transform  _menuLookAnchor; // runtime dummy — placed 10 units ahead in original forward

    private Transform Player => GameController.Instance.player.transform;

    void Awake()
    {
        Instance = this;
        IsInMainMenu    = true;
        IsInMenuSequence = true;
    }

    void Start()
    {
        StartCoroutine(InitMainMenu());
    }

    private IEnumerator InitMainMenu()
    {
        yield return null;

        // Ensure AudioListener is never silenced by an uninitialised slider value
        AudioListener.volume = 1f;

        // ── Snapshot the player's original main-menu transform ────────────────
        _menuPlayerPos = Player.position;
        _menuPlayerRot = Player.rotation;

        // Build a dummy look-anchor 10 units ahead in the body's forward direction.
        // LerpCameraTowardsTarget derives pitch from the vertical offset to this point,
        // so placing it at the same Y keeps the camera roughly level on return.
        _menuLookAnchor = new GameObject("_MenuLookAnchor").transform;
        _menuLookAnchor.position = _menuPlayerPos + _menuPlayerRot * Vector3.forward * 10f;

        // ── UI Init ───────────────────────────────────────────────────────────
        if (mainMenuRoot != null)
            mainMenuRoot.SetActive(true);

        if (settingsMenuRoot != null)
            settingsMenuRoot.SetActive(false);

        if (fullscreenCheckImage != null)
            fullscreenCheckImage.gameObject.SetActive(Screen.fullScreen);

        if (UIController.Instance != null)
        {
            if (UIController.Instance.UIEntryParent != null)
                UIController.Instance.UIEntryParent.SetActive(false);
            if (UIController.Instance.UIEntryCollectedParent != null)
                UIController.Instance.UIEntryCollectedParent.SetActive(false);
            if (UIController.Instance.CursorImage != null)
                UIController.Instance.CursorImage.gameObject.SetActive(false);
        }

        if (radarUIRoot != null)
            radarUIRoot.SetActive(false);

        if (playerController.Instance != null)
            playerController.Instance.SnapRadarToHidden();

        if (playerController.Instance != null)
            playerController.Instance.SetState(playerController.playerState.Cutscene);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        // ── Fade in from black ────────────────────────────────────────────────
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            UIController.Instance.Fade.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01(elapsed / fadeInDuration));
            yield return null;
        }
        UIController.Instance.Fade.color = new Color(0f, 0f, 0f, 0f);
    }

    // ── Button Callbacks ──────────────────────────────────────────────────────

    public void OnStartPressed()
    {
        if (_activeTransition != null) return;
        IsInMainMenu = false; // blocks button clicks and settings escape — lerp still in progress
        _activeTransition = StartCoroutine(StartTransition());
    }

    public void OnSettingsPressed()
    {
        if (_activeTransition != null) return;
        IsInMainMenu = false;
        _activeTransition = StartCoroutine(SettingsTransition());
    }

    public void OnSettingsBackPressed()
    {
        if (_activeTransition != null) return;
        _activeTransition = StartCoroutine(SettingsBackTransition());
    }

    /// <summary>Toggles fullscreen and syncs the check image.</summary>
    public void OnFullscreenToggle()
    {
        Screen.fullScreen = !Screen.fullScreen;
        if (fullscreenCheckImage != null)
            fullscreenCheckImage.gameObject.SetActive(Screen.fullScreen);
    }

    /// <summary>
    /// Drives AudioListener.volume — wire to a Slider's OnValueChanged.
    /// Controls every AudioSource in the scene with a single knob.
    /// </summary>
    public void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
    }

    // ── Transitions ───────────────────────────────────────────────────────────

    private IEnumerator StartTransition()
    {
        CanvasGroup cg = mainMenuRoot != null ? mainMenuRoot.GetComponent<CanvasGroup>() : null;
        if (cg != null)
        {
            float elapsed = 0f;
            while (elapsed < menuFadeOutDuration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(1f, 0f, elapsed / menuFadeOutDuration);
                yield return null;
            }
            cg.alpha = 0f;
        }

        bool rotationDone = false;
        while (!rotationDone || Vector3.Distance(Player.position, startPlayerTarget.position) > 0.02f)
        {
            rotationDone = playerController.Instance.LerpCameraTowardsTarget(startPlayerTarget, startLerpSpeed);
            Player.position = Vector3.Lerp(Player.position, startPlayerTarget.position, startLerpSpeed * Time.deltaTime);
            yield return null;
        }

        Player.position = startPlayerTarget.position;
        playerController.Instance.radarHidden = false;
        EnterGameplay();
        _activeTransition = null;
    }

    private IEnumerator SettingsTransition()
    {
        // 1. Fade out main menu
        CanvasGroup cg = mainMenuRoot != null ? mainMenuRoot.GetComponent<CanvasGroup>() : null;
        if (cg != null)
        {
            float elapsed = 0f;
            while (elapsed < settingsFadeOutDuration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(1f, 0f, elapsed / settingsFadeOutDuration);
                yield return null;
            }
            cg.alpha = 0f;
        }

        // 2. Disable decoder text
        if (UIController.Instance?.DecoderText != null)
            UIController.Instance.DecoderText.gameObject.SetActive(false);

        // 3. Lerp camera to settings position
        bool rotationDone = false;
        while (!rotationDone || Vector3.Distance(Player.position, settingsCameraTarget.position) > 0.02f)
        {
            rotationDone = playerController.Instance.LerpCameraTowardsTarget(settingsLookTarget, settingsLerpSpeed);
            Player.position = Vector3.Lerp(Player.position, settingsCameraTarget.position, settingsLerpSpeed * Time.deltaTime);
            yield return null;
        }
        Player.position = settingsCameraTarget.position;

        // 4. Show settings panel and mark state
        if (settingsMenuRoot != null)
            settingsMenuRoot.SetActive(true);

        _inSettings       = true;
        _activeTransition = null;
    }

    private IEnumerator SettingsBackTransition()
    {
        // Clear settings state immediately so Escape can't re-trigger
        _inSettings = false;

        // 1. Hide settings panel
        if (settingsMenuRoot != null)
            settingsMenuRoot.SetActive(false);

        // 2. Lerp player position and camera back to snapshotted main-menu state
        bool rotationDone = false;
        while (!rotationDone || Vector3.Distance(Player.position, _menuPlayerPos) > 0.02f)
        {
            rotationDone = playerController.Instance.LerpCameraTowardsTarget(_menuLookAnchor, backLerpSpeed);
            Player.position = Vector3.Lerp(Player.position, _menuPlayerPos, backLerpSpeed * Time.deltaTime);
            yield return null;
        }
        Player.position = _menuPlayerPos;
        Player.rotation = _menuPlayerRot;

        // 3. Re-enable decoder text
        if (UIController.Instance?.DecoderText != null)
            UIController.Instance.DecoderText.gameObject.SetActive(true);

        // 4. Fade main menu back in and restore button functionality
        CanvasGroup cg = mainMenuRoot != null ? mainMenuRoot.GetComponent<CanvasGroup>() : null;
        if (cg != null)
        {
            cg.alpha = 0f;
            float elapsed = 0f;
            while (elapsed < settingsFadeInDuration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(0f, 1f, elapsed / settingsFadeInDuration);
                yield return null;
            }
            cg.alpha = 1f;
        }

        IsInMainMenu      = true;
        _activeTransition = null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void EnterGameplay()
    {
        IsInMainMenu     = false;
        IsInMenuSequence = false; // ← pause now allowed for the first time

        if (mainMenuRoot != null)
            mainMenuRoot.SetActive(false);

        if (UIController.Instance != null)
        {
            if (UIController.Instance.UIEntryParent != null)
                UIController.Instance.UIEntryParent.SetActive(true);
            if (UIController.Instance.UIEntryCollectedParent != null)
                UIController.Instance.UIEntryCollectedParent.SetActive(true);
            if (UIController.Instance.CursorImage != null)
                UIController.Instance.CursorImage.gameObject.SetActive(true);
        }

        if (radarUIRoot != null)
            radarUIRoot.SetActive(true);

        if (playerController.Instance != null)
            playerController.Instance.SetState(playerController.playerState.Normal);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    void Update()
    {
        if (!_inSettings) return;
        if (_activeTransition != null) return;
        if (Input.GetKeyDown(KeyCode.Escape))
            OnSettingsBackPressed();
    }
}