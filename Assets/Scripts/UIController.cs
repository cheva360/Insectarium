using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIController : MonoBehaviour
{
    public static UIController Instance { get; private set; }

    public TextMeshProUGUI InteractText;
    public GameObject UIEntryBackingPrefab;
    public GameObject UIEntryParent;
    public GameObject UIEntryCollectedPrefab;
    public GameObject UIEntryCollectedParent;
    public int UIEntryCount = 0;
    public int UICollectedCount = 0;

    private MonoBehaviour _currentInteractable;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
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
        
    }
}
