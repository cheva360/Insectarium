using UnityEngine;

public class DoorInteractable : MonoBehaviour
{
    private float _interactionDistance = 3f;

    private void Start()
    {

    }

    private bool IsMouseOver()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            return hit.collider != null && hit.collider.gameObject == gameObject;
        }
        return false;
    }

    void Update()
    {
        bool inRange = Vector3.Distance(transform.position, GameController.Instance.player.transform.position) <= _interactionDistance;

        if (inRange && IsMouseOver())
        {
            UIController.Instance.RequestInteractText(this);

            if (Input.GetKeyDown(KeyCode.E))
            {
                Debug.Log("Interacted with Door");
                UIController.Instance.ReleaseInteractText(this);
                //Destroy(gameObject);
            }
        }
        else
        {
            UIController.Instance.ReleaseInteractText(this);
        }
    }
}
