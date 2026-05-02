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
    public int UIEntryCount = 0;

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

    // Start is called before the first frame update
    void Start()
    {
        
    }

    //method named interact text
    public void isInteractingText(bool isInteracting)
    {
        InteractText.text = isInteracting ? "[E]" : "";
    }

    public void AddUIEntry()
    {
        //Instantiate prefab and set parent to UIEntryParent
        Instantiate(UIEntryBackingPrefab, UIEntryParent.transform);

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
