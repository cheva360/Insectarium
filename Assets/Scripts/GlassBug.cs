using System.Collections;
using UnityEngine;

public class GlassBug : MonoBehaviour
{
    [SerializeField] private Animator _glassBugAnimator;
    [SerializeField] private Renderer[] _glassBugRenderers;
    [SerializeField] private GameObject[] _triggerObjects;
    [SerializeField] private Light _bugLight;

    [Header("Audio")]
    [SerializeField] private AudioSource _jumpscareAudioSource;
    [SerializeField] private AudioSource _bangAudioSource;
    [SerializeField] private AudioSource _loopAudioSource;
    [SerializeField] private AudioClip _jumpscareClip;
    [SerializeField] private AudioClip _bangClip;
    [SerializeField] private AudioClip _loopClip;
    [SerializeField] [Range(0f, 1f)] private float _jumpscareVolume = 1f;

    [Header("Timing")]
    [SerializeField] [Range(0f, 2f)] private float _jumpscareDelay = 0.15f;
    [SerializeField] private float _activeDuration = 5f;
    [SerializeField] private float _fadeOutDuration = 1.5f;

    [Header("Light Flicker")]
    [SerializeField] private float _flickerMinInterval = 0.05f;
    [SerializeField] private float _flickerMaxInterval = 0.2f;
    [SerializeField] [Range(0f, 1f)] private float _flickerMinIntensity = 0.1f;
    [SerializeField] private float _flickerLerpSpeed = 8f;

    private bool _isAnimPounding;
    private bool _hasTriggeredOnce;
    private float _initialLightIntensity;
    private Coroutine _flickerCoroutine;

    void Start()
    {
        SetBugVisible(false);

        foreach (var obj in _triggerObjects)
            if (obj != null) obj.SetActive(false);

        if (_bugLight != null)
        {
            _initialLightIntensity = _bugLight.intensity;
            _bugLight.enabled = false;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Player" && !_isAnimPounding && !_hasTriggeredOnce)
        {
            _isAnimPounding = true;
            _hasTriggeredOnce = true;

            if (_jumpscareAudioSource != null && _jumpscareClip != null)
                _jumpscareAudioSource.PlayOneShot(_jumpscareClip, _jumpscareVolume);

            StartCoroutine(ActivateAfterDelay());
        }
    }

    private IEnumerator ActivateAfterDelay()
    {
        yield return new WaitForSeconds(_jumpscareDelay);
        Activate();
    }

    private void Activate()
    {
        _glassBugAnimator.SetTrigger("Rise");
        SetBugVisible(true);

        foreach (var obj in _triggerObjects)
            if (obj != null) obj.SetActive(true);

        if (_bugLight != null)
        {
            _bugLight.enabled = true;
            _bugLight.intensity = _initialLightIntensity;
            _flickerCoroutine = StartCoroutine(FlickerLight());
        }

        if (_loopAudioSource != null && _loopClip != null)
        {
            _loopAudioSource.clip = _loopClip;
            _loopAudioSource.loop = true;
            _loopAudioSource.volume = 1f;
            _loopAudioSource.Play();
        }

        GameController.Instance.MusicFadeOut();
        StartCoroutine(DeactivateAfterDuration());
    }

    private IEnumerator DeactivateAfterDuration()
    {
        yield return new WaitForSeconds(_activeDuration);
        yield return StartCoroutine(FadeOutAndDeactivate());
    }

    private IEnumerator FadeOutAndDeactivate()
    {
        if (_flickerCoroutine != null)
        {
            StopCoroutine(_flickerCoroutine);
            _flickerCoroutine = null;
        }

        float elapsed = 0f;
        float startLightIntensity = _bugLight != null && _bugLight.enabled ? _bugLight.intensity : 0f;
        float startJumpscareVol = _jumpscareAudioSource != null ? _jumpscareAudioSource.volume : 0f;
        float startBangVol = _bangAudioSource != null ? _bangAudioSource.volume : 0f;
        float startLoopVol = _loopAudioSource != null ? _loopAudioSource.volume : 0f;

        while (_bugLight != null && _bugLight.intensity > 0.001f || elapsed < _fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _fadeOutDuration);

            if (_bugLight != null)
                _bugLight.intensity = Mathf.Lerp(startLightIntensity, 0f, t);

            if (_jumpscareAudioSource != null)
                _jumpscareAudioSource.volume = Mathf.Lerp(startJumpscareVol, 0f, t);

            if (_bangAudioSource != null)
                _bangAudioSource.volume = Mathf.Lerp(startBangVol, 0f, t);

            if (_loopAudioSource != null)
                _loopAudioSource.volume = Mathf.Lerp(startLoopVol, 0f, t);

            yield return null;
        }

        // Guarantee exact zero before deactivating
        if (_bugLight != null) _bugLight.intensity = 0f;
        if (_jumpscareAudioSource != null) _jumpscareAudioSource.volume = 0f;
        if (_bangAudioSource != null) _bangAudioSource.volume = 0f;
        if (_loopAudioSource != null) _loopAudioSource.volume = 0f;

        _glassBugAnimator.SetTrigger("Drop");
        _isAnimPounding = false;
        SetBugVisible(false);

        if (_bugLight != null)
            _bugLight.enabled = false;

        if (_loopAudioSource != null)
            _loopAudioSource.Stop();

        // Restore volumes for next activation
        if (_jumpscareAudioSource != null) _jumpscareAudioSource.volume = 1f;
        if (_bangAudioSource != null) _bangAudioSource.volume = 1f;

        foreach (var obj in _triggerObjects)
            if (obj != null) obj.SetActive(false);

        GameController.Instance.MusicFadeIn();
    }

    /// <summary>
    /// Call this from Animation Events at the frames where banging occurs.
    /// </summary>
    public void PlayBangSound()
    {
        if (_bangAudioSource != null && _bangClip != null)
            _bangAudioSource.PlayOneShot(_bangClip);
    }

    private void SetBugVisible(bool visible)
    {
        foreach (var r in _glassBugRenderers)
            if (r != null) r.enabled = visible;
    }

    private IEnumerator FlickerLight()
    {
        float targetIntensity = _initialLightIntensity;

        while (true)
        {
            // Pick a new random target and lerp toward it over time
            targetIntensity = Random.Range(_flickerMinIntensity * _initialLightIntensity, _initialLightIntensity);
            float waitTime = Random.Range(_flickerMinInterval, _flickerMaxInterval);
            float elapsed = 0f;

            while (elapsed < waitTime)
            {
                elapsed += Time.deltaTime;
                _bugLight.intensity = Mathf.Lerp(_bugLight.intensity, targetIntensity, Time.deltaTime * _flickerLerpSpeed);
                yield return null;
            }
        }
    }
}
