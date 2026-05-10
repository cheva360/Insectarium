using UnityEngine;

public class Interactable : MonoBehaviour
{
    private float _interactionDistance = 3f;
    private float _lookAtRadius = 0.1f;

    private void Start()
    {
        UIController.Instance.AddUIEntry();
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
                Debug.Log("Interacted with " + gameObject.name);
                UIController.Instance.ReleaseInteractText(this);
                UIController.Instance.AddCollected();
                Destroy(gameObject);
            }
        }
        else
        {
            UIController.Instance.ReleaseInteractText(this);
        }
    }
}
