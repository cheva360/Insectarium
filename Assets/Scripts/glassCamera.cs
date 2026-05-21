using UnityEngine;

public class glassCamera : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;

    void LateUpdate()
    {
        if (mainCamera == null) return;

        transform.SetPositionAndRotation(mainCamera.transform.position, mainCamera.transform.rotation);
    }
}
