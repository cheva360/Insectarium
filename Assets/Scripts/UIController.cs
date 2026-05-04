using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    public static UIController Instance { get; private set; }

    public TextMeshProUGUI InteractText;
    public Image Fade;
    public Volume PostProcessingVolume;
    public GameObject UIEntryBackingPrefab;
    public GameObject UIEntryParent;
    public GameObject UIEntryCollectedPrefab;
    public GameObject UIEntryCollectedParent;
    public int UIEntryCount = 0;
    public int UICollectedCount = 0;
    public float ShakeMagnitude = 0f;

    private MonoBehaviour _currentInteractable;

    void Awake()
    {

        Instance = this;
    }

    public void RequestInteractText(MonoBehaviour requester)
    {
        _currentInteractable = requester;
        InteractText.text = "[E]";
    }

    public void ReleaseInteractText(MonoBehaviour requester)
    {
        if (_currentInteractable == requester)
        {
            _currentInteractable = null;
            InteractText.text = "";
        }
    }

    public void AddUIEntry()
    {
        //Instantiate prefab and set parent to UIEntryParent
        Instantiate(UIEntryBackingPrefab, UIEntryParent.transform);
        UIEntryCount++;

    }

    public void AddCollected()
    {
        if (UICollectedCount < UIEntryCount)
        {
            Instantiate(UIEntryCollectedPrefab, UIEntryCollectedParent.transform);
            UICollectedCount++;

        }
    }

    // Update is called once per frame
    void Update()
    {
        //camera shake
        Camera.main.transform.localPosition = new Vector3(0, 0.24f, 0);
        Vector3 shakeOffset = Random.insideUnitCircle * ShakeMagnitude;
        Camera.main.transform.localPosition += new Vector3(shakeOffset.x, shakeOffset.y, 0);
    }
}
