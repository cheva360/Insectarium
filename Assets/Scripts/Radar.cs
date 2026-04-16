using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Radar : MonoBehaviour
{
    [SerializeField] private GameObject RadarLine;
    [SerializeField] private GameObject RadarChecker;
    [SerializeField] private Transform PlayerRotate;
    [SerializeField] private Transform MinimapRotate;
    
    private float radarLineRotation = 0f;
    private float radarCheckerRotation = 0f;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // Accumulate rotation over time for smooth animation
        radarLineRotation -= 30f * Time.deltaTime; // 30 degrees per second
        radarCheckerRotation += 30f * Time.deltaTime; // 30 degrees per second
        
        RadarLine.transform.localRotation = Quaternion.Euler(RadarLine.transform.localEulerAngles.x, RadarLine.transform.localEulerAngles.y, radarLineRotation);
        RadarChecker.transform.localRotation = Quaternion.Euler(0, radarCheckerRotation, 0);
        
        RadarChecker.transform.position = GameController.Instance.player.transform.position;
        PlayerRotate.transform.rotation = Quaternion.Euler(0, GameController.Instance.player.transform.eulerAngles.y, 0);
        MinimapRotate.transform.rotation = Quaternion.Euler(MinimapRotate.eulerAngles.x, MinimapRotate.eulerAngles.y, GameController.Instance.player.transform.eulerAngles.y);
    }

    
}
