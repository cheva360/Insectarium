using System.Collections;
using UnityEngine;

public class TutorialDoor : MonoBehaviour
{
    //this is only used for grayboxing!!! WILL DELETE THIS AFTERWARD

    [SerializeField] private GameObject TutorialTape;

    // Update is called once per frame
    void Update()
    {
        if (TutorialTape == null)
        {
            StartCoroutine(CountDown());//countdown before door opens
            
        }
        
    }

    IEnumerator CountDown()
    {
        yield return new WaitForSeconds (5.0f);
        OpenDoor();

    }

    private void OpenDoor()//open the door!!! to the main level
    {
        gameObject.SetActive(false);
    }
}
