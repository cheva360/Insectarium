using System.Collections;
using UnityEngine;

public class Jumpscare : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform jumpscareFace;
    [SerializeField] private Material psxMaterial;
    [Tooltip("The decoder's startAudio clip — used to derive the total effect duration.")]
    [SerializeField] private AudioClip decoderAudioLog;

    [Header("Ambient Settings")]
    [Tooltip("Ambient value at the start of the audio.")]
    [SerializeField] private float ambientStart = 0f;
    [Tooltip("Peak ambient value reached by the end of the audio.")]
    [SerializeField] private float ambientPeak = 0.02f;
    [Tooltip("Ambient value lerped to during the slide.")]
    [SerializeField] private float ambientSlideTarget = -5f;

    [Header("Slide Settings")]
    [Tooltip("How many seconds before the end of the audio the slide begins.")]
    [SerializeField] private float slideStartOffset = 2f;
    [Tooltip("How long the slide takes to complete in seconds.")]
    [SerializeField] private float slideMoveDuration = 2f;
    [Tooltip("Local position offset applied during the slide (X, Y, Z).")]
    [SerializeField] private Vector3 slideDelta = new Vector3(-0.5f, -2f, 0f);

    private static readonly int KID            = Shader.PropertyToID("_k");
    private static readonly int SnapIntensityID = Shader.PropertyToID("_SnapIntensity");

    // ── Editor reset state ───────────────────────────────────────────────────
    private Vector4 _originalK;
    private float   _originalSnapIntensity;
    private Vector3 _originalFaceLocalPosition;

    private void OnEnable()
    {
        if (psxMaterial == null || decoderAudioLog == null || jumpscareFace == null) return;

        _originalK                 = psxMaterial.GetVector(KID);
        _originalSnapIntensity     = psxMaterial.GetFloat(SnapIntensityID);
        _originalFaceLocalPosition = jumpscareFace.localPosition;

        float audioDuration = decoderAudioLog.length;

        StartCoroutine(LerpPSXEffect(audioDuration));
        StartCoroutine(SlideNearEnd(audioDuration));
    }

#if UNITY_EDITOR
    private void OnDisable()
    {
        if (psxMaterial == null) return;

        psxMaterial.SetVector(KID, _originalK);
        psxMaterial.SetFloat(SnapIntensityID, _originalSnapIntensity);

        if (jumpscareFace != null)
            jumpscareFace.localPosition = _originalFaceLocalPosition;
    }
#endif

    // ── PSX material lerp ────────────────────────────────────────────────────

    private IEnumerator LerpPSXEffect(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Vector4 k = psxMaterial.GetVector(KID);
            k.x = Mathf.Lerp(ambientStart, ambientPeak, t);
            psxMaterial.SetVector(KID, k);

            psxMaterial.SetFloat(SnapIntensityID, Mathf.Lerp(0.0001f, 0.05f, t));

            yield return null;
        }

        Vector4 kFinal = psxMaterial.GetVector(KID);
        kFinal.x = ambientPeak;
        psxMaterial.SetVector(KID, kFinal);
        psxMaterial.SetFloat(SnapIntensityID, 0.05f);
    }

    // ── Position slide + ambient drive during slide ──────────────────────────

    private IEnumerator SlideNearEnd(float audioDuration)
    {
        float waitTime = audioDuration - slideStartOffset;
        if (waitTime > 0f)
            yield return new WaitForSeconds(waitTime);

        Vector3 startPos = jumpscareFace.localPosition;
        Vector3 endPos   = startPos + slideDelta;
        float   elapsed  = 0f;

        while (elapsed < slideMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t     = Mathf.Clamp01(elapsed / slideMoveDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f); // ease out cubic

            jumpscareFace.localPosition = Vector3.LerpUnclamped(startPos, endPos, eased);

            // Lerp ambient from peak → ambientSlideTarget over the slide duration
            Vector4 k = psxMaterial.GetVector(KID);
            k.x = Mathf.Lerp(ambientPeak, ambientSlideTarget, t);
            psxMaterial.SetVector(KID, k);

            yield return null;
        }

        jumpscareFace.localPosition = endPos;

        Vector4 kFinal = psxMaterial.GetVector(KID);
        kFinal.x = ambientSlideTarget;
        psxMaterial.SetVector(KID, kFinal);

        jumpscareFace.gameObject.SetActive(false);
    }
}
