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
    [Tooltip("Screen-space button GameObjects active only while the main menu is shown.")]
    [SerializeField] private GameObject[] mainMenuButtons;
    [Tooltip("Screen-space button GameObjects active only while the settings menu is shown.")]
    [SerializeField] private GameObject[] settingsButtons;

    [Header("Game UI")]
    [SerializeField] private GameObject radarUIRoot;

    // ── Start Button ──────────────────────────────────────────────────────────
    [Header("Start – Player Destination")]
    [SerializeField] private Transform startPlayerTarget;
    [SerializeField] private Transform startLookTarget;
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
    [Tooltip("The visual slider on the world space canvas — kept in sync with the volume slider.")]
    [SerializeField] private UnityEngine.UI.Slider worldSpaceVolumeSlider;

    // ── Back Button ───────────────────────────────────────────────────────────
    [Header("Back – Camera Return")]
    [SerializeField] private float backLerpSpeed = 2f;
    [SerializeField] private float settingsFadeInDuration = 0.5f;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration = 1f;

    [Header("Portals")]
    [Tooltip("All portal GameObjects to disable during the main menu and re-enable on Start.")]
    [SerializeField] private GameObject[] portals;

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
        IsInMainMenu     = true;
        IsInMenuSequence = true;

        foreach (var portal in portals)
            if (portal != null) portal.SetActive(false);

        if (settingsMenuRoot != null)
            settingsMenuRoot.SetActive(false);
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

        foreach (var btn in mainMenuButtons)
            if (btn != null) btn.SetActive(true);

        foreach (var btn in settingsButtons)
            if (btn != null) btn.SetActive(false);

        if (fullscreenCheckImage != null)
            fullscreenCheckImage.gameObject.SetActive(Screen.fullScreen);

        if (UIController.Instance != null)
        {
            if (UIController.Instance.UIEntryParent != null)
                UIController.Instance.UIEntryParent.SetActive(false);
            if (UIController.Instance.UIEntryCollectedParent != null)
                UIController.Instance.UIEntryCollectedParent.SetActive(false);
            if (UIController.Instance.CursorImage != null)
                UIController.Instance.CursorImage.gameObject.SetActive(true);
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
        IsInMainMenu = false;

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
        if (!_inSettings || _activeTransition != null) return;
        _activeTransition = StartCoroutine(SettingsBackTransition());
    }

    public void OnFullscreenToggle()
    {
        if (!_inSettings) return;
        Screen.fullScreen = !Screen.fullScreen;
        if (fullscreenCheckImage != null)
            fullscreenCheckImage.gameObject.SetActive(Screen.fullScreen);
    }

    public void OnVolumeChanged(float value)
    {
        if (!_inSettings) return;
        AudioListener.volume = value;
        if (worldSpaceVolumeSlider != null)
            worldSpaceVolumeSlider.value = value;
    }

    // ── Transitions ───────────────────────────────────────────────────────────

    private IEnumerator StartTransition()
    {
        if (radarUIRoot != null)
            radarUIRoot.SetActive(true);

        // 1. Fade out menu UI
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

        // 2. Fade screen to black
        float fadeElapsed = 0f;
        while (fadeElapsed < menuFadeOutDuration)
        {
            fadeElapsed += Time.deltaTime;
            UIController.Instance.Fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(fadeElapsed / menuFadeOutDuration));
            yield return null;
        }
        UIController.Instance.Fade.color = new Color(0f, 0f, 0f, 1f);

        // 3. Teleport player and snap camera rotation toward look target
        Player.position = startPlayerTarget.position;

        if (startLookTarget != null)
        {
            Vector3 dir = (startLookTarget.position - startPlayerTarget.position).normalized;

            float yaw = Quaternion.LookRotation(dir, Vector3.up).eulerAngles.y;
            Player.rotation = Quaternion.Euler(0f, yaw, 0f);

            float pitch = -Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
            playerController.Instance.SetVerticalRotation(pitch);
        }
        else
        {
            Player.rotation = startPlayerTarget.rotation;
        }

        // Re-enable portals after player has been teleported
        foreach (var portal in portals)
            if (portal != null) portal.SetActive(true);

        // 4. Fade screen back in
        fadeElapsed = 0f;
        while (fadeElapsed < fadeInDuration)
        {
            fadeElapsed += Time.deltaTime;
            UIController.Instance.Fade.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01(fadeElapsed / fadeInDuration));
            yield return null;
        }
        UIController.Instance.Fade.color = new Color(0f, 0f, 0f, 0f);

        EnterGameplay();
        _activeTransition = null;
    }

    private IEnumerator SettingsTransition()
    {
        // 1. Fade out main menu
        CanvasGroup mainCG = mainMenuRoot != null ? mainMenuRoot.GetComponent<CanvasGroup>() : null;
        if (mainCG != null)
        {
            float elapsed = 0f;
            while (elapsed < settingsFadeOutDuration)
            {
                elapsed += Time.deltaTime;
                mainCG.alpha = Mathf.Lerp(1f, 0f, elapsed / settingsFadeOutDuration);
                yield return null;
            }
            mainCG.alpha = 0f;
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

        // 4. Fade settings panel in
        if (settingsMenuRoot != null)
        {
            CanvasGroup settingsCG = settingsMenuRoot.GetComponent<CanvasGroup>();
            if (settingsCG != null) settingsCG.alpha = 0f;
            settingsMenuRoot.SetActive(true);

            if (settingsCG != null)
            {
                float elapsed = 0f;
                while (elapsed < settingsFadeOutDuration)
                {
                    elapsed += Time.deltaTime;
                    settingsCG.alpha = Mathf.Lerp(0f, 1f, elapsed / settingsFadeOutDuration);
                    yield return null;
                }
                settingsCG.alpha = 1f;
            }
        }

        // After settings fade in completes:
        foreach (var btn in mainMenuButtons)
            if (btn != null) btn.SetActive(false);

        foreach (var btn in settingsButtons)
            if (btn != null) btn.SetActive(true);

        _inSettings       = true;
        _activeTransition = null;
    }

    private IEnumerator SettingsBackTransition()
    {
        // Clear settings state immediately so Escape can't re-trigger
        _inSettings = false;

        foreach (var btn in settingsButtons)
            if (btn != null) btn.SetActive(false);

        foreach (var btn in mainMenuButtons)
            if (btn != null) btn.SetActive(true);

        // 1. Hide settings panel
        if (settingsMenuRoot != null)
            settingsMenuRoot.SetActive(false);

        if (radarUIRoot != null)
            radarUIRoot.SetActive(false);

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
        IsInMenuSequence = false;

        if (mainMenuRoot != null)
            mainMenuRoot.SetActive(false);

        foreach (var btn in mainMenuButtons)
            if (btn != null) btn.SetActive(false);

        foreach (var btn in settingsButtons)
            if (btn != null) btn.SetActive(false);

        if (UIController.Instance != null)
        {
            if (UIController.Instance.UIEntryParent != null)
                UIController.Instance.UIEntryParent.SetActive(false);
            if (UIController.Instance.UIEntryCollectedParent != null)
                UIController.Instance.UIEntryCollectedParent.SetActive(false);
            if (UIController.Instance.CursorImage != null)
                UIController.Instance.CursorImage.gameObject.SetActive(true);
        }

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