using System.Collections;
using UnityEngine;

public class TutorialDoor : MonoBehaviour
{
    //this is only used for grayboxing!!! WILL DELETE OR CHANGE THIS AFTERWARD

    [SerializeField] private GameObject TutorialTape;

    // Update is called once per frame
    void Update()
    {
        if (TutorialTape == null) 
        StartCoroutine(CountDown());
        
    }

    IEnumerator CountDown()
    {
        yield return new WaitForSeconds(3.0f);

        OpenDoor();
    }

    private void OpenDoor()
    {
        gameObject.SetActive(false);
    }
}
