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
    public Volume PostProcessingVolume;
    public GameObject UIEntryBackingPrefab;
    public GameObject UIEntryParent;
    public GameObject UIEntryCollectedPrefab;
    public GameObject UIEntryCollectedParent;
    public int UIEntryCount = 0;
    public int UICollectedCount = 0;
    public float ShakeMagnitude = 0f;
    public AudioSource DecoderAudioSource;

    private MonoBehaviour _currentInteractable;
    private Coroutine _decoderTypewriterCoroutine;
    private Coroutine _cursorCoroutine;
    private bool _isTyping = false;
    private List<string> _committedChunks = new List<string>();
    private string _currentChunk = "";

    public float CursorBlinkRate = 0.5f;

    void Awake()
    {
        Instance = this;
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
        Instantiate(UIEntryBackingPrefab, UIEntryParent.transform);
        UIEntryCount++;
    }

    public void AddCollected()
    {
        if (UICollectedCount < UIEntryCount)
        {
            Instantiate(UIEntryCollectedPrefab, UIEntryCollectedParent.transform);
            UICollectedCount++;
        }
    }

    public void PlayDecoderTypewriter(DecoderWordData wordData)
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

        _decoderTypewriterCoroutine = null;
    }

    void Update()
    {
        Camera.main.transform.localPosition = new Vector3(0, 0.24f, 0);
        Vector3 shakeOffset = Random.insideUnitCircle * ShakeMagnitude;
        Camera.main.transform.localPosition += new Vector3(shakeOffset.x, shakeOffset.y, 0);
    }
}
