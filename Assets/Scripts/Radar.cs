using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Radar : MonoBehaviour
{
    [SerializeField] private GameObject RadarLine;
    [SerializeField] private GameObject RadarChecker;
    [SerializeField] private Transform PlayerRotate;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        RadarLine.transform.Rotate(0, 0, -0.1f);
        RadarChecker.transform.Rotate(0, 0.1f, 0);
        RadarChecker.transform.position = GameController.Instance.player.transform.position;
        PlayerRotate.transform.rotation = new Quaternion(0, GameController.Instance.player.transform.rotation.y, 0, GameController.Instance.player.transform.rotation.w);


    }

    
}
