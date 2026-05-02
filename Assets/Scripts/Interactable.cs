using UnityEngine;

public class Interactable : MonoBehaviour
{
    [SerializeField] private float _interactionDistance = 3f;


    private void Awake()
    {
        UIController.Instance.AddUIEntry();
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

    // Update is called once per frame
    void Update()
    {
        // Checking if player is within interaction distance
        if (Vector3.Distance(transform.position, GameController.Instance.player.transform.position) <= _interactionDistance)
        {
            // Only interact if the mouse cursor is hovering over this object
            if (IsMouseOver())
            {
                // Player interaction once they press E
                UIController.Instance.isInteractingText(true);

                if (Input.GetKeyDown(KeyCode.E))
                {
                    Debug.Log("Interacted with " + gameObject.name);
                    UIController.Instance.isInteractingText(false);
                    Destroy(gameObject);
                }

            }
            else
            {
                UIController.Instance.isInteractingText(false);
            }
        }
        else
        {
            UIController.Instance.isInteractingText(false);
        }
    }
}
