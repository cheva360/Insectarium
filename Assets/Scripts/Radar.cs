using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Radar : MonoBehaviour
{
    [SerializeField] private GameObject RadarLine;
    [SerializeField] private GameObject RadarChecker;
    [SerializeField] private Transform MinimapRotate;
    [SerializeField] private GameObject RadarPingPrefab; // Prefab for radar ping visualization
    [SerializeField] private GameObject RadarPingParent; // Parent object for radar prefab pings
    [SerializeField] private float maxRadarDistance = 50f; // Maximum detection range
    [SerializeField] private Shader radarPingShader; // Shader for radar ping visualization
    
    private float radarLineRotation = 0f;
    private float radarSweepAngle = 0f; // Current angle of the radar sweep
    private HashSet<Collider> detectedThisRotation = new HashSet<Collider>(); // Track detected objects per rotation
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // Accumulate rotation over time for smooth animation
        radarLineRotation -= 30f * Time.deltaTime; // 30 degrees per second
        radarSweepAngle += 30f * Time.deltaTime; // 30 degrees per second
        
        // Reset detection tracking after full rotation
        if (radarSweepAngle >= 360f)
        {
            radarSweepAngle -= 360f;
            detectedThisRotation.Clear();
        }
        
        RadarLine.transform.localRotation = Quaternion.Euler(RadarLine.transform.localEulerAngles.x, RadarLine.transform.localEulerAngles.y, radarLineRotation);
        
        // Perform raycast sweep
        PerformRadarSweep();
        
        MinimapRotate.transform.rotation = Quaternion.Euler(MinimapRotate.eulerAngles.x, MinimapRotate.eulerAngles.y, GameController.Instance.player.transform.eulerAngles.y);
    }

    private void PerformRadarSweep()
    {
        // Calculate sweep direction relative to player rotation
        float absoluteAngle = radarSweepAngle + GameController.Instance.player.transform.eulerAngles.y;
        Vector3 sweepDirection = Quaternion.Euler(0, absoluteAngle, 0) * Vector3.forward;
        
        // Perform Raycast from player position
        RaycastHit[] hits = Physics.RaycastAll(
            GameController.Instance.player.transform.position,
            sweepDirection,
            maxRadarDistance
        );
        
        // Process all hits
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.CompareTag("RadarObj") && !detectedThisRotation.Contains(hit.collider))
            {
                detectedThisRotation.Add(hit.collider);
                CreateRadarPing(hit.collider.transform.position);
            }
        }
    }
    
    private void CreateRadarPing(Vector3 worldPosition)
    {
        // Calculate distance from player
        float distance = Vector3.Distance(GameController.Instance.player.transform.position, worldPosition);

        // Calculate angle relative to player's forward direction
        Vector3 directionToObject = (worldPosition - GameController.Instance.player.transform.position).normalized;
        float angle = Vector3.SignedAngle(GameController.Instance.player.transform.forward, directionToObject, Vector3.up);

        // Debug the information
        Debug.Log($"Radar Detection - Position: {worldPosition} | Angle: {angle:F2}° | Distance: {distance:F2}m");

        // Map to radar coordinates (-0.21 to 0.21)
        float normalizedDistance = Mathf.Clamp01(distance / maxRadarDistance); // 0 to 1
        float radarDistance = normalizedDistance * 0.45f; // 0 to 0.45

        // Convert angle to radians for calculation
        // ADDS PLAYER ROTATION FOR RADAR POINT TO ROTATE WITH PLAYER
        float angleRad = (angle + GameController.Instance.player.transform.eulerAngles.y) * Mathf.Deg2Rad;

        // Calculate radar X and Y positions
        float radarX = radarDistance * Mathf.Sin(angleRad);
        float radarY = radarDistance * Mathf.Cos(angleRad);

        // Create radar ping
        GameObject radarPing = Instantiate(RadarPingPrefab, RadarPingParent.transform);
        radarPing.transform.localPosition = new Vector3(radarX, -1.53f, -radarY);
        
    }

    private void OnDrawGizmos()
    {
        // Only draw if in play mode and player exists
        if (!Application.isPlaying || GameController.Instance == null || GameController.Instance.player == null)
            return;

        Vector3 playerPos = GameController.Instance.player.transform.position;
        
        // Calculate sweep direction
        float absoluteAngle = radarSweepAngle + GameController.Instance.player.transform.eulerAngles.y;
        Vector3 sweepDirection = Quaternion.Euler(0, absoluteAngle, 0) * Vector3.forward;
        
        // Draw the sweep ray (thin line)
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(playerPos, playerPos + sweepDirection * maxRadarDistance);
        
        // Draw the maximum detection range circle
        Gizmos.color = Color.green;
        DrawCircle(playerPos, maxRadarDistance, 64);
        
        // Draw the sweep arc to show current position
        Gizmos.color = Color.yellow;
        Vector3 arcStart = Quaternion.Euler(0, GameController.Instance.player.transform.eulerAngles.y, 0) * Vector3.forward * maxRadarDistance;
        Gizmos.DrawLine(playerPos, playerPos + arcStart);
    }
    
    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}
