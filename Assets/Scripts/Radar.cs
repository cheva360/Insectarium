using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Radar : MonoBehaviour
{
    [SerializeField] private GameObject RadarLine;
    [SerializeField] private Transform MinimapRotate;
    [SerializeField] private GameObject RadarPingPrefab;
    [SerializeField] private GameObject RadarPingParent;
    [SerializeField] private float maxRadarDistance = 50f;
    [SerializeField] private float minRadarDistance = 0.05f;    // minimum ping distance from radar center (visual)
    [SerializeField] private float minDetectionDistance = 3f;   // minimum world-space distance to detect an object
    [SerializeField] private float raycastRange = 100f;      // how far the boxcast actually reaches
    [SerializeField] private AudioClip radarPingSound;
    [SerializeField] private float sweepWidth = 2f;
    [SerializeField] private float sweepHeight = 10f;

    private float radarSweepAngle = 0f;                      // monotonically increasing — never wrapped
    private Quaternion radarLineBaseRotation;
    private Dictionary<Collider, float> lastDetectionAngle = new Dictionary<Collider, float>();
    [SerializeField] private float redetectionAngle = 90f;
    private AudioSource audioSource;
    private bool soundPlayedThisFrame = false;

    [SerializeField] private float rotationSpeed = 30f;      // degrees per second
    [SerializeField] private int targetFPS = 30;             // radar LINE visual update rate

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        radarLineBaseRotation = RadarLine.transform.localRotation;

        StartCoroutine(RadarLineVisualCoroutine());
    }

    // Updates the RadarLine UI at a low, throttled rate
    private IEnumerator RadarLineVisualCoroutine()
    {
        float interval = 1f / targetFPS;

        while (true)
        {
            yield return new WaitForSeconds(interval);

            // Use modulo only for the visual rotation
            RadarLine.transform.localRotation = radarLineBaseRotation * Quaternion.AngleAxis(radarSweepAngle % 360f, Vector3.right);
        }
    }

    void Update()
    {
        MinimapRotate.transform.rotation = Quaternion.Euler(
            MinimapRotate.eulerAngles.x,
            MinimapRotate.eulerAngles.y,
            GameController.Instance.player.transform.eulerAngles.y);

        // Monotonically increasing — no wrap, so redetection math is always accurate
        radarSweepAngle += rotationSpeed * Time.deltaTime;

        soundPlayedThisFrame = false;
        PerformRadarSweep();
    }

    private void PerformRadarSweep()
    {
        float absoluteAngle = (radarSweepAngle % 360f) + GameController.Instance.player.transform.eulerAngles.y;
        Vector3 sweepDirection = Quaternion.Euler(0, absoluteAngle, 0) * Vector3.forward;
        Quaternion sweepRotation = Quaternion.LookRotation(sweepDirection, Vector3.up);

        Vector3 origin = GameController.Instance.player.transform.position;
        Vector3 halfExtents = new Vector3(sweepWidth, sweepHeight, 0.1f);

        RaycastHit[] hits = Physics.BoxCastAll(origin, halfExtents, sweepDirection, sweepRotation, raycastRange);

        foreach (RaycastHit hit in hits)
        {
            if (!hit.collider.CompareTag("RadarObj"))
                continue;

            Vector3 toTarget = hit.collider.transform.position - origin;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude > 0.001f)
            {
                float hitDistance = toTarget.magnitude;

                // Close objects fall inside BoxCast origin and register from any angle.
                // Compensate by tightening the required dot the closer the object is.
                // At raycastRange and beyond: dotThreshold = 0.5  (~60° cone, loose)
                // At distance 0:             dotThreshold = 1.0  (must be directly in front)
                float t = Mathf.Clamp01(hitDistance / raycastRange);
                float dotThreshold = Mathf.Lerp(1f, 0.5f, t);

                float dot = Vector3.Dot(sweepDirection, toTarget.normalized);
                if (dot < dotThreshold)
                    continue;
            }

            bool shouldDetect = false;

            if (!lastDetectionAngle.ContainsKey(hit.collider))
            {
                shouldDetect = true;
            }
            else
            {
                float angleTraveled = radarSweepAngle - lastDetectionAngle[hit.collider];
                if (angleTraveled >= redetectionAngle)
                    shouldDetect = true;
            }

            if (shouldDetect)
            {
                lastDetectionAngle[hit.collider] = radarSweepAngle;
                CreateRadarPing(hit.collider.transform.position);
            }
        }
    }

    private void CreateRadarPing(Vector3 worldPosition)
    {
        float distance = Vector3.Distance(GameController.Instance.player.transform.position, worldPosition);
        Vector3 directionToObject = (worldPosition - GameController.Instance.player.transform.position).normalized;
        float angle = Vector3.SignedAngle(GameController.Instance.player.transform.forward, directionToObject, Vector3.up);

        float normalizedDistance = Mathf.Clamp01(distance / maxRadarDistance);
        float radarDistance = Mathf.Max(normalizedDistance * 0.42f, minRadarDistance); // never collapses to center

        float angleRad = (angle + GameController.Instance.player.transform.eulerAngles.y) * Mathf.Deg2Rad;

        float radarX = radarDistance * Mathf.Sin(angleRad);
        float radarY = radarDistance * Mathf.Cos(angleRad);

        GameObject radarPing = Instantiate(RadarPingPrefab, RadarPingParent.transform);
        radarPing.transform.localPosition = new Vector3(radarX, -1.53f, -radarY);

        if (!soundPlayedThisFrame && radarPingSound != null)
        {
            audioSource.PlayOneShot(radarPingSound);
            soundPlayedThisFrame = true;
        }
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || GameController.Instance == null || GameController.Instance.player == null)
            return;

        Vector3 playerPos = GameController.Instance.player.transform.position;
        float absoluteAngle = (radarSweepAngle % 360f) + GameController.Instance.player.transform.eulerAngles.y;
        Vector3 sweepDirection = Quaternion.Euler(0, absoluteAngle, 0) * Vector3.forward;

        // Raycast range line
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(playerPos, playerPos + sweepDirection * raycastRange);

        // Boxcast extent
        Gizmos.color = Color.blue;
        Gizmos.matrix = Matrix4x4.TRS(
            playerPos + sweepDirection * (raycastRange * 0.5f),
            Quaternion.LookRotation(sweepDirection, Vector3.up),
            Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(sweepWidth * 2f, sweepHeight * 2f, 0.2f));
        Gizmos.matrix = Matrix4x4.identity;

        // Max radar display radius (green)
        Gizmos.color = Color.green;
        DrawCircle(playerPos, maxRadarDistance, 64);

        // Raycast range radius (magenta)
        Gizmos.color = Color.magenta;
        DrawCircle(playerPos, raycastRange, 64);

        Gizmos.color = Color.yellow;
        Vector3 arcStart = Quaternion.Euler(0, GameController.Instance.player.transform.eulerAngles.y, 0) * Vector3.forward * raycastRange;
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

    private void OnTriggerExit(Collider other)
    {
        if (lastDetectionAngle.ContainsKey(other))
            lastDetectionAngle.Remove(other);
    }
}
