using UnityEngine;

public class LoopTriggerTest : MonoBehaviour
{
    //this is only for testing our first build, in which the decoder only plays one audio tape
    //will DELETE THIS in the future!! dont use it for anything else 
    public GameObject trigger;

    void Start()
    {
        trigger.SetActive(false);
    }

    //enable the trigger when interaccted with the decoder
    public void DecoderTriggered()
    {
        trigger.SetActive(true);
    }

    //disable the trigger again when loop 
    public void LoopTriggered()
    {
        trigger.SetActive(false);
    }
}
