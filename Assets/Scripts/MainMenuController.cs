using System.Collections;
using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    public static MainMenuController Instance { get; private set; }

    public static bool IsInMainMenu { get; private set; } = true;

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

    [Header("Fade")]
    [SerializeField] private float fadeInDuration = 1f;

    private Coroutine _activeTransition;

    // Convenience — matches how Decoder accesses the player
    private Transform Player => GameController.Instance.player.transform;

    void Awake()
    {
        Instance = this;
        IsInMainMenu = true;
    }

    void Start()
    {
        StartCoroutine(InitMainMenu());
    }

    private IEnumerator InitMainMenu()
    {
        yield return null;

        if (mainMenuRoot != null)
            mainMenuRoot.SetActive(true);

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
            float t = Mathf.Clamp01(elapsed / fadeInDuration);
            UIController.Instance.Fade.color = new Color(0f, 0f, 0f, 1f - t);
            yield return null;
        }
        UIController.Instance.Fade.color = new Color(0f, 0f, 0f, 0f);
    }

    // ── Button Callbacks ──────────────────────────────────────────────────────

    public void OnStartPressed()
    {
        if (_activeTransition != null) return;
        IsInMainMenu = false; // block buttons immediately on press
        _activeTransition = StartCoroutine(StartTransition());
    }

    public void OnSettingsPressed()
    {
        if (_activeTransition != null) return;
        IsInMainMenu = false; // block buttons immediately on press
        _activeTransition = StartCoroutine(SettingsTransition());
    }

    // ── Transitions ───────────────────────────────────────────────────────────

    private IEnumerator StartTransition()
    {
        // Fade out the main menu UI before the player starts moving
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

            Player.position = Vector3.Lerp(
                Player.position, startPlayerTarget.position, startLerpSpeed * Time.deltaTime);

            yield return null;
        }

        Player.position = startPlayerTarget.position;

        playerController.Instance.radarHidden = false;

        EnterGameplay();
        _activeTransition = null;
    }

    private IEnumerator SettingsTransition()
    {
        bool rotationDone = false;

        while (!rotationDone || Vector3.Distance(Player.position, settingsCameraTarget.position) > 0.02f)
        {
            rotationDone = playerController.Instance.LerpCameraTowardsTarget(settingsLookTarget, settingsLerpSpeed);

            Player.position = Vector3.Lerp(
                Player.position, settingsCameraTarget.position, settingsLerpSpeed * Time.deltaTime);

            yield return null;
        }

        Player.position = settingsCameraTarget.position;
        _activeTransition = null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void EnterGameplay()
    {
        IsInMainMenu = false;

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
}