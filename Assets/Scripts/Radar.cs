using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Radar : MonoBehaviour
{
    [SerializeField] private GameObject RadarLine;
    [SerializeField] private Transform MinimapRotate;
    [SerializeField] private GameObject RadarPingPrefab; // Prefab for radar ping visualization
    [SerializeField] private GameObject RadarPingParent; // Parent object for radar prefab pings
    [SerializeField] private float maxRadarDistance = 50f; // Maximum detection range
    [SerializeField] private AudioClip radarPingSound; // Sound effect for radar ping
    [SerializeField] private float sphereCastRadius = 2f; // Width of the radar sweep beam
                                                           // 
    private float radarLineRotation = 0f;
    private float radarSweepAngle = 0f; // Current angle of the radar sweep
    private Dictionary<Collider, float> lastDetectionAngle = new Dictionary<Collider, float>(); // Track last detection angle per object
    [SerializeField] private float redetectionAngle = 90f; // Minimum angle before object can be detected again
    private AudioSource audioSource;
    private bool soundPlayedThisFrame = false; // Debounce audio playback
    
    // Start is called before the first frame update
    void Start()
    {
        // Get or add AudioSource component
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Reset audio debounce flag at start of frame
        soundPlayedThisFrame = false;
        
        // Accumulate rotation over time for smooth animation
        radarLineRotation -= 30f * Time.deltaTime; // 30 degrees per second
        radarSweepAngle += 30f * Time.deltaTime; // 30 degrees per second
        
        // Normalize angle to 0-360 range
        if (radarSweepAngle >= 360f)
        {
            radarSweepAngle -= 360f;
            // Clean up old entries
            lastDetectionAngle.Clear();
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
        
        // Perform SphereCast from player position for wider detection area
        RaycastHit[] hits = Physics.SphereCastAll(
            GameController.Instance.player.transform.position,
            sphereCastRadius,
            sweepDirection,
            maxRadarDistance
        );
        
        // Process all hits
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.CompareTag("RadarObj"))
            {
                bool shouldDetect = false;
                
                if (!lastDetectionAngle.ContainsKey(hit.collider))
                {
                    // First time detecting this object
                    shouldDetect = true;
                }
                else
                {
                    // Check if enough angle has passed since last detection
                    float angleDifference = Mathf.Abs(Mathf.DeltaAngle(lastDetectionAngle[hit.collider], radarSweepAngle));
                    if (angleDifference >= redetectionAngle)
                    {
                        shouldDetect = true;
                    }
                }
                
                if (shouldDetect)
                {
                    lastDetectionAngle[hit.collider] = radarSweepAngle;
                    CreateRadarPing(hit.collider.transform.position);
                }
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
        //Debug.Log($"Radar Detection - Position: {worldPosition} | Angle: {angle:F2}� | Distance: {distance:F2}m");

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
        
        // Play sound only once per frame even if multiple objects detected
        if (!soundPlayedThisFrame && radarPingSound != null)
        {
            audioSource.PlayOneShot(radarPingSound);
            soundPlayedThisFrame = true;
        }
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
        
        // Draw sphere cast radius
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(playerPos + sweepDirection * (maxRadarDistance * 0.5f), sphereCastRadius);
        
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
