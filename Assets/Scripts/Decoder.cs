using System.Collections;
using UnityEngine;

public class Decoder : MonoBehaviour
{
    private float _interactionDistance = 3f;
    private float _lookAtRadius = 0.1f;

    [Tooltip("Assign one of the 8 DecoderWordData scriptable objects here.")]
    public DecoderWordData wordData;

    [SerializeField] private Transform casetteLookTarget;
    [SerializeField] private GameObject cassette;
    [SerializeField] private GameObject casseteFlap;
    [SerializeField] private Transform dialogueLookTarget;
    [SerializeField] private Transform playerLerpTarget;

    [Header("Cassette Insert Settings")]
    [Tooltip("How fast the camera lerps to the cassette look target.")]
    [SerializeField] private float cameraLerpSpeed = 3f;
    [Tooltip("How sensitive the mouse is when sliding the cassette (units per mouse delta).")]
    [SerializeField] private float cassetteMouseSensitivity = 0.015f;

    private bool _sequenceStarted = false;

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

        if (inRange && IsLookingAt())
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
        // ── Phase 1: lerp camera + player to casetteLookTarget ────────────────
        playerController.Instance.radarHidden = true;
        playerController.Instance.SetState(playerController.playerState.Cutscene);

        if (cassette != null)
            cassette.SetActive(true);

        if (casetteLookTarget != null)
        {
            while (!playerController.Instance.LerpCameraTowardsTarget(casetteLookTarget, cameraLerpSpeed))
            {
                // Also lerp the player position toward playerLerpTarget, same as dialogue does
                if (playerLerpTarget != null)
                    GameController.Instance.player.transform.position = Vector3.Lerp(
                        GameController.Instance.player.transform.position,
                        playerLerpTarget.position,
                        cameraLerpSpeed * Time.deltaTime);

                yield return null;
            }
        }

        // ── Phase 2: mouse-driven cassette slide ──────────────────────────────
        if (cassette != null)
        {
            float startZ = cassette.transform.localPosition.z;
            float endZ   = startZ + 1.5f;

            Renderer cassetteRenderer = cassette.GetComponent<Renderer>();

            while (true)
            {
                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");
                float delta  = (mouseX * -1f + mouseY) * 0.5f * cassetteMouseSensitivity;

                Vector3 lp = cassette.transform.localPosition;
                lp.z = Mathf.Clamp(lp.z + delta, startZ, endZ);
                cassette.transform.localPosition = lp;

                float progress = Mathf.InverseLerp(startZ, endZ, lp.z);

                // Drive snap intensity – EaseOutCubic
                if (cassetteRenderer != null)
                {
                    float eased = 1f - Mathf.Pow(1f - progress, 3f);
                    float snapIntensity = Mathf.Lerp(0.0001f, 0.1f, eased);
                    cassetteRenderer.material.SetFloat("_SnapIntensity", snapIntensity);
                }

                // Drive flap rotation – EaseOutCubic (0 → -90), starts at 20% progress
                if (casseteFlap != null)
                {
                    float flapProgress = Mathf.InverseLerp(0.2f, 1f, progress);
                    float easedFlap = 1f - Mathf.Pow(1f - flapProgress, 3f); // EaseOutCubic
                    float zRot = Mathf.Lerp(0f, -90f, easedFlap);
                    Vector3 euler = casseteFlap.transform.localEulerAngles;
                    casseteFlap.transform.localEulerAngles = new Vector3(euler.x, euler.y, zRot);
                }

                if (lp.z >= endZ)
                    break;

                yield return null;
            }

            // ── Flap return: run independently so Phase 3 starts without waiting ──
            if (casseteFlap != null)
                StartCoroutine(ReturnFlap());

            Destroy(cassette);
        }
        //wait for 1 second
        yield return new WaitForSeconds(1f);

        // ── Phase 3: start dialogue (InDialogue state handles the camera lerp) ─
        Transform lookTarget = dialogueLookTarget != null ? dialogueLookTarget : transform;

        UIController.Instance.PlayDecoderTypewriter(wordData);
        playerController.Instance.EnterDialogue(lookTarget, playerLerpTarget);

        if (UIController.Instance.UICollectedCount == UIController.Instance.UIEntryCount)
            Debug.Log("Interacted with Decoder");

        gameObject.GetComponent<LoopTriggerTest>().DecoderTriggered();
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
