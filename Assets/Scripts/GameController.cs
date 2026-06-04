using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    public GameObject player;
    public Transform MinimapTransform;
    public Transform SpawnLocation;
    //passing out sound
    public AudioClip passingOutSound;
    public AudioSource GameControllerAudioSource;

    [Header("Music")]
    [SerializeField] private AudioClip musicTrack;
    [SerializeField] private AudioSource musicAudioSource;
    [SerializeField] private float musicFadeDuration = 1f;

    private Coroutine _musicFadeCoroutine;

    public enum LoopCount
    {
        Zero = 0,
        One = 1,
        Two = 2,
        Three = 3
    }

    [SerializeField] private GameObject loopOneObject;
    [SerializeField] private GameObject loopTwoObject;
    [SerializeField] private GameObject loopThreeObject;

    [SerializeField] private LoopCount _currentLoop = LoopCount.Zero;
    public LoopCount CurrentLoop
    {
        get => _currentLoop;
        set
        {
            _currentLoop = value;
            ApplyLoopState(_currentLoop);
        }
    }

    private void ApplyLoopState(LoopCount loop)
    {
        if (loopOneObject != null) loopOneObject.SetActive(loop == LoopCount.One);
        if (loopTwoObject != null) loopTwoObject.SetActive(loop == LoopCount.Two);
        if (loopThreeObject != null) loopThreeObject.SetActive(loop == LoopCount.Three);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        //DontDestroyOnLoad(gameObject);
    }

    // Start is called before the first frame update
    void Start()
    {
        ApplyLoopState(_currentLoop);

        if (musicTrack != null && musicAudioSource != null)
        {
            musicAudioSource.clip = musicTrack;
            musicAudioSource.loop = true;
            musicAudioSource.Play();
        }
    }

    public void MusicFadeOut(float duration = -1f)
    {
        if (musicAudioSource == null) return;
        float d = duration < 0f ? musicFadeDuration : duration;
        if (_musicFadeCoroutine != null) StopCoroutine(_musicFadeCoroutine);
        _musicFadeCoroutine = StartCoroutine(FadeMusicCoroutine(musicAudioSource.volume, 0f, d));
    }

    public void MusicFadeIn(float duration = -1f)
    {
        if (musicAudioSource == null) return;
        float d = duration < 0f ? musicFadeDuration : duration;
        if (_musicFadeCoroutine != null) StopCoroutine(_musicFadeCoroutine);
        _musicFadeCoroutine = StartCoroutine(FadeMusicCoroutine(musicAudioSource.volume, 1f, d));
    }

    private IEnumerator FadeMusicCoroutine(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            musicAudioSource.volume = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        musicAudioSource.volume = to;
        _musicFadeCoroutine = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyLoopState(_currentLoop);
    }
#endif
}
