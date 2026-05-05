using UnityEngine;

public class Decoder : MonoBehaviour
{
    private float _interactionDistance = 3f;

    [Tooltip("Assign one of the 8 DecoderWordData scriptable objects here.")]
    public DecoderWordData wordData;

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
                UIController.Instance.PlayDecoderTypewriter(wordData);

                if (UIController.Instance.UICollectedCount == UIController.Instance.UIEntryCount)
                {
                    Debug.Log("Interacted with Decoder");
                    UIController.Instance.ReleaseInteractText(this);
                    //UIController.Instance.PlayDecoderTypewriter(wordData);
                }
            }
        }
        else
        {
            UIController.Instance.ReleaseInteractText(this);
        }
    }
}
