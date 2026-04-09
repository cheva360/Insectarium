using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraTransform : MonoBehaviour
{
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float yOffset = 0.24f;
    [SerializeField] private float lerpSpeed = 500f;

    // Start is called before the first frame update
    void Start()
    {
        if (playerTransform == null)
        {
            playerTransform = GetComponentInParent<Transform>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (playerTransform == null)
            return;

        Vector3 targetPosition = new Vector3(playerTransform.position.x, playerTransform.position.y + yOffset, playerTransform.position.z);
        transform.position = Vector3.Lerp(transform.position, targetPosition, lerpSpeed * Time.deltaTime);
    }
}
