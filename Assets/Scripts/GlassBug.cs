using Unity.VisualScripting;
using UnityEngine;

public class GlassBug : MonoBehaviour
{
    [SerializeField] private Animator _glassBugAnimator;
    [SerializeField] private Renderer[] _glassBugRenderers;
    [SerializeField] private GameObject[] _triggerObjects;
    private bool _isAnimPounding;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    void Start()
    {
        SetBugVisible(false);

        foreach (var obj in _triggerObjects)
            if (obj != null) obj.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Player" && _isAnimPounding != true)
        {
            _glassBugAnimator.SetTrigger("Rise");
            _isAnimPounding = true;

            SetBugVisible(true);

            foreach (var obj in _triggerObjects)
                if (obj != null) obj.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.tag == "Player" && _isAnimPounding == true)
        {
            _glassBugAnimator.SetTrigger("Drop");
            _isAnimPounding = false;

            SetBugVisible(false);

            foreach (var obj in _triggerObjects)
                if (obj != null) obj.SetActive(false);
        }
    }

    private void SetBugVisible(bool visible)
    {
        foreach (var r in _glassBugRenderers)
            if (r != null) r.enabled = visible;
    }
}
