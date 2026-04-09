using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RadarChecker : MonoBehaviour
{
    [SerializeField] private Transform RadarPoint1;
    [SerializeField] private float maxRadarDistance = 50f; // Maximum detection range
    
    // Start is called before the first frame update
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("RadarObj"))
        {
            // Calculate distance from player
            float distance = Vector3.Distance(GameController.Instance.player.transform.position, other.transform.position);

            // Calculate angle relative to player's forward direction
            Vector3 directionToObject = (other.transform.position - GameController.Instance.player.transform.position).normalized;
            float angle = Vector3.SignedAngle(GameController.Instance.player.transform.forward, directionToObject, Vector3.up);

            // Debug the information
            Debug.Log($"Radar Detection - Object: {other.gameObject.name} | Position: {other.transform.position} | Angle: {angle:F2}° | Distance: {distance:F2}m");

            // Map to radar coordinates (-0.21 to 0.21)
            float normalizedDistance = Mathf.Clamp01(distance / maxRadarDistance); // 0 to 1
            float radarDistance = normalizedDistance * 0.42f; // 0 to 0.21

            // Convert angle to radians for calculation
            float angleRad = angle * Mathf.Deg2Rad;

            // Calculate radar X and Y positions
            float radarX = radarDistance * Mathf.Sin(angleRad);
            float radarY = radarDistance * Mathf.Cos(angleRad);

            // Update RadarPoint1 local position
            RadarPoint1.localPosition = new Vector3(radarX, RadarPoint1.localPosition.y, -radarY);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
