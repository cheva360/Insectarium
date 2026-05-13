using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    public GameObject player;
    public Transform MinimapTransform;
    public Transform SpawnLocation;
    //passing out sound
    public AudioClip passingOutSound;
    public AudioSource GameControllerAudioSource;

    public enum LoopCount
    {
        Zero = 0,
        One = 1,
        Two = 2,
        Three = 3
    }

    [SerializeField] private GameObject loopOneObject;
    [SerializeField] private GameObject loopTwoObject;
    [SerializeField] private GameObject loopThreeObject;

    [SerializeField] private LoopCount _currentLoop = LoopCount.Zero;
    public LoopCount CurrentLoop
    {
        get => _currentLoop;
        set
        {
            _currentLoop = value;
            ApplyLoopState(_currentLoop);
        }
    }

    private void ApplyLoopState(LoopCount loop)
    {
        loopOneObject?.SetActive(loop == LoopCount.One);
        loopTwoObject?.SetActive(loop == LoopCount.Two);
        loopThreeObject?.SetActive(loop == LoopCount.Three);
    }

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
        ApplyLoopState(_currentLoop);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyLoopState(_currentLoop);
    }
#endif
}
