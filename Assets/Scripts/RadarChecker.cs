using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class RadarChecker : MonoBehaviour
{
    [SerializeField] private GameObject RadarPingPrefab; // Prefab for radar ping visualization
    [SerializeField] private GameObject RadarPingParent; // Parent object for radar prefab pings
    [SerializeField] private float maxRadarDistance = 50f; // Maximum detection range
    [SerializeField] private Shader radarPingShader; // Shader for radar ping visualization
    private int radarPass = 1; // Pass index for radar ping shader
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
            float radarDistance = normalizedDistance * 0.45f; // 0 to 0.45

            // Convert angle to radians for calculation
            // ADDS PLAYER ROTATION FOR RADAR POINT TO ROTATE WITH PLAYER
            float angleRad = (angle + GameController.Instance.player.transform.eulerAngles.y) * Mathf.Deg2Rad;

            // Calculate radar X and Y positions
            float radarX = radarDistance * Mathf.Sin(angleRad);
            float radarY = radarDistance * Mathf.Cos(angleRad);

            // Update RadarPoint1 local position
            //RadarPoint1.localPosition = new Vector3(radarX, RadarPoint1.localPosition.y, -radarY);
            GameObject radarPing = Instantiate(RadarPingPrefab, RadarPingParent.transform);
            radarPing.transform.localPosition = new Vector3(radarX, -1.53f, -radarY);
            // set pingmaterial to new name each pass to avoid material instance sharing and fading out all pings at once
            Material pingMaterial = new Material(radarPingShader);
            pingMaterial.name = $"RadarPingMaterial_{radarPass++}";
            // set material fade - store the current time as spawn time
            pingMaterial.SetFloat("_SpawnTime", Time.time); // Store when this ping was created
            radarPing.GetComponent<MeshRenderer>().material = pingMaterial;
            
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
