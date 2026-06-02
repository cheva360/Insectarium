using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Decoder : MonoBehaviour
{
    private float _interactionDistance = 3f;
    private float _lookAtRadius = 0.1f;

    [Header("Audio Logs")]
    [Tooltip("All audio log entries in sequential order across all loops.")]
    [SerializeField] private List<DecoderWordData> audioLogs = new List<DecoderWordData>();

    [Header("Unlock Gate")]
    [Tooltip("When enabled the decoder is always interactable, ignoring cassette collection count.")]
    [SerializeField] private bool debugUnlockAlways = false;

    [SerializeField] private Transform casetteLookTarget;
    [SerializeField] private GameObject cassette;
    [SerializeField] private GameObject casseteFlap;
    [SerializeField] private Transform dialogueLookTarget;
    [SerializeField] private Transform playerLerpTarget;

    [Header("Cassette Insert Settings")]
    [Tooltip("How fast the camera lerps to the cassette look target.")]
    [SerializeField] private float cameraLerpSpeed = 3f;
    [Tooltip("How fast the camera lerps back after the sequence. Should be faster than cameraLerpSpeed.")]
    [SerializeField] private float cameraReturnLerpSpeed = 6f;
    [Tooltip("How sensitive the mouse is when sliding the cassette (units per mouse delta).")]
    [SerializeField] private float cassetteMouseSensitivity = 0.015f;

    [Header("Light Dimming")]
    [SerializeField] private Light decoderLight;
    [SerializeField] private OccaSoftware.Buto.Runtime.ButoLight decoderButoLight;
    [SerializeField] private Light secondaryLight;
    [SerializeField] private float lightDimDuration = 0.8f;
    [SerializeField] private float lightRestoreDuration = 0.8f;

    [Header("Jumpscare")]
    [SerializeField] private GameObject jumpscareFace;

    [Header("Cockroach")]
    [SerializeField] private SmallCockroach smallCockroach;

    [Header("Cassette SFX")]
    [SerializeField] private AudioSource cassetteAudioSource;
    [SerializeField] private AudioClip flapOpenClip;
    [SerializeField] private AudioClip cassetteInsertedClip;

    private float _originalLightIntensity;
    private float _originalSecondaryLightIntensity;
    private Coroutine _lightCoroutine;
    private Coroutine _secondaryLightCoroutine;
    private bool _sequenceStarted = false;

    // Persistent across loops — tracks which audio log to start from next session.
    private int _audioLogIndex = 0;

    private bool IsLookingAt()
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.SphereCast(ray, _lookAtRadius, out RaycastHit hit, _interactionDistance))
            return hit.collider != null && hit.collider.gameObject == gameObject;
        return false;
    }

    void Update()
    {
        if (_sequenceStarted ||
            playerController.Instance.CurrentState == playerController.playerState.InDialogue)
        {
            UIController.Instance.ReleaseInteractText(this);
            return;
        }

        bool inRange = Vector3.Distance(transform.position, GameController.Instance.player.transform.position) <= _interactionDistance;
        bool unlocked = debugUnlockAlways ||
                        (UIController.Instance.UIEntryCount > 0 &&
                         UIController.Instance.UICollectedCount >= UIController.Instance.UIEntryCount);

        if (inRange && IsLookingAt() && unlocked)
        {
            UIController.Instance.RequestInteractText(this);

            if (Input.GetKeyDown(KeyCode.E))
            {
                UIController.Instance.ReleaseInteractText(this);
                _sequenceStarted = true;
                StartCoroutine(CassetteInsertSequence());
            }
        }
        else
        {
            UIController.Instance.ReleaseInteractText(this);
        }
    }

    private IEnumerator CassetteInsertSequence()
    {
        Vector3 originalPosition = GameController.Instance.player.transform.position;

        // When using the debug override and nothing has been collected yet,
        // fall back to playing all remaining logs so the sequence always runs.
        int logsThisSession = (debugUnlockAlways && UIController.Instance.UICollectedCount == 0)
            ? Mathf.Max(audioLogs.Count - _audioLogIndex, 0)
            : UIController.Instance.UICollectedCount;

        int startIndex = _audioLogIndex;

        if (logsThisSession == 0 || startIndex >= audioLogs.Count)
        {
            // Nothing to play — abort cleanly without touching dialogue state.
            _sequenceStarted = false;
            yield break;
        }

        // ── Initial setup ─────────────────────────────────────────────────────
        playerController.Instance.radarHidden = true;
        playerController.Instance.SetState(playerController.playerState.Cutscene);

        if (decoderLight != null)
        {
            _originalLightIntensity = decoderLight.intensity;
            if (_lightCoroutine != null) StopCoroutine(_lightCoroutine);
            _lightCoroutine = StartCoroutine(LerpLightIntensity(decoderLight, decoderLight.intensity, 0f, lightDimDuration));
        }

        if (secondaryLight != null)
        {
            _originalSecondaryLightIntensity = secondaryLight.intensity;
            if (_secondaryLightCoroutine != null) StopCoroutine(_secondaryLightCoroutine);
            _secondaryLightCoroutine = StartCoroutine(LerpSimpleLightIntensity(secondaryLight, secondaryLight.intensity, 15f, lightDimDuration));
        }

        Vector3 cassetteResetPos = cassette != null ? cassette.transform.localPosition : Vector3.zero;
        Renderer cassetteRenderer = cassette != null ? cassette.GetComponent<Renderer>() : null;

        if (cassette != null)
            cassette.SetActive(true);

        int logsPlayed = 0;

        for (int i = 0; i < logsThisSession; i++)
        {
            int logIndex = startIndex + i;
            if (logIndex >= audioLogs.Count) break;

            // ── Reset cassette for each log after the first ───────────────────
            if (i > 0)
            {
                UIController.Instance.RestoreEntryParentsOnly();

                if (cassette != null)
                {
                    cassette.transform.localPosition = cassetteResetPos;
                    if (cassetteRenderer != null)
                        cassetteRenderer.material.SetFloat("_SnapIntensity", 0.0001f);
                }
                if (casseteFlap != null)
                {
                    Vector3 e = casseteFlap.transform.localEulerAngles;
                    casseteFlap.transform.localEulerAngles = new Vector3(e.x, e.y, 0f);
                }
            }

            // ── Phase 1: lerp camera + player to casetteLookTarget ────────────
            if (casetteLookTarget != null)
            {
                while (!playerController.Instance.LerpCameraTowardsTarget(casetteLookTarget, cameraLerpSpeed))
                {
                    if (playerLerpTarget != null)
                        GameController.Instance.player.transform.position = Vector3.Lerp(
                            GameController.Instance.player.transform.position,
                            playerLerpTarget.position,
                            cameraLerpSpeed * Time.deltaTime);
                    yield return null;
                }
            }

            // ── Phase 2: mouse-driven cassette slide ──────────────────────────
            if (cassette != null)
            {
                float startX = cassetteResetPos.x;   // ~2  (furthest out)
                float endX = -0.4f;               // fully inserted
                bool cockroachTriggered = false;
                bool flapSoundTriggered = false;

                while (true)
                {
                    float mouseX = Input.GetAxis("Mouse X");
                    float mouseY = Input.GetAxis("Mouse Y");
                    float delta  = (mouseX * -1f + mouseY) * 0.5f * cassetteMouseSensitivity;

                    Vector3 lp = cassette.transform.localPosition;
                    lp.x = Mathf.Clamp(lp.x - delta, endX, startX);
                    cassette.transform.localPosition = lp;

                    float progress = Mathf.InverseLerp(startX, endX, lp.x);

                    if (cassetteRenderer != null)
                    {
                        float eased = 1f - Mathf.Pow(1f - progress, 3f);
                        cassetteRenderer.material.SetFloat("_SnapIntensity", Mathf.Lerp(0.0001f, 0.1f, eased));
                    }

                    if (casseteFlap != null)
                    {
                        float flapProgress = Mathf.InverseLerp(0.2f, 1f, progress);
                        float easedFlap = 1f - Mathf.Pow(1f - flapProgress, 3f);
                        float zRot = Mathf.Lerp(0f, -90f, easedFlap);
                        Vector3 euler = casseteFlap.transform.localEulerAngles;
                        casseteFlap.transform.localEulerAngles = new Vector3(euler.x, euler.y, zRot);

                        if (easedFlap >= 0.175f)
                        {
                            if (!flapSoundTriggered)
                            {
                                flapSoundTriggered = true;
                                if (cassetteAudioSource != null && flapOpenClip != null)
                                    cassetteAudioSource.PlayOneShot(flapOpenClip);
                            }
                        }
                        else
                        {
                            flapSoundTriggered = false;
                        }

                        if (easedFlap >= 0.75f && !cockroachTriggered)
                        {
                            cockroachTriggered = true;
                            if (smallCockroach != null)
                                smallCockroach.hasStartedMoving = true;
                        }
                    }

                    if (lp.x <= endX)
                    {
                        if (cassetteAudioSource != null && cassetteInsertedClip != null)
                            cassetteAudioSource.PlayOneShot(cassetteInsertedClip);

                        UIController.Instance.TriggerLatestEntryFillOut(destroyDelay: 0.5f);
                        break;
                    }

                    yield return null;
                }

                if (casseteFlap != null)
                    StartCoroutine(ReturnFlap());
            }

            yield return new WaitForSeconds(1.2f);

            // ── Phase 3: dialogue + audio log playback ────────────────────────
            Transform lookTarget = dialogueLookTarget != null ? dialogueLookTarget : transform;
            UIController.Instance.SetActiveDecoder(this);
            playerController.Instance.EnterDialogue(lookTarget, playerLerpTarget, originalPosition);

            yield return new WaitForSeconds(1.5f);

            bool logFinished = false;
            UIController.Instance.PlayDecoderTypewriter(audioLogs[logIndex], () => logFinished = true);
            yield return new WaitUntil(() => logFinished);

            logsPlayed++;

            bool moreLogsRemain = i < logsThisSession - 1 && (startIndex + i + 1) < audioLogs.Count;
            if (moreLogsRemain)
                playerController.Instance.SetState(playerController.playerState.Cutscene);
        }

        _audioLogIndex += logsPlayed;

        // ── Final cleanup ─────────────────────────────────────────────────────
        UIController.Instance.RestoreEntryParents();
        playerController.Instance.ExitDialogue();

        if (UIController.Instance.UICollectedCount == UIController.Instance.UIEntryCount)
            Debug.Log("Interacted with Decoder");

        gameObject.GetComponent<LoopTriggerTest>().DecoderTriggered();
    }

    /// <summary>
    /// Called by UIController when the typewriter finishes and the crosshair is restored.
    /// </summary>
    public void RestoreLight()
    {
        if (decoderLight != null)
        {
            if (_lightCoroutine != null) StopCoroutine(_lightCoroutine);
            _lightCoroutine = StartCoroutine(LerpLightIntensity(decoderLight, decoderLight.intensity, _originalLightIntensity, lightRestoreDuration));
        }

        if (secondaryLight != null)
        {
            if (_secondaryLightCoroutine != null) StopCoroutine(_secondaryLightCoroutine);
            _secondaryLightCoroutine = StartCoroutine(LerpSimpleLightIntensity(secondaryLight, secondaryLight.intensity, _originalSecondaryLightIntensity, lightRestoreDuration));
        }
    }

    private IEnumerator LerpLightIntensity(Light light, float from, float to, float duration)
    {
        float butoFrom = decoderButoLight != null ? decoderButoLight.LightIntensity : from;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            light.intensity = Mathf.Lerp(from, to, t);

            if (decoderButoLight != null)
                decoderButoLight.LightIntensity = Mathf.Lerp(butoFrom, to, t);

            yield return null;
        }

        light.intensity = to;
        if (decoderButoLight != null)
            decoderButoLight.LightIntensity = to;
        _lightCoroutine = null;
    }

    private IEnumerator LerpSimpleLightIntensity(Light light, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            light.intensity = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        light.intensity = to;
        _secondaryLightCoroutine = null;
    }

    private IEnumerator ReturnFlap()
    {
        float flapReturnSpeed = 3f;
        float elapsed = 0f;
        float duration = 1f / flapReturnSpeed;
        float startRot = -90f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float zRot = Mathf.Lerp(startRot, 0f, t);
            Vector3 euler = casseteFlap.transform.localEulerAngles;
            casseteFlap.transform.localEulerAngles = new Vector3(euler.x, euler.y, zRot);
            yield return null;
        }

        Vector3 finalEuler = casseteFlap.transform.localEulerAngles;
        casseteFlap.transform.localEulerAngles = new Vector3(finalEuler.x, finalEuler.y, 0f);
    }
}
