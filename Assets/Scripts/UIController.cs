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
    public Image Fade;
    public Volume PostProcessingVolume;
    public GameObject UIEntryBackingPrefab;
    public GameObject UIEntryParent;
    public GameObject UIEntryCollectedPrefab;
    public GameObject UIEntryCollectedParent;
    public int UIEntryCount = 0;
    public int UICollectedCount = 0;
    public float ShakeMagnitude = 0f;

    private MonoBehaviour _currentInteractable;
    private Coroutine _decoderTypewriterCoroutine;

    private List<string> _committedChunks = new List<string>();
    private string _currentChunk = "";
    private float _originalHeight;

    void Awake()
    {
        Instance = this;
        _originalHeight = DecoderText.rectTransform.sizeDelta.y;
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
        DecoderText.maxVisibleCharacters = 0;

        // Reset rect height
        DecoderText.rectTransform.sizeDelta = new Vector2(
            DecoderText.rectTransform.sizeDelta.x,
            _originalHeight
        );

        _decoderTypewriterCoroutine = StartCoroutine(DecoderTypewriterCoroutine(wordData));
    }

    private IEnumerator DecoderTypewriterCoroutine(DecoderWordData wordData)
    {
        foreach (var entry in wordData.words)
        {
            _currentChunk = entry.word;

            DecoderText.text = BuildFullText();
            DecoderText.maxVisibleCharacters = GetCommittedLength();

            float delayPerChar = entry.word.Length > 0 ? entry.typewriterDuration / entry.word.Length : 0f;

            for (int i = 1; i <= _currentChunk.Length; i++)
            {
                int visible = GetCommittedLength() + i;
                DecoderText.maxVisibleCharacters = visible;

                // Pass exact visible count so we only measure what's been typed
                GrowRectToFit(visible);

                yield return new WaitForSeconds(delayPerChar);
            }

            yield return new WaitForSeconds(entry.delayAfterWord);

            _committedChunks.Add(_currentChunk);
            _currentChunk = "";
        }

        _decoderTypewriterCoroutine = null;
    }

    private void GrowRectToFit()
    {
        DecoderText.ForceMeshUpdate();

        if (DecoderText.textInfo.pageCount <= 1)
            return;

        float lineHeight = 0f;
        if (DecoderText.textInfo.lineCount > 0)
        {
            TMP_LineInfo line = DecoderText.textInfo.lineInfo[0];
            lineHeight = line.ascender - line.descender;
        }

        if (lineHeight <= 0f)
            return;

        // Grow by exactly one line — called per character so this naturally catches up
        DecoderText.rectTransform.offsetMax = new Vector2(
            DecoderText.rectTransform.offsetMax.x,
            DecoderText.rectTransform.offsetMax.y + lineHeight
        );
    }

    private void GrowRectToFit(int visibleCharCount)
    {
        // Temporarily set text to only what has been revealed so far
        string fullText = DecoderText.text;
        int fullMax = DecoderText.maxVisibleCharacters;

        string visibleText = fullText.Length >= visibleCharCount
            ? fullText.Substring(0, visibleCharCount)
            : fullText;

        DecoderText.text = visibleText;
        DecoderText.maxVisibleCharacters = int.MaxValue;
        DecoderText.ForceMeshUpdate();

        bool overflows = DecoderText.textInfo.pageCount > 1;

        float lineHeight = 0f;
        if (DecoderText.textInfo.lineCount > 0)
        {
            TMP_LineInfo line = DecoderText.textInfo.lineInfo[0];
            lineHeight = line.ascender - line.descender;
        }

        // Restore full text and visible count for correct word wrapping
        DecoderText.text = fullText;
        DecoderText.maxVisibleCharacters = fullMax;

        if (overflows && lineHeight > 0f)
        {
            DecoderText.rectTransform.offsetMax = new Vector2(
                DecoderText.rectTransform.offsetMax.x,
                DecoderText.rectTransform.offsetMax.y + lineHeight
            );
        }
    }

    private string BuildFullText()
    {
        if (_committedChunks.Count == 0)
            return _currentChunk;

        return string.Join("\n", _committedChunks)
               + (_currentChunk.Length > 0 ? "\n" + _currentChunk : "");
    }

    private int GetCommittedLength()
    {
        int total = 0;
        foreach (var chunk in _committedChunks)
            total += chunk.Length + 1; // +1 for \n separator
        return total;
    }

    void Update()
    {
        Camera.main.transform.localPosition = new Vector3(0, 0.24f, 0);
        Vector3 shakeOffset = Random.insideUnitCircle * ShakeMagnitude;
        Camera.main.transform.localPosition += new Vector3(shakeOffset.x, shakeOffset.y, 0);
    }
}
