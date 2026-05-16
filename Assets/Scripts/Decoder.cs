using UnityEngine;

public class Decoder : MonoBehaviour
{
    private float _interactionDistance = 3f;
    private float _lookAtRadius = 0.1f;

    [Tooltip("Assign one of the 8 DecoderWordData scriptable objects here.")]
    public DecoderWordData wordData;

    [SerializeField] private Transform dialogueLookTarget;
    [SerializeField] private Transform playerLerpTarget;

    private bool IsLookingAt()
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.SphereCast(ray, _lookAtRadius, out RaycastHit hit, _interactionDistance))
            return hit.collider != null && hit.collider.gameObject == gameObject;
        return false;
    }

    void Update()
    {
        // Never show interact text while in dialogue
        if (playerController.Instance.CurrentState == playerController.playerState.InDialogue)
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
                UIController.Instance.PlayDecoderTypewriter(wordData);

                playerController.Instance.radarHidden = true;
                playerController.Instance.EnterDialogue(
                    dialogueLookTarget != null ? dialogueLookTarget : transform,
                    playerLerpTarget
                );

                if (UIController.Instance.UICollectedCount == UIController.Instance.UIEntryCount)
                {
                    Debug.Log("Interacted with Decoder");
                    UIController.Instance.ReleaseInteractText(this);
                }

                //enable the looping trigger (this is only for first build testing, will delete in the future)
                gameObject.GetComponent<LoopTriggerTest>().DecoderTriggered();
            }

            
        }
        else
        {
            UIController.Instance.ReleaseInteractText(this);
        }
    }
}
