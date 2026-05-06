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
    private List<string> _committedChunks = new List<string>();
    private string _currentChunk = "";

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

        _committedChunks.Clear();
        _currentChunk = "";
        DecoderText.text = "";

        if (wordData.startAudio != null && DecoderAudioSource != null)
        {
            DecoderAudioSource.clip = wordData.startAudio;
            DecoderAudioSource.Play();
        }

        _decoderTypewriterCoroutine = StartCoroutine(DecoderTypewriterCoroutine(wordData));
    }

    private IEnumerator DecoderTypewriterCoroutine(DecoderWordData wordData)
    {
        foreach (var entry in wordData.words)
        {
            // Handle deletion before typing
            if (entry.deleteCharsBefore != 0)
            {
                string committed = string.Join("", _committedChunks);
                int deleteCount = entry.deleteCharsBefore == -1 ? committed.Length : Mathf.Min(entry.deleteCharsBefore, committed.Length);

                if (entry.deletionSpeed == -1f)
                {
                    committed = committed.Substring(0, committed.Length - deleteCount);
                    DecoderText.text = committed;
                    DecoderScrollRect.verticalNormalizedPosition = 0f;
                }
                else
                {
                    int remaining = deleteCount;
                    while (remaining > 0)
                    {
                        int step = Mathf.Min(entry.deletionCharsPerStep, remaining);
                        committed = committed.Substring(0, committed.Length - step);
                        remaining -= step;
                        DecoderText.text = committed;
                        DecoderScrollRect.verticalNormalizedPosition = 0f;
                        yield return new WaitForSeconds(entry.deletionSpeed);
                    }
                }

                _committedChunks.Clear();
                if (committed.Length > 0)
                    _committedChunks.Add(committed);
            }

            // Type out the new word
            _currentChunk = entry.word;
            float delayPerChar = entry.word.Length > 0 ? entry.typewriterDuration / entry.word.Length : 0f;

            for (int i = 1; i <= _currentChunk.Length; i++)
            {
                DecoderText.text = string.Join("", _committedChunks) + _currentChunk.Substring(0, i);
                DecoderScrollRect.verticalNormalizedPosition = 0f;
                yield return new WaitForSeconds(delayPerChar);
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
