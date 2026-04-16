using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class radarPoint : MonoBehaviour
{
    
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        // Keep z rotation at 0 in world space to stay upright regardless of parent rotation
        Vector3 currentRotation = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, 0f);
    }
    
}
