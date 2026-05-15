using UnityEngine;

public class TutorialDoor : MonoBehaviour
{
    //this is only used for grayboxing!!! WILL DELETE THIS AFTERWARD

    [SerializeField] private GameObject TutorialTape;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (TutorialTape = null) this.gameObject.SetActive(false);
    }
}
