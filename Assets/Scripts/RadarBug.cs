using System.Collections;
using UnityEngine;
 
public class RadarBug : MonoBehaviour
{
    private float _interactionDistance = 3f;
    private float _lookAtRadius = 0.1f;
 
    [SerializeField] private Transform cameraLookTarget;
    [SerializeField] private Transform playerLerpTarget;
 
    [Header("Camera Lerp Settings")]
    [Tooltip("How fast the camera lerps to the radar bug look target.")]
    [SerializeField] private float cameraLerpSpeed = 3f;
    [Tooltip("How fast the camera lerps back to the original position. Should be faster than cameraLerpSpeed.")]
    [SerializeField] private float cameraReturnLerpSpeed = 6f;
 
    // ── NEW ───────────────────────────────────────────────────────────────────
    [Header("Hand Interaction")]
    [SerializeField] private Animator handAnimator;
    [SerializeField] private string animationTrigger = "OpenHand";
    [SerializeField] private float animationDuration = 2f;
    [SerializeField] private GameObject collectibleObject;
    // ─────────────────────────────────────────────────────────────────────────
 
    private bool _sequenceStarted = false;
 
    // Captured at interaction time for the return lerp
    private Vector3 _capturedPlayerPosition;
    private GameObject _returnCameraTargetGO;
 
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
 
                // ── Snapshot the player's world state at the moment of interaction ──
                _capturedPlayerPosition = GameController.Instance.player.transform.position;
 
                // Place a temporary look target 5 units in front of the camera's current forward
                _returnCameraTargetGO = new GameObject("RadarBug_ReturnTarget");
                _returnCameraTargetGO.transform.position = Camera.main.transform.position
                    + Camera.main.transform.forward * 5f;
 
                StartCoroutine(RadarBugSequence());
            }
        }
        else
        {
            UIController.Instance.ReleaseInteractText(this);
        }
    }
 
    private IEnumerator RadarBugSequence()
    {
        // ── Phase 1: lerp camera + player to cameraLookTarget ─────────────────
        playerController.Instance.radarHidden = true;
        playerController.Instance.SetState(playerController.playerState.Cutscene);
 
        // Unlock and show the OS cursor — crosshair is hidden automatically via UIController
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
 
        if (cameraLookTarget != null)
        {
            while (!playerController.Instance.LerpCameraTowardsTarget(cameraLookTarget, cameraLerpSpeed))
            {
                if (playerLerpTarget != null)
                    GameController.Instance.player.transform.position = Vector3.Lerp(
                        GameController.Instance.player.transform.position,
                        playerLerpTarget.position,
                        cameraLerpSpeed * Time.deltaTime);
 
                yield return null;
            }
        }
 
        // ── Phase 2: play animation, then wait for player to collect the item ──
        handAnimator.SetTrigger(animationTrigger);
        yield return new WaitForSeconds(animationDuration);
 
        UIController.Instance.RequestInteractText(collectibleObject.GetComponent<MonoBehaviour>());
        yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.E));
        UIController.Instance.ReleaseInteractText(collectibleObject.GetComponent<MonoBehaviour>());
 
        collectibleObject.SetActive(false);
        UIController.Instance.AddCollected();
        // ──────────────────────────────────────────────────────────────────────
 
        // ── Phase 3: lerp camera + player back to captured position (faster) ──
        if (_returnCameraTargetGO != null)
        {
            while (!playerController.Instance.LerpCameraTowardsTarget(_returnCameraTargetGO.transform, cameraReturnLerpSpeed))
            {
                GameController.Instance.player.transform.position = Vector3.Lerp(
                    GameController.Instance.player.transform.position,
                    _capturedPlayerPosition,
                    cameraReturnLerpSpeed * Time.deltaTime);
 
                yield return null;
            }
 
            Destroy(_returnCameraTargetGO);
        }
 
        // ── Phase 4: restore state ─────────────────────────────────────────────
        playerController.Instance.radarHidden = false;
        playerController.Instance.SetState(playerController.playerState.Normal);
 
        // Re-lock and hide the OS cursor to return to gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
 
        // Re-enable UI parents after sequence ends
        UIController.Instance.RestoreEntryParents();
    }
}