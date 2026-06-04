using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class MainMenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Tooltip("The '►' or select indicator to the left of this button's label.")]
    [SerializeField] private GameObject selectPrompt;

    [Tooltip("The action this button performs.")]
    [SerializeField] private ButtonAction action = ButtonAction.None;

    [Header("Arrow Bob")]
    [SerializeField] private float bobDistance = 8f;
    [SerializeField] private float bobSpeed    = 4f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip   hoverSound;
    [SerializeField] private AudioClip   clickSound;

    [Header("Cursor")]
    [SerializeField] private Texture2D hoverCursor;
    [SerializeField] private Vector2 cursorHotspot = Vector2.zero;

    private Coroutine _bobCoroutine;
    private Vector2   _promptOrigin;
    private bool      _promptOriginCached;

    public enum ButtonAction { None, Start, Settings }

    void OnEnable()
    {
        _promptOriginCached = false;

        if (selectPrompt != null)
        {
            // Cache the resting position while the prompt is inactive (untouched layout position)
            RectTransform rt = selectPrompt.GetComponent<RectTransform>();
            if (rt != null)
            {
                _promptOrigin       = rt.anchoredPosition;
                _promptOriginCached = true;
            }
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
        if (!MainMenuController.IsInMainMenu) return;

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
        if (!MainMenuController.IsInMainMenu) return;
        if (MainMenuController.Instance == null) return;

        if (audioSource != null && clickSound != null)
            audioSource.PlayOneShot(clickSound);

        switch (action)
        {
            case ButtonAction.Start:
                MainMenuController.Instance.OnStartPressed();
                break;
            case ButtonAction.Settings:
                MainMenuController.Instance.OnSettingsPressed();
                break;
        }
    }

    private void StartBob()
    {
        if (selectPrompt == null) return;
        RectTransform rt = selectPrompt.GetComponent<RectTransform>();
        if (rt == null) return;

        // Stop any running bob and snap back before (re)capturing origin
        if (_bobCoroutine != null)
        {
            StopCoroutine(_bobCoroutine);
            _bobCoroutine = null;
            rt.anchoredPosition = _promptOrigin;
        }

        // Only cache origin if OnEnable didn't already do it
        if (!_promptOriginCached)
        {
            _promptOrigin       = rt.anchoredPosition;
            _promptOriginCached = true;
        }

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

    void Update()
    {
        // If state changed while hovering, force-hide the prompt
        if (!MainMenuController.IsInMainMenu && selectPrompt != null && selectPrompt.activeSelf)
        {
            StopBob();
            selectPrompt.SetActive(false);
        }
    }
}