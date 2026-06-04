using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class RadarBug : MonoBehaviour
{
    private float _interactionDistance = 3f;
    private float _lookAtRadius = 0.5f;

    [SerializeField] private Transform cameraLookTarget;
    [SerializeField] private Transform playerLerpTarget;

    [Header("Camera Lerp Settings")]
    [SerializeField] private float cameraLerpSpeed = 3f;
    [SerializeField] private float cameraReturnLerpSpeed = 6f;

    [Header("Hand Interaction")]
    [SerializeField] private Animator handAnimator;
    [SerializeField] private GameObject collectibleObject;
    [SerializeField] private Button raiseButton;
    [SerializeField] private AudioSource raiseAudioSource;
    [SerializeField] private AudioClip raiseClip;
    [SerializeField] private AudioClip cassetteCollectClip;   // ← new field

    private bool _sequenceStarted = false;
    private bool _raisePressed = false;
    private Vector3 _capturedPlayerPosition;
    private GameObject _returnCameraTargetGO;

    private bool IsLookingAt()
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.SphereCast(ray, _lookAtRadius, out RaycastHit hit, _interactionDistance))
            return hit.collider != null && hit.collider.gameObject == gameObject;
        return false;
    }

    void Start()
    {
        UIController.Instance.AddUIEntry();

        if (raiseButton != null)
            raiseButton.gameObject.SetActive(false);
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
                _capturedPlayerPosition = GameController.Instance.player.transform.position;

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
        playerController.Instance.radarHidden = true;
        playerController.Instance.SetState(playerController.playerState.Cutscene);

        _raisePressed = false;
        if (raiseButton != null)
            raiseButton.onClick.AddListener(() => _raisePressed = true);

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

        int raiseState = 0;
        handAnimator.SetInteger("RaiseState", raiseState);

        if (raiseButton != null)
            raiseButton.gameObject.SetActive(true);

        while (true)
        {
            yield return new WaitUntil(() => _raisePressed);
            _raisePressed = false;
            yield return null;

            if (raiseState >= 4)
            {
                collectibleObject.SetActive(false);

                if (raiseAudioSource != null && cassetteCollectClip != null)
                    raiseAudioSource.PlayOneShot(cassetteCollectClip);   // ← collect SFX

                UIController.Instance.AddCollected();
                break;
            }

            raiseState = Mathf.Min(raiseState + 1, 4);
            handAnimator.SetInteger("RaiseState", raiseState);

            if (raiseAudioSource != null && raiseClip != null)
                raiseAudioSource.PlayOneShot(raiseClip);
        }

        if (raiseButton != null)
        {
            raiseButton.gameObject.SetActive(false);
            raiseButton.onClick.RemoveAllListeners();
        }

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

        playerController.Instance.radarHidden = false;
        playerController.Instance.SetState(playerController.playerState.Normal);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        UIController.Instance.RestoreEntryParents();
    }
}