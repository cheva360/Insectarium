using Unity.VisualScripting;
using UnityEngine;

public class GlassBug : MonoBehaviour
{
    [SerializeField] private Animator _glassBugAnimator;
    private bool _isAnimPounding;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Player"&& _isAnimPounding != true)
        {
            _glassBugAnimator.SetTrigger("Rise");
            _isAnimPounding = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.tag == "Player"&& _isAnimPounding == true)
        {
            _glassBugAnimator.SetTrigger("Drop");
            _isAnimPounding = false;
        }
    }
}
