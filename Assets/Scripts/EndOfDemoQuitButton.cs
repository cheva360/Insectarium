using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class EndOfDemoQuitButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("The '►' or select indicator to the left of this button's label.")]
    [SerializeField] private GameObject selectPrompt;

    [Tooltip("CanvasGroup on the End of Demo root — fades out the UI before loading.")]
    [SerializeField] private CanvasGroup endOfDemoCanvasGroup;

    [Header("UI Fade Out")]
    [SerializeField] private float uiFadeOutDuration = 0.5f;

    [Header("Arrow Bob")]
    [SerializeField] private float bobDistance = 8f;
    [SerializeField] private float bobSpeed    = 4f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip   hoverSound;
    [SerializeField] private AudioClip   clickSound;

    [Header("Cursor")]
    [SerializeField] private Texture2D hoverCursor;
    [SerializeField] private Vector2   cursorHotspot = Vector2.zero;

    private Coroutine _bobCoroutine;
    private Vector2   _promptOrigin;
    private bool      _clicked;

    void OnEnable()
    {
        _clicked = false;

        if (selectPrompt != null)
        {
            RectTransform rt = selectPrompt.GetComponent<RectTransform>();
            if (rt != null) _promptOrigin = rt.anchoredPosition;
            selectPrompt.SetActive(false);
        }
    }

    void OnDisable()
    {
        StopBob();
        if (selectPrompt != null)
            selectPrompt.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_clicked) return;

        Cursor.SetCursor(hoverCursor, cursorHotspot, CursorMode.Auto);

        if (audioSource != null && hoverSound != null)
            audioSource.PlayOneShot(hoverSound);

        if (selectPrompt != null)
        {
            selectPrompt.SetActive(true);
            StartBob();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        StopBob();

        if (selectPrompt != null)
            selectPrompt.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_clicked) return;
        _clicked = true;

        StopBob();
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

        if (audioSource != null && clickSound != null)
            audioSource.PlayOneShot(clickSound);

        StartCoroutine(QuitSequence());
    }

    private IEnumerator QuitSequence()
    {
        // Fade out the End of Demo UI; the screen stays black behind it
        if (endOfDemoCanvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < uiFadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                endOfDemoCanvasGroup.alpha = Mathf.Clamp01(1f - elapsed / uiFadeOutDuration);
                yield return null;
            }
            endOfDemoCanvasGroup.alpha = 0f;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ── Bob helpers ───────────────────────────────────────────────────────────

    private void StartBob()
    {
        if (selectPrompt == null) return;
        RectTransform rt = selectPrompt.GetComponent<RectTransform>();
        if (rt == null) return;

        rt.anchoredPosition = _promptOrigin;
        if (_bobCoroutine != null) StopCoroutine(_bobCoroutine);
        _bobCoroutine = StartCoroutine(BobCoroutine(rt));
    }

    private void StopBob()
    {
        if (_bobCoroutine != null)
        {
            StopCoroutine(_bobCoroutine);
            _bobCoroutine = null;
        }

        if (selectPrompt != null)
        {
            RectTransform rt = selectPrompt.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = _promptOrigin;
        }
    }

    private IEnumerator BobCoroutine(RectTransform rt)
    {
        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime * bobSpeed;
            rt.anchoredPosition = _promptOrigin + new Vector2(Mathf.Sin(t) * bobDistance, 0f);
            yield return null;
        }
    }
}