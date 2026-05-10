using UnityEngine;

public class Decoder : MonoBehaviour
{
    private float _interactionDistance = 3f;
    private float _lookAtRadius = 0.1f; // tolerance radius for close-range detection

    [Tooltip("Assign one of the 8 DecoderWordData scriptable objects here.")]
    public DecoderWordData wordData;

    private void Start()
    {

    }

    private bool IsLookingAt()
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.SphereCast(ray, _lookAtRadius, out RaycastHit hit, _interactionDistance))
        {
            return hit.collider != null && hit.collider.gameObject == gameObject;
        }
        return false;
    }

    void Update()
    {
        bool inRange = Vector3.Distance(transform.position, GameController.Instance.player.transform.position) <= _interactionDistance;

        if (inRange && IsLookingAt())
        {
            UIController.Instance.RequestInteractText(this);

            if (Input.GetKeyDown(KeyCode.E))
            {
                UIController.Instance.PlayDecoderTypewriter(wordData);

                if (UIController.Instance.UICollectedCount == UIController.Instance.UIEntryCount)
                {
                    Debug.Log("Interacted with Decoder");
                    UIController.Instance.ReleaseInteractText(this);
                }
            }
        }
        else
        {
            UIController.Instance.ReleaseInteractText(this);
        }
    }
}
