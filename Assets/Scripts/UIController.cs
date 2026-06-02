using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    public static UIController Instance { get; private set; }

    public TextMeshProUGUI InteractText;
    public TextMeshProUGUI DecoderText;
    public ScrollRect DecoderScrollRect;
    public Image Fade;
    public Image CursorImage;
    public Volume PostProcessingVolume;
    public GameObject UIEntryBackingPrefab;
    public GameObject UIEntryParent;
    public GameObject UIEntryCollectedPrefab;
    public GameObject UIEntryCollectedParent;
    public int UIEntryCount = 0;
    public int UICollectedCount = 0;
    public float ShakeMagnitude = 0f;
    public AudioSource DecoderAudioSource;

    [Header("Entry Fill-Out")]
    [Tooltip("How long (seconds) the latest entry and its backing take to fade their fill from 1 to 0 on cassette insert.")]
    public float EntryFillOutDuration = 0.6f;
    [Header("Entry UI Fade-In")]
    [Tooltip("Duration in seconds for the entry UI panels to fade in after portal travel.")]
    [SerializeField] private float entryUIFadeInDuration = 0.5f;

    private MonoBehaviour _currentInteractable;
    private Coroutine _decoderTypewriterCoroutine;
    private Coroutine _cursorCoroutine;
    private Coroutine _entryFadeCoroutine;
    private bool _isTyping = false;
    private List<string> _committedChunks = new List<string>();
    private string _currentChunk = "";

    // Most recently instantiated UI entry objects
    private GameObject _lastEntryBacking;
    private GameObject _lastEntryCollected;

    // All live entry objects — TriggerLatestEntryFillOut pops from the end.
    private List<GameObject> _entryBackings = new List<GameObject>();
    private List<GameObject> _entryCollected = new List<GameObject>();

    private int _fillOutPending = 0;

    public float CursorBlinkRate = 0.5f;

    private Decoder _activeDecoder;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Start cursor blink immediately so it runs even before any typewriter plays
        _cursorCoroutine = StartCoroutine(CursorBlinkCoroutine());
    }

    public void RequestInteractText(MonoBehaviour requester)
    {
        _currentInteractable = requester;
        InteractText.text = "[E]";
    }

    public void ReleaseInteractText(MonoBehaviour requester)
    {
        if (_currentInteractable == requester)
        {
            _currentInteractable = null;
            InteractText.text = "";
        }
    }

    public void AddUIEntry()
    {
        _lastEntryBacking = Instantiate(UIEntryBackingPrefab, UIEntryParent.transform);
        _entryBackings.Add(_lastEntryBacking);
        UIEntryCount++;
    }

    public void AddCollected()
    {
        if (UICollectedCount < UIEntryCount)
        {
            _lastEntryCollected = Instantiate(UIEntryCollectedPrefab, UIEntryCollectedParent.transform);
            _entryCollected.Add(_lastEntryCollected);
            UICollectedCount++;
        }
    }

    /// <summary>
    /// Triggered when the cassette is fully inserted. Lerps the fill of the most recently
    /// instantiated entry and its backing from 1 → 0 using a vertical sprite fill.
    /// </summary>
    public void TriggerLatestEntryFillOut(float destroyDelay = 0f)
    {
        _fillOutPending = 0;

        // Pop the most recently added collected entry
        GameObject collectedGO = PopLast(_entryCollected);
        if (collectedGO != null)
        {
            Image img = collectedGO.GetComponent<Image>();
            if (img != null)
            {
                img.type       = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Vertical;
                img.fillOrigin = (int)Image.OriginVertical.Bottom;
                img.fillAmount = 1f;
                _fillOutPending++;
                StartCoroutine(LerpFillToZero(img, EntryFillOutDuration, destroyDelay));
            }
        }

        // Pop the most recently added backing entry
        GameObject backingGO = PopLast(_entryBackings);
        if (backingGO != null)
        {
            Image img = backingGO.GetComponent<Image>();
            if (img != null)
            {
                img.type       = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Vertical;
                img.fillOrigin = (int)Image.OriginVertical.Bottom;
                img.fillAmount = 1f;
                _fillOutPending++;
                StartCoroutine(LerpFillToZero(img, EntryFillOutDuration, destroyDelay));
            }
        }
    }

    private GameObject PopLast(List<GameObject> list)
    {
        if (list == null || list.Count == 0) return null;
        int last = list.Count - 1;
        GameObject go = list[last];
        list.RemoveAt(last);
        return go;
    }

    private IEnumerator LerpFillToZero(Image image, float duration, float destroyDelay = 0f)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            image.fillAmount = Mathf.Lerp(1f, 0f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        image.fillAmount = 0f;

        if (destroyDelay > 0f)
            yield return new WaitForSeconds(destroyDelay);

        Destroy(image.gameObject);

        _fillOutPending--;
        if (_fillOutPending <= 0)
        {
            if (UIEntryParent != null)         UIEntryParent.SetActive(false);
            if (UIEntryCollectedParent != null) UIEntryCollectedParent.SetActive(false);
        }
    }

    // Add this method to set the active decoder
    public void SetActiveDecoder(Decoder decoder)
    {
        _activeDecoder = decoder;
    }

    public void RestoreEntryParents()
    {
        if (UIEntryParent != null)         UIEntryParent.SetActive(true);
        if (UIEntryCollectedParent != null) UIEntryCollectedParent.SetActive(true);

        if (_activeDecoder != null)
        {
            _activeDecoder.RestoreLight();
            _activeDecoder = null;
        }
    }

    /// <summary>
    /// Re-enables the entry UI parents without triggering light restore or
    /// dialogue exit. Used between sequential cassette logs.
    /// </summary>
    public void RestoreEntryParentsOnly()
    {
        if (UIEntryParent != null)         UIEntryParent.SetActive(true);
        if (UIEntryCollectedParent != null) UIEntryCollectedParent.SetActive(true);
    }

    // Reset audio tape UI when new loop starts
    public void ResetUI()
    {
        if (UIEntryParent != null)
        {
            foreach (Transform child in UIEntryParent.transform)
                Destroy(child.gameObject);
        }

        if (UIEntryCollectedParent != null)
        {
            foreach (Transform child in UIEntryCollectedParent.transform)
                Destroy(child.gameObject);
        }

        UIEntryCount = 0;
        UICollectedCount = 0;
        _lastEntryBacking = null;
        _lastEntryCollected = null;
        _entryBackings.Clear();
        _entryCollected.Clear();
    }

    private System.Action _typewriterOnComplete;

    public void PlayDecoderTypewriter(DecoderWordData wordData, System.Action onComplete = null)
    {
        if (wordData == null) return;

        if (_decoderTypewriterCoroutine != null)
            StopCoroutine(_decoderTypewriterCoroutine);
        if (_cursorCoroutine != null)
            StopCoroutine(_cursorCoroutine);

        _committedChunks.Clear();
        _currentChunk = "";
        DecoderText.text = "";
        _isTyping = true;
        _typewriterOnComplete = onComplete;

        if (wordData.startAudio != null && DecoderAudioSource != null)
        {
            DecoderAudioSource.clip = wordData.startAudio;
            DecoderAudioSource.Play();
        }

        _decoderTypewriterCoroutine = StartCoroutine(DecoderTypewriterCoroutine(wordData));
        _cursorCoroutine = StartCoroutine(CursorBlinkCoroutine());
    }

    private IEnumerator CursorBlinkCoroutine()
    {
        bool cursorVisible = false;
        while (true)
        {
            yield return new WaitForSeconds(CursorBlinkRate);
            if (!_isTyping)
            {
                cursorVisible = !cursorVisible;
                string baseText = string.Join("", _committedChunks);
                DecoderText.text = baseText + (cursorVisible ? "|" : "");
            }
        }
    }

    private IEnumerator DecoderTypewriterCoroutine(DecoderWordData wordData)
    {
        foreach (var entry in wordData.words)
        {
            if (entry.deleteCharsBefore != 0)
            {
                string committed = string.Join("", _committedChunks);
                int deleteCount = entry.deleteCharsBefore == -1 ? committed.Length : Mathf.Min(entry.deleteCharsBefore, committed.Length);

                if (entry.deletionSpeed == -1f)
                {
                    committed = committed.Substring(0, committed.Length - deleteCount);
                    _committedChunks.Clear();
                    if (committed.Length > 0) _committedChunks.Add(committed);
                    _currentChunk = "";
                    DecoderText.text = committed;
                    DecoderScrollRect.verticalNormalizedPosition = 0f;
                }
                else
                {
                    int remaining = deleteCount;
                    float deleteTimer = 0f;
                    while (remaining > 0)
                    {
                        deleteTimer += Time.deltaTime;
                        while (deleteTimer >= entry.deletionSpeed && remaining > 0)
                        {
                            int step = Mathf.Min(entry.deletionCharsPerStep, remaining);
                            committed = committed.Substring(0, committed.Length - step);
                            remaining -= step;
                            deleteTimer -= entry.deletionSpeed;
                        }
                        _committedChunks.Clear();
                        if (committed.Length > 0) _committedChunks.Add(committed);
                        _currentChunk = "";
                        DecoderText.text = committed;
                        DecoderScrollRect.verticalNormalizedPosition = 0f;
                        yield return null;
                    }
                }
            }

            _currentChunk = entry.word;
            int revealedChars = 0;
            float elapsed = 0f;
            float delayPerChar = entry.word.Length > 0 ? entry.typewriterDuration / entry.word.Length : 0f;

            while (revealedChars < _currentChunk.Length)
            {
                elapsed += Time.deltaTime;
                int targetChars = delayPerChar > 0 ? Mathf.Min(Mathf.FloorToInt(elapsed / delayPerChar), _currentChunk.Length) : _currentChunk.Length;

                if (targetChars > revealedChars)
                {
                    revealedChars = targetChars;
                    DecoderText.text = string.Join("", _committedChunks) + _currentChunk.Substring(0, revealedChars);
                    DecoderScrollRect.verticalNormalizedPosition = 0f;
                }

                yield return null;
            }

            yield return new WaitForSeconds(entry.delayAfterWord);

            _committedChunks.Add(_currentChunk);
            _currentChunk = "";
        }

        _isTyping = false;
        _decoderTypewriterCoroutine = null;

        // Notify the Decoder that this log is done. The Decoder is responsible
        // for calling ExitDialogue() and RestoreEntryParents() at the right time.
        var cb = _typewriterOnComplete;
        _typewriterOnComplete = null;
        cb?.Invoke();
    }

    void Update()
    {
        if (PauseManager.IsPaused) return;

        Camera.main.transform.localPosition = new Vector3(0, 0.85f, 0);
        Vector3 shakeOffset = Random.insideUnitCircle * ShakeMagnitude;
        Camera.main.transform.localPosition += new Vector3(shakeOffset.x, shakeOffset.y, 0);

        bool inDialogue = playerController.Instance != null &&
                          playerController.Instance.CurrentState == playerController.playerState.InDialogue;

        bool inCutscene = playerController.Instance != null &&
                          playerController.Instance.CurrentState == playerController.playerState.Cutscene;

        bool hideUI = inDialogue || inCutscene;

        if (hideUI)
        {
            _currentInteractable = null;
            InteractText.text = "";
        }

        if (CursorImage != null)
            CursorImage.enabled = !hideUI;
    }

    /// <summary>
    /// Fades UIEntryParent and UIEntryCollectedParent in using a CanvasGroup.
    /// Requires a CanvasGroup on each parent object.
    /// </summary>
    public void FadeInEntryUI()
    {
        if (_entryFadeCoroutine != null) StopCoroutine(_entryFadeCoroutine);
        _entryFadeCoroutine = StartCoroutine(FadeInEntryUICoroutine());
    }

    private IEnumerator FadeInEntryUICoroutine()
    {
        CanvasGroup cgEntry     = UIEntryParent         != null ? UIEntryParent.GetComponent<CanvasGroup>()         : null;
        CanvasGroup cgCollected = UIEntryCollectedParent != null ? UIEntryCollectedParent.GetComponent<CanvasGroup>() : null;

        if (UIEntryParent != null)         UIEntryParent.SetActive(true);
        if (UIEntryCollectedParent != null) UIEntryCollectedParent.SetActive(true);

        if (cgEntry     != null) cgEntry.alpha     = 0f;
        if (cgCollected != null) cgCollected.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < entryUIFadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / entryUIFadeInDuration);
            if (cgEntry     != null) cgEntry.alpha     = t;
            if (cgCollected != null) cgCollected.alpha = t;
            yield return null;
        }

        if (cgEntry     != null) cgEntry.alpha     = 1f;
        if (cgCollected != null) cgCollected.alpha = 1f;

        _entryFadeCoroutine = null;
    }
}
