using UnityEngine;

public class Interactable : MonoBehaviour
{
    private float _interactionDistance = 3f;
    private float _lookAtRadius = 0.1f;

    [Header("Pickup SFX")]
    [SerializeField] private AudioClip pickupClip;

    private void Start()
    {
        UIController.Instance.AddUIEntry();
    }

    private bool IsLookingAt()
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = Physics.SphereCastAll(ray, _lookAtRadius, _interactionDistance, Physics.AllLayers, QueryTriggerInteraction.Collide);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider != null && hit.collider.gameObject == gameObject)
                return true;
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
                Debug.Log("Interacted with " + gameObject.name);
                UIController.Instance.ReleaseInteractText(this);
                UIController.Instance.AddCollected();

                if (pickupClip != null)
                    AudioSource.PlayClipAtPoint(pickupClip, transform.position);

                Destroy(gameObject);
            }
        }
        else
        {
            UIController.Instance.ReleaseInteractText(this);
        }
    }
}
